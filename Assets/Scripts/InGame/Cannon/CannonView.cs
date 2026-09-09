using System;
using Framework.Object;
using UnityEngine;

namespace InGame.Cannon
{
    /// <summary>Scene-authored Cannon MVC endpoint. It never creates or searches for its visual hierarchy.</summary>
    [DisallowMultipleComponent]
    public sealed class CannonView : ObView<CannonModel>
    {
        [SerializeField] private Transform yawRoot;
        [SerializeField] private Transform barrelReference;
        [Tooltip("Reference-local direction, measured in world units, from the reference to the Ball spawn point.")]
        [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 0.16f, 0f);

        private CannonModel _ownedModel;
        private CannonController _controller;
        private CannonHead _head;

        public CannonController Controller => _controller;
        public Vector3 MuzzlePosition => RequireHead().MuzzlePosition;
        public Vector3 BarrelDirection => RequireHead().BarrelDirection;

        private void Awake()
        {
            _head = new CannonHead(yawRoot, barrelReference, muzzleOffset);
            _ownedModel = new CannonModel(_head.BarrelDirection);
            _controller = new CannonController(_ownedModel);
            Bind(_ownedModel);
        }

        protected override void RefreshView(CannonModel model)
        {
            if (!RequireHead().TryAim(model.AimDirection)) throw new InvalidOperationException("Cannon could not align its authored barrel horizontally with the launch direction.");
        }

        protected override void OnDestroy()
        {
            try { base.OnDestroy(); }
            finally
            {
                _controller = null;
                _ownedModel = null;
                _head = null;
            }
        }

        private CannonHead RequireHead() => _head ?? throw new InvalidOperationException("CannonView must finish Awake before use.");
    }
}
