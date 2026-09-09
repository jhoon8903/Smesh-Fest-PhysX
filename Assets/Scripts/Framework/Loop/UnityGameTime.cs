using System;
using UnityEngine;

namespace Framework.Loop
{
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
            Time.timeScale = _clock.EffectiveTimeScale;
        }
    }
}
