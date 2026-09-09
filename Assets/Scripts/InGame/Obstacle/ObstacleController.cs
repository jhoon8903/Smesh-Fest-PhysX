using System;
using Framework.Object;
using Framework.Pool;

namespace InGame.Obstacle
{
    /// <summary>
    /// Owns the current rental token for one ObstacleModel. Physics and gameplay subscriptions are
    /// added only after their authority is defined; this unit does not register no-op callbacks.
    /// </summary>
    public sealed class ObstacleController : ObController, IDisposable
    {
        private PoolLease lease;

        public ObstacleController(ObstacleModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
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

            lease = rentalLease;
            RentalEpoch = rentalEpoch;
            IsRented = true;
        }

        internal void EndRental()
        {
            IsRented = false;
            RentalEpoch = 0;
            lease = default;
        }

        public void Dispose() => EndRental();
    }
}
