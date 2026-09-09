using System;
using Framework.Object;
using UnityEngine;

namespace InGame.Cannon
{
    [DisallowMultipleComponent]
    public sealed class CannonView : ObView
    {
        [SerializeField] private Transform yawRoot;
        [SerializeField] private Transform barrelReference;
        [Tooltip("Reference-local direction, measured in world units, from the reference to the Ball spawn point.")]
        [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 0.16f, 0f);
        private CannonHead _head;

        public Vector3 MuzzlePosition => RequireHead().MuzzlePosition;
        public Vector3 BarrelDirection => RequireHead().BarrelDirection;

        private void Awake()
        {
            RequireHead();
        }

        internal bool RenderAim(Vector3 direction) => RequireHead().TryAim(direction);

        protected override void OnDestroy()
        {
            try { base.OnDestroy(); }
            finally { _head = null; }
        }

        private CannonHead RequireHead()
        {
            return _head ??= new CannonHead(yawRoot, barrelReference, muzzleOffset);
        }
    }
}
