using System;
using Framework.Object;
using Framework.Pool;
using UnityEngine;

namespace InGame.Obstacle
{
    /// <summary>
    /// Owns the current rental token and resets the authoritative Rigidbody for one ObstacleModel.
    /// Gameplay collision consequences remain outside this lifecycle unit.
    /// </summary>
    public sealed class ObstacleController : ObController, IDisposable
    {
        private PoolLease lease;
        private readonly Rigidbody body;

        public ObstacleController(ObstacleModel model, Rigidbody body)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            this.body = body != null ? body : throw new ArgumentNullException(nameof(body));
            if (this.body.isKinematic)
                throw new InvalidOperationException("ObstacleController requires a dynamic Rigidbody.");
        }

        public ObstacleModel Model { get; }
        public bool IsRented { get; private set; }
        public uint RentalEpoch { get; private set; }

        /// <summary>Checks both the Obstacle epoch and Pool lease to reject work from an old use.</summary>
        public bool IsCurrentRental(uint rentalEpoch) =>
            IsRented && RentalEpoch == rentalEpoch && Model.IsCurrentRental(rentalEpoch) && lease.IsValid;

        /// <summary>Returns this Obstacle only when the caller belongs to the current rental epoch.</summary>
        public bool TryReturn(uint rentalEpoch)
        {
            if (!IsCurrentRental(rentalEpoch))
                return false;

            PoolLease currentLease = lease;
            return currentLease.Return();
        }

        internal void BeginRental(PoolLease rentalLease)
        {
            if (IsRented)
                throw new InvalidOperationException("ObstacleController is already rented.");
            if (!rentalLease.IsValid)
                throw new InvalidOperationException("ObstacleController requires the current PoolLease.");

            uint rentalEpoch = Model.RentalEpoch;
            if (!Model.IsCurrentRental(rentalEpoch))
                throw new InvalidOperationException("ObstacleModel must begin its rental before ObstacleController.");
            if (body == null || body.isKinematic)
                throw new InvalidOperationException("ObstacleController requires a live dynamic Rigidbody for every rental.");

            lease = rentalLease;
            RentalEpoch = rentalEpoch;
            IsRented = true;
            ResetMotion(true);
        }

        /// <summary>
        /// Wakes the reset body after the pooled GameObject becomes active.
        /// WakeUp while inactive is not a reliable observable PhysX state.
        /// </summary>
        internal void OnRentalActivated()
        {
            if (!IsCurrentRental(RentalEpoch) || body == null || body.isKinematic)
                return;

            body.WakeUp();
        }

        internal void EndRental()
        {
            IsRented = false;
            RentalEpoch = 0;
            lease = default;
            ResetMotion(false);
        }

        public void Dispose() => EndRental();

        private void ResetMotion(bool wake)
        {
            if (body == null || body.isKinematic)
                return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            if (wake)
                body.WakeUp();
            else
                body.Sleep();
        }
    }
}
