using System;
using UnityEngine;

namespace Framework.Loop
{
    /// <summary>Owns Unity's game time for one scene scope; UI does not receive a separate clock.</summary>
    public sealed class UnityGameTime : IDisposable
    {
        private readonly GameClock _clock;
        private readonly float _previousTimeScale;
        private bool _disposed;

        public UnityGameTime(GameClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _previousTimeScale = Time.timeScale;
            _clock.StateChanged += Apply;
            Apply();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _clock.StateChanged -= Apply;
            Time.timeScale = _previousTimeScale;
        }

        private void Apply()
        {
            // Preserve fixedDeltaTime: physics keeps the existing step in game seconds.
            Time.timeScale = _clock.EffectiveTimeScale;
        }
    }
}
