using System;
using UnityEngine;

namespace InGame.Config
{
    [Serializable]
    [CreateAssetMenu(fileName = "PhysXConfig", menuName = "Smesh Fest/Config/PhysX")]
    public sealed class PhysXConfig : ScriptableObject
    {
        [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
        [SerializeField, Min(0.01f)] private float pointerRayDistance = 100f;
        [SerializeField] private LayerMask pointerCollisionMask = 1 << 8;
        [SerializeField] private QueryTriggerInteraction pointerTriggerInteraction = QueryTriggerInteraction.Ignore;

        public Vector3 Gravity => gravity;
        public float PointerRayDistance => pointerRayDistance;
        public LayerMask PointerCollisionMask => pointerCollisionMask;
        public QueryTriggerInteraction PointerTriggerInteraction => pointerTriggerInteraction;
    }
}
