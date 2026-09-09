using System;
using UnityEngine;
using VContainer;

namespace Framework.Loop
{
    public class UpdateLoop : MonoBehaviour
    {
        #region TimeScale

        private float _requestedTimeScale = 1f;
        public float TimeScale => _clock?.RequestedTimeScale ?? _requestedTimeScale;
        [SerializeField] private float maxScale = 2f;
        [SerializeField] private float minScale = 1f;

        #endregion

        private GameClock _clock;
        private LoopDispatcher _dispatcher;

        public bool IsRunning => _dispatcher != null && _dispatcher.IsRunning;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(GameClock clock, LoopDispatcher dispatcher)
        {
            if (_clock != null) throw new InvalidOperationException("UpdateLoop was already injected.");
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _clock.SetTimeScale(_requestedTimeScale);
            _clock.StateChanged += SynchronizeState;
            SynchronizeState();
        }

        public void StartLoop()
        {
            RequireClock().StartLoop();
        }

        public void StopLoop()
        {
            _clock?.StopLoop();
        }

        public void SetTimeScale(float scale)
        {
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0f)
                throw new ArgumentOutOfRangeException(nameof(scale));
            _requestedTimeScale = Mathf.Clamp(scale, minScale, maxScale);
            _clock?.SetTimeScale(_requestedTimeScale);
        }

        private void Update()
        {
            _dispatcher?.TickUpdate(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            _dispatcher?.TickFixed(Time.fixedDeltaTime);
        }

        private void LateUpdate()
        {
            _dispatcher?.TickLate(Time.deltaTime);
        }

        private void OnEnable()
        {
            SynchronizeState();
        }

        private void OnDisable()
        {
            _dispatcher?.StopLoop();
        }

        private void OnDestroy()
        {
            if (_clock != null) _clock.StateChanged -= SynchronizeState;
        }

        private void SynchronizeState()
        {
            if (_clock == null) return;
            if (isActiveAndEnabled && _clock.EffectiveTimeScale > 0f) _dispatcher.StartLoop();
            else _dispatcher.StopLoop();
        }

        private GameClock RequireClock()
        {
            return _clock ?? throw new InvalidOperationException("UpdateLoop requires GameLifetimeScope injection before starting.");
        }
    }
}
