using System;
using Framework.Object;
using Framework.Pool;

namespace InGame.Ball
{
    /// <summary>
    /// Owns the current rental token for one BallModel. Physics and input subscriptions will be
    /// added only after their authority is defined; this unit does not register a no-op Loop callback.
    /// </summary>
    public sealed class BallController : ObController, IDisposable
    {
        private PoolLease lease;

        public BallController(BallModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public BallModel Model { get; }
        public bool IsRented { get; private set; }
        public uint RentalEpoch { get; private set; }

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

        internal void BeginRental(PoolLease rentalLease)
        {
            if (IsRented)
                throw new InvalidOperationException("BallController is already rented.");
            if (!rentalLease.IsValid)
                throw new InvalidOperationException("BallController requires the current PoolLease.");

            uint rentalEpoch = Model.RentalEpoch;
            if (!Model.IsCurrentRental(rentalEpoch))
                throw new InvalidOperationException("BallModel must begin its rental before BallController.");

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
