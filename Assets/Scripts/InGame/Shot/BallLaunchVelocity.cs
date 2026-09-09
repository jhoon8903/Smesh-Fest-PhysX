using InGame.Config;
using UnityEngine;

namespace InGame.Shot
{
    public static class BallLaunchVelocity
    {
        public static bool TryCalculate(Vector3 origin, Vector3 target, BallTrajectoryMode trajectoryMode,
            float launchSpeed, float curveFlightSeconds, Vector3 gravity, out Vector3 velocity)
        {
            velocity = default;
            if (!IsFinite(origin) || !IsFinite(target)) return false;

            Vector3 displacement = target - origin;
            if (displacement.sqrMagnitude <= 0.00000001f) return false;

            switch (trajectoryMode)
            {
                case BallTrajectoryMode.Straight:
                    if (!IsFinite(launchSpeed) || launchSpeed <= 0f) return false;
                    velocity = displacement.normalized * launchSpeed;
                    break;

                case BallTrajectoryMode.Curve:
                    if (!IsFinite(curveFlightSeconds) || curveFlightSeconds <= 0f || !IsFinite(gravity)) return false;
                    velocity = displacement / curveFlightSeconds - 0.5f * gravity * curveFlightSeconds;
                    break;

                default: return false;
            }

            return IsFinite(velocity) && velocity.sqrMagnitude > 0.00000001f;
        }

        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
