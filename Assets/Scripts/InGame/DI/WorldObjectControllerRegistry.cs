using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Framework.Object;
using Framework.Pool;
using InGame.Ball;
using InGame.Cannon;
using InGame.Config;
using InGame.Obstacle;
using InGame.Presentation;
using UnityEngine;

namespace InGame.DI
{
    public sealed class WorldObjectControllerRegistry : IPoolObjectComposer, IDisposable
    {
        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            internal static readonly ReferenceComparer<T> Instance = new();
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
        }

        private readonly BallConfig _ballSettings;
        private readonly ObstacleConfig _obstacleSettings;
        private readonly Dictionary<BallView, BallController> _balls = new(ReferenceComparer<BallView>.Instance);
        private readonly Dictionary<ObstacleView, ObstacleController> _obstacles = new(ReferenceComparer<ObstacleView>.Instance);
        private readonly Dictionary<CannonView, CannonController> _cannons = new(ReferenceComparer<CannonView>.Instance);
        private bool _disposed;

        public WorldObjectControllerRegistry(BallConfig ballSettings, ObstacleConfig obstacleSettings)
        {
            _ballSettings = ballSettings != null ? ballSettings : throw new ArgumentNullException(nameof(ballSettings));
            _obstacleSettings = obstacleSettings != null ? obstacleSettings : throw new ArgumentNullException(nameof(obstacleSettings));
        }

        public int BallCount => _balls.Count;
        public int ObstacleCount => _obstacles.Count;
        public int CannonCount => _cannons.Count;

        public void Compose(IPoolable owner)
        {
            ThrowIfDisposed();
            if (owner == null || owner is UnityEngine.Object unityOwner && unityOwner == null)
                throw new ArgumentNullException(nameof(owner));

            switch (owner)
            {
                case BallView ball:
                    ComposeBall(ball);
                    return;
                case ObstacleView obstacle:
                    ComposeObstacle(obstacle);
                    return;
                case ObView view:
                    throw new InvalidOperationException($"No Controller composition is registered for {view.GetType().FullName}.");
            }
        }

        public CannonController GetOrCreate(CannonView view)
        {
            ThrowIfDisposed();
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (_cannons.TryGetValue(view, out CannonController existing)) return existing;

            CannonController controller = new CannonController(view, new CannonModel(view.BarrelDirection));
            try
            {
                _cannons.Add(view, controller);
                controller.Disposed += HandleControllerDisposed;
            }
            catch
            {
                controller.Dispose();
                throw;
            }
            return controller;
        }

        public bool TryGet(BallView view, out BallController controller)
        {
            controller = null;
            return !_disposed && view != null && _balls.TryGetValue(view, out controller) && !controller.IsDisposed;
        }

        public bool TryGet(ObstacleView view, out ObstacleController controller)
        {
            controller = null;
            return !_disposed && view != null && _obstacles.TryGetValue(view, out controller) && !controller.IsDisposed;
        }

        public bool TryGet(CannonView view, out CannonController controller)
        {
            controller = null;
            return !_disposed && view != null && _cannons.TryGetValue(view, out controller) && !controller.IsDisposed;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Exception failure = null;

            BallController[] balls = new BallController[_balls.Count];
            _balls.Values.CopyTo(balls, 0);
            for (int i = 0; i < balls.Length; i++) DisposeController(balls[i], ref failure);

            ObstacleController[] obstacles = new ObstacleController[_obstacles.Count];
            _obstacles.Values.CopyTo(obstacles, 0);
            for (int i = 0; i < obstacles.Length; i++) DisposeController(obstacles[i], ref failure);

            CannonController[] cannons = new CannonController[_cannons.Count];
            _cannons.Values.CopyTo(cannons, 0);
            for (int i = 0; i < cannons.Length; i++) DisposeController(cannons[i], ref failure);

            _balls.Clear();
            _obstacles.Clear();
            _cannons.Clear();
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void ComposeBall(BallView view)
        {
            if (_balls.ContainsKey(view)) throw new InvalidOperationException("BallView already has a Controller.");
            if (!view.TryGetComponent(out Rigidbody body)) throw new InvalidOperationException("BallView requires a Rigidbody on the same GameObject.");
            if (!view.TryGetComponent(out GroundFadeReturn fade)) throw new InvalidOperationException("BallView requires GroundFadeReturn on the same GameObject.");
            BallController controller = new BallController(view, new BallModel(), body, fade, _ballSettings);
            try
            {
                _balls.Add(view, controller);
                controller.Disposed += HandleControllerDisposed;
            }
            catch
            {
                controller.Dispose();
                throw;
            }
        }

        private void ComposeObstacle(ObstacleView view)
        {
            if (_obstacles.ContainsKey(view)) throw new InvalidOperationException("ObstacleView already has a Controller.");
            if (!view.TryGetComponent(out Rigidbody body)) throw new InvalidOperationException("ObstacleView requires a Rigidbody on the same GameObject.");
            if (!view.TryGetComponent(out GroundFadeReturn fade)) throw new InvalidOperationException("ObstacleView requires GroundFadeReturn on the same GameObject.");
            ObstacleController controller = new ObstacleController(view, new ObstacleModel(), body, fade, _obstacleSettings);
            try
            {
                _obstacles.Add(view, controller);
                controller.Disposed += HandleControllerDisposed;
            }
            catch
            {
                controller.Dispose();
                throw;
            }
        }

        private void HandleControllerDisposed(ObView view)
        {
            switch (view)
            {
                case BallView ball:
                    _balls.Remove(ball);
                    break;
                case ObstacleView obstacle:
                    _obstacles.Remove(obstacle);
                    break;
                case CannonView cannon:
                    _cannons.Remove(cannon);
                    break;
            }
        }

        private static void DisposeController(IDisposable controller, ref Exception failure)
        {
            try { controller.Dispose(); }
            catch (Exception exception) { failure ??= exception; }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WorldObjectControllerRegistry));
        }
    }
}
