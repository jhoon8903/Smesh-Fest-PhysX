using System;
using UnityEngine;

namespace InGame.Config
{
    [Serializable]
    [CreateAssetMenu(fileName = "CannonConfig", menuName = "Smesh Fest/Config/Cannon")]
    public sealed class CannonConfig : ScriptableObject
    {
        [SerializeField] private Vector3 lookRotationOffsetEuler;
        [SerializeField] private Vector3 muzzleOffset = new(0f, 0.16f, 0f);
        [SerializeField, Min(0f)] private float headRecoilDistance;
        [SerializeField, Min(0f)] private float headRecoilSeconds;
        [SerializeField, Min(0f)] private float headRecoverySeconds;

        public Vector3 LookRotationOffsetEuler => lookRotationOffsetEuler;
        public Vector3 MuzzleOffset => muzzleOffset;
        public float HeadRecoilDistance => headRecoilDistance;
        public float HeadRecoilSeconds => headRecoilSeconds;
        public float HeadRecoverySeconds => headRecoverySeconds;
    }
}
