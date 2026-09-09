using System;
using Framework.Object;

namespace InGame.Ball
{
    public sealed class BallModel : ObModel
    {
        public bool IsRented { get; private set; }
        public uint RentalEpoch { get; private set; }
        public bool IsCurrentRental(uint rentalEpoch) => IsRented && rentalEpoch != 0 && rentalEpoch == RentalEpoch;

        internal void BeginRental()
        {
            if (IsRented) throw new InvalidOperationException("BallModel is already rented.");
            RentalEpoch = NextEpoch(RentalEpoch);
            IsRented = true;
            NotifyChanged();
        }

        internal void EndRental()
        {
            if (!IsRented) return;
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
