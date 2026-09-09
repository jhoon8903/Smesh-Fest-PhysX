using System;
using System.Runtime.ExceptionServices;
using Framework.Object;
using Framework.Pool;
using InGame.Config;
using InGame.Presentation;
using UnityEngine;

namespace InGame.Obstacle
{
    public sealed class ObstacleController : ObController<ObstacleView, ObstacleModel>
    {
        private PoolLease _lease;
        private readonly Rigidbody _body;
        private readonly ObstacleConfig _settings;
        private bool _created;

        public ObstacleController(ObstacleView view, ObstacleModel model, Rigidbody body,
            GroundFadeReturn groundFadeReturn, ObstacleConfig settings)
            : base(view, model)
        {
            _body = body != null ? body : throw new ArgumentNullException(nameof(body));
            if (_body.isKinematic) throw new InvalidOperationException("ObstacleController requires a dynamic Rigidbody.");
            if (_body.gameObject != view.gameObject) throw new ArgumentException("Obstacle Rigidbody must share the View GameObject.", nameof(body));
            if (groundFadeReturn == null || groundFadeReturn.gameObject != view.gameObject)
                throw new ArgumentException("Obstacle GroundFadeReturn must share the View GameObject.", nameof(groundFadeReturn));
            if (!view.TryGetComponent(out Collider _)) throw new InvalidOperationException("ObstacleController requires a Collider on the View GameObject.");
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
        public bool IsTargetable => !IsDisposed && View != null && View.isActiveAndEnabled && Model.IsTargetable && IsCurrentRental(RentalEpoch);
        public bool IsCurrentRental(uint rentalEpoch) => !IsDisposed && IsRented && RentalEpoch == rentalEpoch && Model.IsCurrentRental(rentalEpoch) && _lease.IsValid;

        public bool TryReturn(uint rentalEpoch)
        {
            if (!IsCurrentRental(rentalEpoch)) return false;
            PoolLease currentLease = _lease;
            return currentLease.Return();
        }

        protected override void OnViewEnabled()
        {
            if (!IsCurrentRental(RentalEpoch) || _body == null || _body.isKinematic) return;
            _body.WakeUp();
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
                    failure = new InvalidOperationException("ObstacleController could not return its active PoolLease during disposal.");
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

        protected override void RefreshView(ObstacleModel model)
        {
        }

        private void HandleCreated()
        {
            if (_created) throw new InvalidOperationException("ObstacleController was already created by a Pool.");
            ApplyPhysicsSettings();
            ResetMotion(false);
            _created = true;
        }

        private void BeginRental(PoolLease rentalLease)
        {
            if (!_created) throw new InvalidOperationException("ObstacleController must be created by its Pool before rental.");
            if (IsRented) throw new InvalidOperationException("ObstacleController is already rented.");
            if (!rentalLease.IsValid) throw new InvalidOperationException("ObstacleController requires the current PoolLease.");
            if (_body == null || _body.isKinematic) throw new InvalidOperationException("ObstacleController requires a live dynamic Rigidbody for every rental.");

            ApplyPhysicsSettings();
            Model.BeginRental(_settings.TargetableAtStart);
            try
            {
                _lease = rentalLease;
                RentalEpoch = Model.RentalEpoch;
                IsRented = true;
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
            _lease = default;
            ResetMotion(false);
            Model.EndRental();
        }

        private void HandleCollision(Collision collision)
        {
            if (collision.collider != null && collision.collider.TryGetComponent(out GroundSurface _))
                Model.MarkGrounded(RentalEpoch);
        }

        private void ResetMotion(bool wake)
        {
            if (_body == null || _body.isKinematic) return;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            if (wake) _body.WakeUp();
            else _body.Sleep();
        }

        private void ApplyPhysicsSettings()
        {
            if (_body == null || _body.isKinematic) throw new InvalidOperationException("ObstacleController requires a live dynamic Rigidbody.");
            _body.mass = _settings.Mass;
            _body.linearDamping = _settings.LinearDamping;
            _body.angularDamping = _settings.AngularDamping;
            _body.interpolation = _settings.Interpolation;
            _body.collisionDetectionMode = _settings.CollisionDetection;
        }
    }
}
