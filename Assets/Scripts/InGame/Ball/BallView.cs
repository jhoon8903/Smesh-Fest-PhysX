using System;
using System.Runtime.ExceptionServices;
using Framework.Object;
using Framework.Pool;
using InGame.Config;
using InGame.Obstacle;
using UnityEngine;
using VContainer;

namespace InGame.Ball
{
    /// <summary>
    /// Pool adapter and visual endpoint for one reusable Ball MVC bundle.
    /// The bundle is created once, while each rent/return reconnects and resets its transient state.
    /// </summary>
    public sealed class BallView : ObView<BallModel>, IPoolable
    {
        private BallModel _ownedModel;
        private BallController _controller;
        private BallConfig _settings;
        private uint _collisionRentalEpoch;

        public GameObject PoolObject => gameObject;
        public BallModel OwnedModel => _ownedModel;
        public BallController Controller => _controller;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(BallConfig settings)
        {
            if (_settings != null) throw new InvalidOperationException("BallView was already configured.");
            _settings = settings != null ? settings : throw new ArgumentNullException(nameof(settings));
            ApplyPhysicsSettings();
        }

        public void OnPoolCreated(IPoolable owner)
        {
            if (!ReferenceEquals(owner, this))
                throw new InvalidOperationException("BallView must be the owner of its PoolObject.");
            if (_ownedModel != null || _controller != null)
                throw new InvalidOperationException("Ball MVC bundle was already created.");
            if (!TryGetComponent(out Rigidbody body))
                throw new InvalidOperationException("BallView requires a Rigidbody on the same GameObject.");
            if (!TryGetComponent(out Collider _))
                throw new InvalidOperationException("BallView requires a Collider on the same GameObject.");
            if (body.isKinematic)
                throw new InvalidOperationException("BallView requires a dynamic Rigidbody; it does not override Inspector physics settings.");
            if (_settings == null)
                throw new InvalidOperationException("BallView requires BallConfig injection before pool creation.");

            _ownedModel = new BallModel();
            _controller = new BallController(_ownedModel, body);
        }

        public void OnPoolRent(PoolLease lease)
        {
            EnsureBundle();
            try
            {
                // The clone is still inactive here. Reset first, retain the Model, then expose it.
                ApplyPhysicsSettings();
                _ownedModel.BeginRental();
                Bind(_ownedModel);
                _controller.BeginRental(lease);
                _collisionRentalEpoch = _controller.RentalEpoch;
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
            _controller?.OnRentalActivated();
        }

        protected override void OnDestroy()
        {
            Exception failure = null;
            try { DestroyBundle(); }
            catch (Exception exception) { failure = exception; }

            try { base.OnDestroy(); }
            catch (Exception exception) { failure ??= exception; }

            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.collider != null && collision.collider.TryGetComponent(out ObstacleView _))
                _controller?.TryActivateGravityFromObstacleCollision(_collisionRentalEpoch);
        }

        private void DestroyBundle()
        {
            Exception failure = null;
            try { ResetRental(); }
            catch (Exception exception) { failure = exception; }

            try { _controller?.Dispose(); }
            catch (Exception exception) { failure ??= exception; }

            _controller = null;
            _ownedModel = null;
            _settings = null;
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        protected override void RefreshView(BallModel model)
        {
            // Rental state has no visual representation. PhysX drives this Transform directly;
            // trail and impact presentation remain separate follow-up units.
        }

        private void ResetRental()
        {
            Exception failure = null;
            _collisionRentalEpoch = 0;
            try { _controller?.EndRental(); }
            catch (Exception exception) { failure = exception; }

            try { Unbind(); }
            catch (Exception exception) { failure ??= exception; }

            try { _ownedModel?.EndRental(); }
            catch (Exception exception) { failure ??= exception; }

            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void EnsureBundle()
        {
            if (_ownedModel == null || _controller == null)
                throw new InvalidOperationException("Ball MVC bundle must be created before rental.");
        }

        private void ApplyPhysicsSettings()
        {
            if (_settings == null || !TryGetComponent(out Rigidbody body))
                throw new InvalidOperationException("BallView requires BallConfig and a Rigidbody on the same GameObject.");
            if (body.isKinematic)
                throw new InvalidOperationException("BallView requires a dynamic Rigidbody.");

            body.mass = _settings.Mass;
            body.linearDamping = _settings.LinearDamping;
            body.angularDamping = _settings.AngularDamping;
            body.interpolation = _settings.Interpolation;
            body.collisionDetectionMode = _settings.CollisionDetection;
        }
    }
}
