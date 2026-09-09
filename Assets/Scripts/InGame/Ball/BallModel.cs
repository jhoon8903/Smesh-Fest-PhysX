using System;
using Framework.Object;

namespace InGame.Ball
{
    /// <summary>
    /// Physics-independent lifetime state for one pooled Ball bundle.
    /// Unity's Rigidbody owns the runtime Transform and velocity state in the current PhysX slice.
    /// </summary>
    public sealed class BallModel : ObModel
    {
        public bool IsRented { get; private set; }
        public uint RentalEpoch { get; private set; }

        /// <summary>True only for callbacks that belong to the currently active rental.</summary>
        public bool IsCurrentRental(uint rentalEpoch) =>
            IsRented && rentalEpoch != 0 && rentalEpoch == RentalEpoch;

        internal void BeginRental()
        {
            if (IsRented)
                throw new InvalidOperationException("BallModel is already rented.");

            RentalEpoch = NextEpoch(RentalEpoch);
            IsRented = true;
            NotifyChanged();
        }

        internal void EndRental()
        {
            if (!IsRented)
                return;

            IsRented = false;
            NotifyChanged();
        }

        private static uint NextEpoch(uint value)
        {
            value++;
            return value == 0 ? 1u : value;
        }
    }
}
