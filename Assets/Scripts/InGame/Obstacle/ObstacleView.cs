using System;
using System.Runtime.ExceptionServices;
using Framework.Object;
using Framework.Pool;
using InGame.Config;
using UnityEngine;
using VContainer;

namespace InGame.Obstacle
{
    /// <summary>
    /// Pool adapter and visual endpoint for one reusable Obstacle MVC bundle.
    /// The bundle is created once, while each rent/return reconnects and resets transient state.
    /// </summary>
    public sealed class ObstacleView : ObView<ObstacleModel>, IPoolable
    {
        private ObstacleModel ownedModel;
        private ObstacleController controller;
        private ObstacleConfig settings;
        private bool targetable = true;

        public GameObject PoolObject => gameObject;
        public ObstacleModel OwnedModel => ownedModel;
        public ObstacleController Controller => controller;
        public bool IsTargetable => targetable && isActiveAndEnabled;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(ObstacleConfig obstacleSettings)
        {
            if (settings != null) throw new InvalidOperationException("ObstacleView was already configured.");
            settings = obstacleSettings != null
                ? obstacleSettings
                : throw new ArgumentNullException(nameof(obstacleSettings));
            ApplyPhysicsSettings();
            targetable = settings.TargetableAtStart;
        }

        public void OnPoolCreated(IPoolable owner)
        {
            if (!ReferenceEquals(owner, this))
                throw new InvalidOperationException("ObstacleView must be the owner of its PoolObject.");
            if (ownedModel != null || controller != null)
                throw new InvalidOperationException("Obstacle MVC bundle was already created.");
            if (!TryGetComponent(out Rigidbody body))
                throw new InvalidOperationException("ObstacleView requires a Rigidbody on the same GameObject.");
            if (!TryGetComponent(out Collider _))
                throw new InvalidOperationException("ObstacleView requires a Collider on the same GameObject.");
            if (body.isKinematic)
                throw new InvalidOperationException("ObstacleView requires a dynamic Rigidbody; it does not override Inspector physics settings.");
            if (settings == null)
                throw new InvalidOperationException("ObstacleView requires ObstacleConfig injection before pool creation.");

            targetable = false;
            ownedModel = new ObstacleModel();
            controller = new ObstacleController(ownedModel, body);
        }

        public void OnPoolRent(PoolLease lease)
        {
            EnsureBundle();
            try
            {
                // The clone is inactive here. Reset first, retain the Model, then expose it.
                ApplyPhysicsSettings();
                ownedModel.BeginRental();
                Bind(ownedModel);
                controller.BeginRental(lease);
                targetable = settings.TargetableAtStart;
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
            // OnPoolRent runs while the clone is inactive; wake the body after activation.
            controller?.OnRentalActivated();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (targetable && collision.collider.TryGetComponent(out GroundSurface _))
                targetable = false;
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
            targetable = false;
            Exception failure = null;
            try { ResetRental(); }
            catch (Exception exception) { failure = exception; }

            try { controller?.Dispose(); }
            catch (Exception exception) { failure ??= exception; }

            controller = null;
            ownedModel = null;
            settings = null;
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        protected override void RefreshView(ObstacleModel model)
        {
            // Rental state has no visual representation. PhysX drives this Transform directly;
            // damage, destruction and impact presentation remain follow-up units.
        }

        private void ResetRental()
        {
            targetable = false;
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
                throw new InvalidOperationException("Obstacle MVC bundle must be created before rental.");
        }

        private void ApplyPhysicsSettings()
        {
            if (settings == null || !TryGetComponent(out Rigidbody body))
                throw new InvalidOperationException("ObstacleView requires ObstacleConfig and a Rigidbody on the same GameObject.");
            if (body.isKinematic)
                throw new InvalidOperationException("ObstacleView requires a dynamic Rigidbody.");

            body.mass = settings.Mass;
            body.linearDamping = settings.LinearDamping;
            body.angularDamping = settings.AngularDamping;
            body.interpolation = settings.Interpolation;
            body.collisionDetectionMode = settings.CollisionDetection;
        }
    }
}
