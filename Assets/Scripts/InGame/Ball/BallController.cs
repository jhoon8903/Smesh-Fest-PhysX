using System;
using Framework.Object;
using Framework.Pool;
using InGame.Config;
using UnityEngine;

namespace InGame.Ball
{
    /// <summary>
    /// Owns the current rental token and commands the Rigidbody for one BallModel.
    /// The Rigidbody remains authoritative for runtime motion; the Model does not mirror it.
    /// </summary>
    public sealed class BallController : ObController, IDisposable
    {
        private PoolLease _lease;
        private readonly Rigidbody _body;
        private bool _waitingForGravityActivation;

        public BallController(BallModel model, Rigidbody body)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _body = body != null ? body : throw new ArgumentNullException(nameof(body));
            if (_body.isKinematic) throw new InvalidOperationException("BallController requires a dynamic Rigidbody.");
        }

        public BallModel Model { get; }
        public bool IsRented { get; private set; }
        public uint RentalEpoch { get; private set; }
        public bool HasLaunched { get; private set; }

        /// <summary>Checks both the Ball epoch and the Pool lease to reject work from an old use.</summary>
        public bool IsCurrentRental(uint rentalEpoch) => IsRented && RentalEpoch == rentalEpoch && Model.IsCurrentRental(rentalEpoch) && _lease.IsValid;

        /// <summary>Returns this Ball only when the caller belongs to the current rental epoch.</summary>
        public bool TryReturn(uint rentalEpoch)
        {
            if (!IsCurrentRental(rentalEpoch)) return false;
            PoolLease currentLease = _lease;
            return currentLease.Return();
        }

        /// <summary>Applies one initial PhysX velocity only to the current, not-yet-launched rental.</summary>
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

        /// <summary>Enables Straight-ball gravity once when this rental directly hits an ObstacleView.</summary>
        internal bool TryActivateGravityFromObstacleCollision(uint rentalEpoch)
        {
            if (!IsCurrentRental(rentalEpoch) || !HasLaunched || !_waitingForGravityActivation
                || _body == null || _body.isKinematic) return false;

            ActivateGravity();
            return true;
        }

        internal void BeginRental(PoolLease rentalLease)
        {
            if (IsRented) throw new InvalidOperationException("BallController is already rented.");
            if (!rentalLease.IsValid) throw new InvalidOperationException("BallController requires the current PoolLease.");
            uint rentalEpoch = Model.RentalEpoch;
            if (!Model.IsCurrentRental(rentalEpoch)) throw new InvalidOperationException("BallModel must begin its rental before BallController.");
            if (_body == null || _body.isKinematic) throw new InvalidOperationException("BallController requires a live dynamic Rigidbody for every rental.");
            _lease = rentalLease;
            RentalEpoch = rentalEpoch;
            IsRented = true;
            HasLaunched = false;
            _waitingForGravityActivation = false;
            ResetMotion(true);
        }

        /// <summary>
        /// Re-applies the intended sleep state after the pooled GameObject becomes active.
        /// Unity does not guarantee that Sleep called while the Rigidbody is inactive survives activation.
        /// </summary>
        internal void OnRentalActivated()
        {
            if (!IsCurrentRental(RentalEpoch) || _body == null || _body.isKinematic) return;
            if (HasLaunched) _body.WakeUp();
            else _body.Sleep();
        }

        internal void EndRental()
        {
            IsRented = false;
            RentalEpoch = 0;
            HasLaunched = false;
            _waitingForGravityActivation = false;
            _lease = default;
            ResetMotion(true);
        }

        public void Dispose() => EndRental();

        private void ResetMotion(bool sleep)
        {
            if (_body == null || _body.isKinematic) return;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.useGravity = false;
            if (sleep) _body.Sleep();
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
