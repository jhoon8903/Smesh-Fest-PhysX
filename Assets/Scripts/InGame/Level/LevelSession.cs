using System;
using UnityEngine;
using VContainer;

namespace InGame.Level
{
    public sealed class LevelSession : MonoBehaviour
    {
        private LevelSpawner _levelSpawner;
        private bool _started;
        private bool _authoredBlocksWereActive;

        public bool IsStarted => _started;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(LevelSpawner injectedLevelSpawner)
        {
            if (_levelSpawner != null) throw new InvalidOperationException("LevelSession was already configured.");
            _levelSpawner = injectedLevelSpawner ?? throw new ArgumentNullException(nameof(injectedLevelSpawner));
        }
        
        public bool TryStart(out string failure)
        {
            failure = null;
            if (_started)
            {
                failure = "LevelSession is already started. Call ReturnAll before starting again.";
                return false;
            }
            if (_levelSpawner == null)
            {
                failure = "LevelSession requires LevelSpawner injection before starting.";
                return false;
            }
            Transform runtimeRoot = _levelSpawner.RuntimeRoot;
            if (runtimeRoot == null)
            {
                failure = "LevelSession requires LevelSpawner to have an explicit RuntimeRoot before starting.";
                return false;
            }
            try
            {
                if (_levelSpawner.TrySpawn(out failure))
                {
                    _started = true;
                    return true;
                }
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }
            return false;
        }
        
        public void ReturnAll()
        {
            if (_levelSpawner == null) throw new InvalidOperationException("LevelSession requires LevelSpawner injection before returning.");

            try
            {
                _levelSpawner.ReturnAll();
            }
            finally
            {
                _started = false;
            }
        }

        public void ResetSession() => ReturnAll();
    }
}
