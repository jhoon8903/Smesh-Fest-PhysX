using System;
using Framework.Object;

namespace InGame.Obstacle
{
    public sealed class ObstacleModel : ObModel
    {
        public bool IsRented { get; private set; }
        public bool IsTargetable { get; private set; }
        public uint RentalEpoch { get; private set; }
        public bool IsCurrentRental(uint rentalEpoch) => IsRented && rentalEpoch != 0 && rentalEpoch == RentalEpoch;

        internal void BeginRental(bool targetable)
        {
            if (IsRented) throw new InvalidOperationException("ObstacleModel is already rented.");
            RentalEpoch = NextEpoch(RentalEpoch);
            IsRented = true;
            IsTargetable = targetable;
            NotifyChanged();
        }

        internal bool MarkGrounded(uint rentalEpoch)
        {
            if (!IsCurrentRental(rentalEpoch) || !IsTargetable) return false;
            IsTargetable = false;
            NotifyChanged();
            return true;
        }

        internal void EndRental()
        {
            if (!IsRented) return;
            IsRented = false;
            IsTargetable = false;
            NotifyChanged();
        }

        private static uint NextEpoch(uint value)
        {
            value++;
            return value == 0 ? 1u : value;
        }
    }
}
