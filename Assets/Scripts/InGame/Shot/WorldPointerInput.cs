using System;
using System.Collections.Generic;
using Framework.Loop;
using Framework.Pool;
using InGame.Cannon;
using InGame.Config;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace InGame.Shot
{
    /// <summary>Scene adapter that polls one pointer press through the central UpdateLoop.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldPointerInput : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CannonView cannon;
        [SerializeField] private PoolConfig ballPool;
        [SerializeField] private Transform ballParent;

        private readonly List<RaycastResult> _uiHits = new(8);
        private ILoopEvents _loopEvents;
        private ShotDirector _director;
        private PhysXConfig _physXSettings;
        private PointerEventData _pointerEventData;
        private EventSystem _pointerEventSystem;
        private bool _subscribed;

        public int ActiveShotCount => _director?.ActiveShotCount ?? 0;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(PoolFactory poolFactory, ILoopEvents events, BallConfig ballSettings,
            PhysXConfig physXSettings)
        {
            if (_director != null || _loopEvents != null)
                throw new InvalidOperationException("WorldPointerInput was already injected.");
            if (worldCamera == null || cannon == null || ballPool == null)
                throw new InvalidOperationException(
                    "WorldPointerInput requires Camera, Cannon, and Ball Pool references.");
            if (ballSettings == null || physXSettings == null)
                throw new InvalidOperationException("WorldPointerInput requires BallConfig and PhysXConfig injection.");
            _loopEvents = events ?? throw new ArgumentNullException(nameof(events));
            _physXSettings = physXSettings;
            _director = new ShotDirector(poolFactory, ballPool, cannon, ballParent, ballSettings, physXSettings);
            Subscribe();
        }

        /// <summary>Raycasts and fires a supplied point; UI filtering belongs to the live pointer adapter.</summary>
        public bool TryFireScreenPoint(Vector2 screenPoint)
        {
            return _director != null
                   && ObstacleTargetRaycaster.TryGetTarget(worldCamera, screenPoint, _physXSettings,
                       out _, out Vector3 target)
                   && _director.TryFire(target);
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            _director?.Dispose();
            _director = null;
            _physXSettings = null;
            _pointerEventData = null;
            _pointerEventSystem = null;
            _uiHits.Clear();
        }

        private void Subscribe()
        {
            if (_subscribed || _loopEvents == null || !isActiveAndEnabled) return;
            _loopEvents.UpdateTick += HandleUpdate;
            _loopEvents.FixedTick += HandleFixed;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _loopEvents.UpdateTick -= HandleUpdate;
            _loopEvents.FixedTick -= HandleFixed;
            _subscribed = false;
        }

        private void HandleUpdate(float deltaTime)
        {
            _director.Tick(deltaTime);
            Pointer pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;
            Vector2 screenPoint = pointer.position.ReadValue();
            if (!Screen.safeArea.Contains(screenPoint) || IsOverUi(screenPoint)) return;
            TryFireScreenPoint(screenPoint);
        }

        private void HandleFixed(float _)
        {
            _director.TickPhysics();
        }

        private bool IsOverUi(Vector2 screenPoint)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;
            if (_pointerEventData == null || _pointerEventSystem != eventSystem)
            {
                _pointerEventData = new PointerEventData(eventSystem);
                _pointerEventSystem = eventSystem;
            }

            _pointerEventData.Reset();
            _pointerEventData.position = screenPoint;
            eventSystem.RaycastAll(_pointerEventData, _uiHits);
            for (int i = 0; i < _uiHits.Count; i++)
            {
                if (_uiHits[i].module is GraphicRaycaster) return true;
            }
            return false;
        }
    }
}
