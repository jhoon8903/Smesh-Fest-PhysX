using System;
using Framework.Object;
using UnityEngine;

namespace InGame.Cannon
{
    /// <summary>Accepts aim commands while the View remains responsible for authored transforms.</summary>
    public sealed class CannonController : ObController
    {
        public CannonController(CannonModel model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
        }

        public CannonModel Model { get; }

        public bool TryAim(Vector3 launchVelocity) => Model.TrySetAimDirection(launchVelocity);
    }
}
