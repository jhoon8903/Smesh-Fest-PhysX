using System;
using Framework.Object;
using UnityEngine;

namespace InGame.Cannon
{
    /// <summary>Stores the cannon's commanded launch direction, not any Ball physics state.</summary>
    public sealed class CannonModel : ObModel
    {
        public CannonModel(Vector3 initialDirection)
        {
            if (!TryNormalize(initialDirection, out Vector3 normalized))
                throw new ArgumentException("Cannon requires a finite non-zero initial direction.", nameof(initialDirection));

            AimDirection = normalized;
        }

        public Vector3 AimDirection { get; private set; }

        internal bool TrySetAimDirection(Vector3 direction)
        {
            if (!TryNormalize(direction, out Vector3 normalized)) return false;
            if ((AimDirection - normalized).sqrMagnitude <= 0.00000001f) return true;

            AimDirection = normalized;
            NotifyChanged();
            return true;
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = default;
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z)
                || value.sqrMagnitude <= 0.00000001f)
                return false;

            normalized = value.normalized;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
