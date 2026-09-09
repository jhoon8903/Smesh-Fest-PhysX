using System;
using System.Runtime.ExceptionServices;
using Framework.Pool;
using InGame.Ball;
using InGame.Cannon;
using InGame.Config;
using UnityEngine;

namespace InGame.Shot
{
    /// <summary>Coordinates one target command without becoming the authority for Ball motion.</summary>
    public sealed class ShotDirector : IDisposable
    {
        private struct ActiveShot
        {
            public BallView Ball;
            public uint Epoch;
            public float Elapsed;
        }

        private readonly PoolFactory _poolFactory;
        private readonly PoolConfig _ballPool;
        private readonly BallConfig _ballSettings;
        private readonly PhysXConfig _physXSettings;
        private readonly CannonView _cannon;
        private readonly Transform _ballParent;
        private readonly ActiveShot[] _activeShots;
        private int _activeShotCount;
        private bool _disposed;

        public ShotDirector(PoolFactory poolFactory, PoolConfig ballPool, CannonView cannon,
            Transform ballParent, BallConfig ballSettings, PhysXConfig physXSettings)
        {
            _poolFactory = poolFactory ?? throw new ArgumentNullException(nameof(poolFactory));
            _ballPool = ballPool != null ? ballPool : throw new ArgumentNullException(nameof(ballPool));
            _ballSettings = ballSettings != null
                ? ballSettings
                : throw new ArgumentNullException(nameof(ballSettings));
            _physXSettings = physXSettings != null
                ? physXSettings
                : throw new ArgumentNullException(nameof(physXSettings));
            _cannon = cannon != null ? cannon : throw new ArgumentNullException(nameof(cannon));

            _ballParent = ballParent;
            _activeShots = new ActiveShot[Mathf.Max(1, ballPool.MaxPool)];
        }

        public int ActiveShotCount => _activeShotCount;

        public bool TryFire(Vector3 target)
        {
            if (_disposed || !IsFinite(target) || !IsFinite(_ballSettings.GravityActivationWorldZ)
                || _activeShotCount >= _activeShots.Length) return false;
            if (!_cannon.Controller.TryAim(target - _cannon.MuzzlePosition)) return false;
            Vector3 gravity = _physXSettings.Gravity;
            Physics.gravity = gravity;
            Vector3 origin = _cannon.MuzzlePosition;
            if (!BallLaunchVelocity.TryCalculate(origin, target, _ballSettings.TrajectoryMode,
                    _ballSettings.LaunchSpeed, _ballSettings.CurveFlightSeconds, gravity, out Vector3 velocity)) return false;

            Quaternion rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            if (!_poolFactory.TryRent(_ballPool, new PoolSpawnArgs(origin, rotation, _ballParent), out PoolLease lease)) return false;

            BallView ball = lease.Value as BallView;
            if (ball == null || ball.Controller == null)
            {
                lease.Return();
                return false;
            }

            uint epoch = ball.Controller.RentalEpoch;
            if (!ball.Controller.TryLaunch(epoch, velocity, _ballSettings.TrajectoryMode))
            {
                lease.Return();
                return false;
            }

            _activeShots[_activeShotCount++] = new ActiveShot { Ball = ball, Epoch = epoch };
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (_disposed) return;
            if (!IsFinite(deltaTime) || deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            for (int i = _activeShotCount - 1; i >= 0; i--)
            {
                ActiveShot shot = _activeShots[i];
                BallController controller = shot.Ball != null ? shot.Ball.Controller : null;
                if (controller == null || !controller.IsCurrentRental(shot.Epoch))
                {
                    RemoveAt(i);
                    continue;
                }

                shot.Elapsed += deltaTime;
                _activeShots[i] = shot;
                if (shot.Elapsed < _ballSettings.MaxLifetimeSeconds
                    && shot.Ball.transform.position.y >= _ballSettings.RecycleBelowY) continue;
                if (controller.TryReturn(shot.Epoch) || !controller.IsCurrentRental(shot.Epoch)) RemoveAt(i);
            }
        }

        public void TickPhysics()
        {
            if (_disposed) return;
            float gravityActivationWorldZ = _ballSettings.GravityActivationWorldZ;
            for (int i = _activeShotCount - 1; i >= 0; i--)
            {
                ActiveShot shot = _activeShots[i];
                BallController controller = shot.Ball != null ? shot.Ball.Controller : null;
                controller?.TickPhysics(shot.Epoch, gravityActivationWorldZ);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Exception failure = null;
            for (int i = _activeShotCount - 1; i >= 0; i--)
            {
                ActiveShot shot = _activeShots[i];
                try
                {
                    BallController controller = shot.Ball != null ? shot.Ball.Controller : null;
                    if (controller != null && controller.IsCurrentRental(shot.Epoch)) controller.TryReturn(shot.Epoch);
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }
                _activeShots[i] = default;
            }
            _activeShotCount = 0;
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void RemoveAt(int index)
        {
            int last = --_activeShotCount;
            if (index != last) _activeShots[index] = _activeShots[last];
            _activeShots[last] = default;
        }

        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
