using System;
using UnityEngine;

namespace InGame.Cannon
{
    /// <summary>
    /// Applies world-Y yaw to the cannon root while preserving every authored child rotation.
    /// The muzzle offset is expressed in reference-local direction and world units.
    /// </summary>
    public sealed class CannonHead
    {
        private readonly Transform _yawRoot;
        private readonly Transform _barrelReference;
        private readonly Vector3 _muzzleOffset;
        private readonly Quaternion _initialYawRotation;

        public CannonHead(Transform yawRoot, Transform barrelReference, Vector3 muzzleOffset)
        {
            this._yawRoot = yawRoot != null ? yawRoot : throw new ArgumentNullException(nameof(yawRoot));
            this._barrelReference = barrelReference != null
                ? barrelReference
                : throw new ArgumentNullException(nameof(barrelReference));
            if (this._barrelReference != this._yawRoot && !this._barrelReference.IsChildOf(this._yawRoot))
                throw new ArgumentException("The barrel reference must be the yaw root or one of its children.", nameof(barrelReference));
            if (!TryNormalize(muzzleOffset, out _)) throw new ArgumentException("Muzzle offset must be finite and non-zero.", nameof(muzzleOffset));

            this._muzzleOffset = muzzleOffset;
            _initialYawRotation = this._yawRoot.rotation;
        }

        public Vector3 MuzzlePosition => _barrelReference.position + _barrelReference.TransformDirection(_muzzleOffset);

        public Vector3 BarrelDirection => _barrelReference.TransformDirection(_muzzleOffset.normalized).normalized;

        public bool TryAim(Vector3 direction)
        {
            if (!TryNormalize(direction, out Vector3 desired)) return false;
            Vector3 desiredHorizontal = Vector3.ProjectOnPlane(desired, Vector3.up);
            if (desiredHorizontal.sqrMagnitude <= 0.00000001f) return false;

            _yawRoot.rotation = _initialYawRotation;
            Vector3 currentHorizontal = Vector3.ProjectOnPlane(BarrelDirection, Vector3.up);
            if (currentHorizontal.sqrMagnitude <= 0.00000001f) return false;

            float yaw = Vector3.SignedAngle(currentHorizontal, desiredHorizontal, Vector3.up);
            _yawRoot.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * _yawRoot.rotation;
            Vector3 aimedHorizontal = Vector3.ProjectOnPlane(BarrelDirection, Vector3.up);
            return aimedHorizontal.sqrMagnitude > 0.00000001f
                   && Vector3.Dot(aimedHorizontal.normalized, desiredHorizontal.normalized) >= 0.9999f;
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = default;
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z) || value.sqrMagnitude <= 0.00000001f) return false;
            normalized = value.normalized;
            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
