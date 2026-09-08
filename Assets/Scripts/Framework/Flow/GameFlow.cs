using System;
using Framework.Loop;
using UnityEngine;
using VContainer;
using static Framework.Log.GameLog;

namespace Framework.Flow
{
    public class GameFlow : MonoBehaviour
    {
        [SerializeField] private GameLogLevel logLevel = GameLogLevel.Debug;
        private UpdateLoop _loop;
        private bool _started;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(UpdateLoop loop)
        {
            _loop = loop ?? throw new ArgumentNullException(nameof(loop));
        }

        private void Awake()
        {
            SetLogLevel(logLevel);
        }

        private void Start()
        {
            if (_loop == null) throw new InvalidOperationException("GameFlow requires GameLifetimeScope injection before starting.");
            _started = true;
            _loop.StartLoop();
        }

        private void OnEnable()
        {
            if (_started) _loop.StartLoop();
        }

        private void OnDisable()
        {
            if (_loop != null) _loop.StopLoop();
        }
    }
}
