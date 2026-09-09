using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Framework.Loop
{
    public sealed class GameClock : IGamePause, IDisposable
    {
        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            bool IEqualityComparer<object>.Equals(object left, object right) => ReferenceEquals(left, right);
            public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
        }

        private readonly HashSet<object> _pauseOwners = new(new ReferenceComparer());
        private bool _isDisposed;

        public event Action StateChanged;

        public float RequestedTimeScale { get; private set; } = 1f;
        public float EffectiveTimeScale => IsRunning && !IsPaused ? RequestedTimeScale : 0f;
        public bool IsRunning { get; private set; }

        public bool IsPaused => _pauseOwners.Count > 0;
        public int PauseCount => _pauseOwners.Count;

        public void StartLoop()
        {
            ThrowIfDisposed();
            if (IsRunning) return;
            IsRunning = true;
            NotifyStateChanged();
        }

        public void StopLoop()
        {
            if (_isDisposed || !IsRunning) return;
            IsRunning = false;
            NotifyStateChanged();
        }

        public void SetTimeScale(float timeScale)
        {
            ThrowIfDisposed();
            if (float.IsNaN(timeScale) || float.IsInfinity(timeScale) || timeScale < 0f) throw new ArgumentOutOfRangeException(nameof(timeScale));
            if (Mathf.Approximately(RequestedTimeScale, timeScale)) return;
            RequestedTimeScale = timeScale;
            NotifyStateChanged();
        }

        public bool Pause(object owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            ThrowIfDisposed();
            if (!_pauseOwners.Add(owner)) return false;
            NotifyStateChanged();
            return true;
        }

        public bool Resume(object owner)
        {
            if (owner == null || _isDisposed) return false;
            if (!_pauseOwners.Remove(owner)) return false;
            NotifyStateChanged();
            return true;
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            IsRunning = false;
            _pauseOwners.Clear();
            StateChanged = null;
        }

        private void NotifyStateChanged()
        {
            StateChanged?.Invoke();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(GameClock));
        }
    }
}
