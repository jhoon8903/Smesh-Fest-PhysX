using System;
using Framework.Object;
using UnityEngine;

namespace InGame.Cannon
{
    public sealed class CannonController : ObController<CannonView, CannonModel>
    {
        public CannonController(CannonView view, CannonModel model)
            : base(view, model)
        {
            try
            {
                Activate();
            }
            catch
            {
                try { Dispose(); }
                catch { }
                throw;
            }
        }

        public Vector3 MuzzlePosition => View != null
            ? View.MuzzlePosition
            : throw new MissingReferenceException("CannonView was destroyed.");

        public bool TryAim(Vector3 launchVelocity)
        {
            return !IsDisposed && View != null && Model.TrySetAimDirection(launchVelocity);
        }

        protected override void RefreshView(CannonModel model)
        {
            if (!View.RenderAim(model.AimDirection))
                throw new InvalidOperationException("Cannon could not align its authored barrel horizontally with the launch direction.");
        }
    }
}
