using System;
using UnityEngine;

namespace InGame.Config
{
    [Serializable]
    [CreateAssetMenu(fileName = "ObstacleConfig", menuName = "Smesh Fest/Config/Obstacle")]
    public sealed class ObstacleConfig : ScriptableObject
    {
        [SerializeField] private bool targetableAtStart = true;
        [SerializeField, Min(0.0001f)] private float mass = 1f;
        [SerializeField, Min(0f)] private float linearDamping;
        [SerializeField, Min(0f)] private float angularDamping = 0.05f;
        [SerializeField] private RigidbodyInterpolation interpolation;
        [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.Continuous;

        public bool TargetableAtStart => targetableAtStart;
        public float Mass => mass;
        public float LinearDamping => linearDamping;
        public float AngularDamping => angularDamping;
        public RigidbodyInterpolation Interpolation => interpolation;
        public CollisionDetectionMode CollisionDetection => collisionDetection;
    }
}
