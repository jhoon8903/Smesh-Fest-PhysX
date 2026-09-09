using System;
using Framework.Object;
using Framework.Pool;
using UnityEngine;

namespace InGame.Ball
{
    /// <summary>
    /// Owns the current rental token and commands the Rigidbody for one BallModel.
    /// The Rigidbody remains authoritative for runtime motion; the Model does not mirror it.
    /// </summary>
    public sealed class BallController : ObController, IDisposable
    {
        private PoolLease lease;
        private readonly Rigidbody body;

        public BallController(BallModel model, Rigidbody body)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            this.body = body != null ? body : throw new ArgumentNullException(nameof(body));
            if (this.body.isKinematic)
                throw new InvalidOperationException("BallController requires a dynamic Rigidbody.");
        }

        public BallModel Model { get; }
        public bool IsRented { get; private set; }
        public uint RentalEpoch { get; private set; }
        public bool HasLaunched { get; private set; }

        /// <summary>Checks both the Ball epoch and the Pool lease to reject work from an old use.</summary>
        public bool IsCurrentRental(uint rentalEpoch) =>
            IsRented && RentalEpoch == rentalEpoch && Model.IsCurrentRental(rentalEpoch) && lease.IsValid;

        /// <summary>Returns this Ball only when the caller belongs to the current rental epoch.</summary>
        public bool TryReturn(uint rentalEpoch)
        {
            if (!IsCurrentRental(rentalEpoch))
                return false;

            PoolLease currentLease = lease;
            return currentLease.Return();
        }

        /// <summary>Applies one initial PhysX velocity only to the current, not-yet-launched rental.</summary>
        public bool TryLaunch(uint rentalEpoch, Vector3 linearVelocity)
        {
            if (!IsCurrentRental(rentalEpoch) || HasLaunched || body == null || body.isKinematic
                || !IsFinite(linearVelocity) || linearVelocity.sqrMagnitude <= 0.00000001f)
                return false;

            body.angularVelocity = Vector3.zero;
            body.linearVelocity = linearVelocity;
            body.WakeUp();
            HasLaunched = true;
            return true;
        }

        internal void BeginRental(PoolLease rentalLease)
        {
            if (IsRented)
                throw new InvalidOperationException("BallController is already rented.");
            if (!rentalLease.IsValid)
                throw new InvalidOperationException("BallController requires the current PoolLease.");

            uint rentalEpoch = Model.RentalEpoch;
            if (!Model.IsCurrentRental(rentalEpoch))
                throw new InvalidOperationException("BallModel must begin its rental before BallController.");
            if (body == null || body.isKinematic)
                throw new InvalidOperationException("BallController requires a live dynamic Rigidbody for every rental.");

            lease = rentalLease;
            RentalEpoch = rentalEpoch;
            IsRented = true;
            HasLaunched = false;
            ResetMotion(true);
        }

        /// <summary>
        /// Re-applies the intended sleep state after the pooled GameObject becomes active.
        /// Unity does not guarantee that Sleep called while the Rigidbody is inactive survives activation.
        /// </summary>
        internal void OnRentalActivated()
        {
            if (!IsCurrentRental(RentalEpoch) || body == null || body.isKinematic)
                return;

            if (HasLaunched)
                body.WakeUp();
            else
                body.Sleep();
        }

        internal void EndRental()
        {
            IsRented = false;
            RentalEpoch = 0;
            HasLaunched = false;
            lease = default;
            ResetMotion(true);
        }

        public void Dispose() => EndRental();

        private void ResetMotion(bool sleep)
        {
            if (body == null || body.isKinematic)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            if (sleep)
                body.Sleep();
        }

        private static bool IsFinite(Vector3 value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
