using System;
using UnityEngine;

namespace InGame.Config
{
    public enum BallTrajectoryMode
    {
        Straight = 0,
        Curve = 1
    }
    [Serializable]
    [CreateAssetMenu(fileName = "BallConfig", menuName = "Smesh Fest/Config/Ball")]
    public sealed class BallConfig : ScriptableObject
    {
        [SerializeField] private BallTrajectoryMode trajectoryMode = BallTrajectoryMode.Straight;
        [SerializeField, Min(0.01f)] private float launchSpeed = 20f;
        [SerializeField, Min(0.05f)] private float curveFlightSeconds = 0.75f;
        [SerializeField] private float gravityActivationWorldZ;
        [SerializeField, Min(0.1f)] private float maxLifetimeSeconds = 6f;
        [SerializeField] private float recycleBelowY = -1f;
        [SerializeField, Min(0.0001f)] private float mass = 1f;
        [SerializeField, Min(0f)] private float linearDamping;
        [SerializeField, Min(0f)] private float angularDamping = 0.05f;
        [SerializeField] private RigidbodyInterpolation interpolation;
        [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousDynamic;

        public BallTrajectoryMode TrajectoryMode => trajectoryMode;
        public float LaunchSpeed => launchSpeed;
        public float CurveFlightSeconds => curveFlightSeconds;
        public float GravityActivationWorldZ => gravityActivationWorldZ;
        public float MaxLifetimeSeconds => maxLifetimeSeconds;
        public float RecycleBelowY => recycleBelowY;
        public float Mass => mass;
        public float LinearDamping => linearDamping;
        public float AngularDamping => angularDamping;
        public RigidbodyInterpolation Interpolation => interpolation;
        public CollisionDetectionMode CollisionDetection => collisionDetection;
    }
}
