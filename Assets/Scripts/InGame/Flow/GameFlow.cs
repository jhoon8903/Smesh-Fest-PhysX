using System;
using Framework.Log;
using Framework.Loop;
using InGame.Level;
using UnityEngine;
using VContainer;
using static Framework.Log.GameLog;

namespace InGame.Flow
{
    public class GameFlow : MonoBehaviour
    {
        [SerializeField] private GameLogLevel logLevel = GameLogLevel.Debug;
        private UpdateLoop _loop;
        private LevelSession _levelSession;
        private bool _started;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(UpdateLoop loop, LevelSession levelSession)
        {
            _loop = loop ?? throw new ArgumentNullException(nameof(loop));
            _levelSession = levelSession ?? throw new ArgumentNullException(nameof(levelSession));
        }

        private void Awake()
        {
            SetLogLevel(logLevel);
        }

        private void Start()
        {
            if (_loop == null || _levelSession == null) throw new InvalidOperationException("GameFlow requires GameLifetimeScope injection before starting.");
            if (!_levelSession.TryStart(out string failure))
            {
                GameLog.D("[GameFlow] Level start failed: " + failure, this);
                return;
            }
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
