using System;
using Framework.Observer;

namespace Framework.Loop
{
    /// <summary>
    /// Synchronously dispatches caller-supplied deltas on the main thread.
    /// </summary>
    /// <remarks>
    /// StopLoop preserves subscriptions and allows the current dispatch to finish. Dispose is final
    /// cleanup: it removes subscriptions and prevents later setup.
    /// </remarks>
    public sealed class LoopDispatcher : ILoopEvents, IDisposable
    {
        private readonly Observable<float> _updateTicks = new();
        private readonly Observable<float> _fixedTicks = new();
        private readonly Observable<float> _lateTicks = new();

        private bool _isRunning;
        private bool _isDisposed;
        private bool _isTicking;

        public bool IsRunning => _isRunning;

        public event Action<float> UpdateTick
        {
            add
            {
                ThrowIfDisposed();
                _updateTicks.Subscribe(value);
            }
            remove
            {
                if (!_isDisposed) _updateTicks.Unsubscribe(value);
            }
        }

        public event Action<float> FixedTick
        {
            add
            {
                ThrowIfDisposed();
                _fixedTicks.Subscribe(value);
            }
            remove
            {
                if (!_isDisposed) _fixedTicks.Unsubscribe(value);
            }
        }

        public event Action<float> LateTick
        {
            add
            {
                ThrowIfDisposed();
                _lateTicks.Subscribe(value);
            }
            remove
            {
                if (!_isDisposed) _lateTicks.Unsubscribe(value);
            }
        }

        public void StartLoop()
        {
            ThrowIfDisposed();
            _isRunning = true;
        }

        public void StopLoop()
        {
            if (_isDisposed) return;
            _isRunning = false;
        }

        public void TickUpdate(float deltaTime)
        {
            Tick(_updateTicks, deltaTime);
        }

        public void TickFixed(float deltaTime)
        {
            Tick(_fixedTicks, deltaTime);
        }

        public void TickLate(float deltaTime)
        {
            Tick(_lateTicks, deltaTime);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _isRunning = false;
            _updateTicks.Clear();
            _fixedTicks.Clear();
            _lateTicks.Clear();
        }

        private void Tick(Observable<float> ticks, float deltaTime)
        {
            if (_isDisposed || !_isRunning) return;
            if (_isTicking) throw new InvalidOperationException("LoopDispatcher does not support reentrant Tick calls.");
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));

            _isTicking = true;
            try
            {
                ticks.Publish(deltaTime);
            }
            finally
            {
                _isTicking = false;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(LoopDispatcher));
        }
    }
}
