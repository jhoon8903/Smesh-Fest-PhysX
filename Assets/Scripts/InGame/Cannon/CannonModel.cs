using System;
using Framework.Object;
using UnityEngine;

namespace InGame.Cannon
{
    public sealed class CannonModel : ObModel
    {
        public CannonModel(Vector3 initialDirection)
        {
            if (!TryNormalize(initialDirection, out Vector3 normalized)) throw new ArgumentException("Cannon requires a finite non-zero initial direction.", nameof(initialDirection));
            AimDirection = normalized;
        }

        public Vector3 AimDirection { get; private set; }

        internal bool TrySetAimDirection(Vector3 direction)
        {
            if (!TryNormalize(direction, out Vector3 normalized)) return false;
            if ((AimDirection - normalized).sqrMagnitude <= 0.00000001f) return true;

            Vector3 previous = AimDirection;
            AimDirection = normalized;
            try
            {
                NotifyChanged();
            }
            catch
            {
                AimDirection = previous;
                try { NotifyChanged(); }
                catch { }
                throw;
            }
            return true;
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = default;
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z)
                || value.sqrMagnitude <= 0.00000001f
                || Vector3.ProjectOnPlane(value, Vector3.up).sqrMagnitude <= 0.00000001f)
                return false;

            normalized = value.normalized;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
