using System;
using System.Runtime.ExceptionServices;
using Framework.Object;
using Framework.Pool;
using InGame.Config;
using InGame.Obstacle;
using InGame.Presentation;
using UnityEngine;

namespace InGame.Ball
{
    public sealed class BallController : ObController<BallView, BallModel>
    {
        private PoolLease _lease;
        private readonly Rigidbody _body;
        private readonly BallConfig _settings;
        private bool _waitingForGravityActivation;
        private bool _created;

        public BallController(BallView view, BallModel model, Rigidbody body,
            GroundFadeReturn groundFadeReturn, BallConfig settings)
            : base(view, model)
        {
            _body = body != null ? body : throw new ArgumentNullException(nameof(body));
            if (_body.isKinematic) throw new InvalidOperationException("BallController requires a dynamic Rigidbody.");
            if (_body.gameObject != view.gameObject) throw new ArgumentException("Ball Rigidbody must share the View GameObject.", nameof(body));
            if (groundFadeReturn == null || groundFadeReturn.gameObject != view.gameObject)
                throw new ArgumentException("Ball GroundFadeReturn must share the View GameObject.", nameof(groundFadeReturn));
            if (!view.TryGetComponent(out Collider _)) throw new InvalidOperationException("BallController requires a Collider on the View GameObject.");
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            GroundFadeReturn = groundFadeReturn;

            try
            {
                view.Created += HandleCreated;
                view.Rented += BeginRental;
                view.Returned += EndRental;
                view.CollisionEntered += HandleCollision;
                Activate();
            }
            catch
            {
                try { Dispose(); }
                catch { }
                throw;
            }
        }

        public GroundFadeReturn GroundFadeReturn { get; }
        public bool IsRented { get; private set; }
        public uint RentalEpoch { get; private set; }
        public bool HasLaunched { get; private set; }
        public Vector3 Position => _body != null ? _body.position : throw new MissingReferenceException("Ball Rigidbody was destroyed.");
        public bool IsCurrentRental(uint rentalEpoch) => !IsDisposed && IsRented && RentalEpoch == rentalEpoch && Model.IsCurrentRental(rentalEpoch) && _lease.IsValid;

        public bool TryReturn(uint rentalEpoch)
        {
            if (!IsCurrentRental(rentalEpoch)) return false;
            PoolLease currentLease = _lease;
            return currentLease.Return();
        }

        public bool TryLaunch(uint rentalEpoch, Vector3 linearVelocity, BallTrajectoryMode trajectoryMode)
        {
            if (!IsCurrentRental(rentalEpoch) || HasLaunched || _body == null || _body.isKinematic
                || !IsFinite(linearVelocity) || linearVelocity.sqrMagnitude <= 0.00000001f
                || (trajectoryMode != BallTrajectoryMode.Straight && trajectoryMode != BallTrajectoryMode.Curve)) return false;
            _body.useGravity = trajectoryMode == BallTrajectoryMode.Curve;
            _waitingForGravityActivation = trajectoryMode == BallTrajectoryMode.Straight;
            _body.angularVelocity = Vector3.zero;
            _body.linearVelocity = linearVelocity;
            _body.WakeUp();
            HasLaunched = true;
            return true;
        }

        internal void TickPhysics(uint rentalEpoch, float gravityActivationWorldZ)
        {
            if (!IsCurrentRental(rentalEpoch) || !HasLaunched || !_waitingForGravityActivation
                || _body == null || _body.isKinematic || !IsFinite(gravityActivationWorldZ)) return;
            if (_body.position.z <= gravityActivationWorldZ) return;

            ActivateGravity();
        }

        internal bool TryActivateGravityFromObstacleCollision(uint rentalEpoch)
        {
            if (!IsCurrentRental(rentalEpoch) || !HasLaunched || !_waitingForGravityActivation
                || _body == null || _body.isKinematic) return false;

            ActivateGravity();
            return true;
        }

        protected override void OnViewEnabled()
        {
            if (!IsCurrentRental(RentalEpoch) || _body == null || _body.isKinematic) return;
            if (HasLaunched) _body.WakeUp();
            else _body.Sleep();
        }

        protected override void DisposeController()
        {
            Exception failure = null;
            PoolLease currentLease = _lease;
            try
            {
                if (View.IsDestroying)
                {
                    currentLease.DetachDestroyedValue();
                }
                else if (currentLease.IsValid && !currentLease.Return())
                    failure = new InvalidOperationException("BallController could not return its active PoolLease during disposal.");
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            View.Created -= HandleCreated;
            View.Rented -= BeginRental;
            View.Returned -= EndRental;
            View.CollisionEntered -= HandleCollision;
            try { EndRental(); }
            catch (Exception exception) { failure ??= exception; }
            _created = false;
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        protected override void RefreshView(BallModel model)
        {
        }

        private void HandleCreated()
        {
            if (_created) throw new InvalidOperationException("BallController was already created by a Pool.");
            ApplyPhysicsSettings();
            ResetMotion(true);
            _created = true;
        }

        private void BeginRental(PoolLease rentalLease)
        {
            if (!_created) throw new InvalidOperationException("BallController must be created by its Pool before rental.");
            if (IsRented) throw new InvalidOperationException("BallController is already rented.");
            if (!rentalLease.IsValid) throw new InvalidOperationException("BallController requires the current PoolLease.");
            if (_body == null || _body.isKinematic) throw new InvalidOperationException("BallController requires a live dynamic Rigidbody for every rental.");

            ApplyPhysicsSettings();
            Model.BeginRental();
            try
            {
                _lease = rentalLease;
                RentalEpoch = Model.RentalEpoch;
                IsRented = true;
                HasLaunched = false;
                _waitingForGravityActivation = false;
                ResetMotion(true);
            }
            catch
            {
                IsRented = false;
                RentalEpoch = 0;
                _lease = default;
                Model.EndRental();
                throw;
            }
        }

        private void EndRental()
        {
            IsRented = false;
            RentalEpoch = 0;
            HasLaunched = false;
            _waitingForGravityActivation = false;
            _lease = default;
            ResetMotion(true);
            Model.EndRental();
        }

        private void HandleCollision(Collision collision)
        {
            if (collision.collider != null && collision.collider.TryGetComponent(out ObstacleView _))
                TryActivateGravityFromObstacleCollision(RentalEpoch);
        }

        private void ResetMotion(bool sleep)
        {
            if (_body == null || _body.isKinematic) return;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.useGravity = false;
            if (sleep) _body.Sleep();
        }

        private void ApplyPhysicsSettings()
        {
            if (_body == null || _body.isKinematic) throw new InvalidOperationException("BallController requires a live dynamic Rigidbody.");
            _body.mass = _settings.Mass;
            _body.linearDamping = _settings.LinearDamping;
            _body.angularDamping = _settings.AngularDamping;
            _body.interpolation = _settings.Interpolation;
            _body.collisionDetectionMode = _settings.CollisionDetection;
        }

        private void ActivateGravity()
        {
            _waitingForGravityActivation = false;
            _body.useGravity = true;
            _body.WakeUp();
        }

        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
