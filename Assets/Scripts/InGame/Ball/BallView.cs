using System;
using System.Runtime.ExceptionServices;
using Framework.Object;
using Framework.Pool;
using UnityEngine;

namespace InGame.Ball
{
    /// <summary>
    /// Pool adapter and visual endpoint for one reusable Ball MVC bundle.
    /// The bundle is created once, while each rent/return reconnects and resets its transient state.
    /// </summary>
    public sealed class BallView : ObView<BallModel>, IPoolable
    {
        private BallModel ownedModel;
        private BallController controller;

        public GameObject PoolObject => gameObject;
        public BallModel OwnedModel => ownedModel;
        public BallController Controller => controller;

        public void OnPoolCreated(IPoolable owner)
        {
            if (!ReferenceEquals(owner, this))
                throw new InvalidOperationException("BallView must be the owner of its PoolObject.");
            if (ownedModel != null || controller != null)
                throw new InvalidOperationException("Ball MVC bundle was already created.");
            if (!TryGetComponent(out Rigidbody body))
                throw new InvalidOperationException("BallView requires a Rigidbody on the same GameObject.");
            if (!TryGetComponent(out Collider _))
                throw new InvalidOperationException("BallView requires a Collider on the same GameObject.");
            if (body.isKinematic)
                throw new InvalidOperationException("BallView requires a dynamic Rigidbody; it does not override Inspector physics settings.");

            ownedModel = new BallModel();
            controller = new BallController(ownedModel, body);
        }

        public void OnPoolRent(PoolLease lease)
        {
            EnsureBundle();
            try
            {
                // The clone is still inactive here. Reset first, retain the Model, then expose it.
                ownedModel.BeginRental();
                Bind(ownedModel);
                controller.BeginRental(lease);
            }
            catch
            {
                try { ResetRental(); }
                catch { /* Preserve the rental preparation failure. */ }
                throw;
            }
        }

        public void OnPoolReturn() => ResetRental();

        public void OnPoolDestroy() => DestroyBundle();

        protected override void OnEnable()
        {
            base.OnEnable();
            // OnPoolRent runs while the clone is inactive; enforce the intended state after activation.
            controller?.OnRentalActivated();
        }

        protected override void OnDestroy()
        {
            Exception failure = null;
            try { DestroyBundle(); }
            catch (Exception exception) { failure = exception; }

            try { base.OnDestroy(); }
            catch (Exception exception) { failure ??= exception; }

            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void DestroyBundle()
        {
            Exception failure = null;
            try { ResetRental(); }
            catch (Exception exception) { failure = exception; }

            try { controller?.Dispose(); }
            catch (Exception exception) { failure ??= exception; }

            controller = null;
            ownedModel = null;
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        protected override void RefreshView(BallModel model)
        {
            // Rental state has no visual representation. PhysX drives this Transform directly;
            // trail and impact presentation remain separate follow-up units.
        }

        private void ResetRental()
        {
            Exception failure = null;
            try { controller?.EndRental(); }
            catch (Exception exception) { failure = exception; }

            try { Unbind(); }
            catch (Exception exception) { failure ??= exception; }

            try { ownedModel?.EndRental(); }
            catch (Exception exception) { failure ??= exception; }

            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void EnsureBundle()
        {
            if (ownedModel == null || controller == null)
                throw new InvalidOperationException("Ball MVC bundle must be created before rental.");
        }
    }
}
