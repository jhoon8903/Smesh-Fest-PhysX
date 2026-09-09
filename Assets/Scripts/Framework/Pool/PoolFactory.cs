using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Pool
{
    public sealed class PoolFactory : IDisposable
    {
        private sealed class Registration
        {
            public PoolConfig Config;
            public Transform Root;
            public Transform ConstructionRoot;
            public IObjectResolver Resolver;
            public IPoolObjectComposer Composer;
            public Pool Pool;

            public IPoolable Create()
            {
                MonoBehaviour clone = UnityEngine.Object.Instantiate(Config.Prefab, ConstructionRoot, false);
                try
                {
                    clone.gameObject.SetActive(false);
                    Resolver.InjectGameObject(clone.gameObject);
                    IPoolable owner = (IPoolable)clone;
                    Composer.Compose(owner);
                    return owner;
                }
                catch
                {
                    clone.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(clone.gameObject);
                    throw;
                }
            }

            public void Destroy(IPoolable value)
            {
                if (value is UnityEngine.Object unityObject && unityObject == null)
                    return;
                if (value == null || value.PoolObject == null)
                    return;
                value.PoolObject.SetActive(false);
                UnityEngine.Object.Destroy(value.PoolObject);
            }
        }

        private readonly PoolContainer _container;
        private readonly IObjectResolver _resolver;
        private readonly IPoolObjectComposer _composer;
        private readonly bool _retainMinimum;
        private readonly Func<double> _now;
        private readonly Dictionary<PoolConfig, Registration> _registrations = new Dictionary<PoolConfig, Registration>();
        private Registration[] _ordered = Array.Empty<Registration>();
        private CancellationTokenSource _maintenanceCancellation;
        private bool _initializing;
        private bool _initialized;
        private bool _disposed;
        private bool _collecting;

        public PoolFactory(PoolContainer container, IObjectResolver resolver,
            IPoolObjectComposer composer, bool retainMinimum, Func<double> now = null)
        {
            _container = container != null ? container : throw new ArgumentNullException(nameof(container));
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _composer = composer ?? throw new ArgumentNullException(nameof(composer));
            _retainMinimum = retainMinimum;
            _now = now ?? ReadRealtime;
        }

        public void Initialize(bool startMaintenance = true)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PoolFactory));
            if (_initialized) return;
            if (_initializing) throw new InvalidOperationException("PoolFactory initialization is already in progress.");

            _initializing = true;
            try
            {
                IReadOnlyList<PoolConfig> configs = _container.Configs;
                if (configs == null) throw new InvalidOperationException("PoolContainer requires a config collection.");
                _ordered = new Registration[configs.Count];
                for (int i = 0; i < configs.Count; i++)
                {
                    PoolConfig config = configs[i];
                    if (config == null) throw new InvalidOperationException("PoolContainer contains a missing PoolConfig.");
                    config.Validate();
                    if (_registrations.ContainsKey(config)) throw new InvalidOperationException("PoolContainer contains the same PoolConfig more than once.");
                    Registration registration = new Registration { Config = config, Resolver = _resolver, Composer = _composer };
                    _registrations.Add(config, registration);
                    _ordered[i] = registration;
                }

                for (int i = 0; i < _ordered.Length; i++)
                {
                    Registration registration = _ordered[i];
                    registration.Root = new GameObject(registration.Config.PoolRootName).transform;
                    registration.Root.SetParent(_container.transform, false);
                    GameObject construction = new GameObject("Construction");
                    construction.SetActive(false);
                    construction.transform.SetParent(registration.Root, false);
                    registration.ConstructionRoot = construction.transform;
                    registration.Pool = new Pool(registration.Config, registration.Root, registration.Create, registration.Destroy, _now);
                    registration.Pool.Prewarm();
                    if (_disposed) throw new ObjectDisposedException(nameof(PoolFactory));
                }

                _initialized = true;
                if (!startMaintenance || _ordered.Length <= 0) return;
                _maintenanceCancellation = new CancellationTokenSource();
                MaintainIdleAsync(_maintenanceCancellation.Token).Forget();
            }
            catch
            {
                try { Dispose(); }
                catch { /* Preserve the initialization failure after cleaning up all registrations. */ }
                throw;
            }
            finally
            {
                _initializing = false;
            }
        }

        public bool TryRent(PoolConfig config, in PoolSpawnArgs args, out PoolLease lease)
        {
            lease = default;
            return !_disposed && _initialized && config != null
                   && _registrations.TryGetValue(config, out Registration registration)
                   && registration.Pool.TryRent(args, out lease);
        }

        public IPool GetPool(PoolConfig config)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PoolFactory));
            if (!_initialized) throw new InvalidOperationException("Initialize the PoolFactory before querying a pool.");
            if (config == null || !_registrations.TryGetValue(config, out Registration registration)) throw new ArgumentException("The PoolConfig is not registered in this container.", nameof(config));
            return registration.Pool;
        }

        public int CollectIdle()
        {
            if (_disposed || !_initialized || _collecting) return 0;
            _collecting = true;
            int removed = 0;
            Exception failure = null;
            try
            {
                for (int i = 0; i < _ordered.Length && !_disposed; i++)
                {
                    try { removed += _ordered[i].Pool.TrimIdle(_retainMinimum); }
                    catch (Exception exception) { failure ??= exception; }
                }
            }
            finally
            {
                _collecting = false;
            }

            return failure != null ? throw failure : removed;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _maintenanceCancellation?.Cancel();
            _maintenanceCancellation?.Dispose();
            _maintenanceCancellation = null;

            Exception failure = null;
            for (int i = _ordered.Length - 1; i >= 0; i--)
            {
                Registration registration = _ordered[i];
                if (registration == null) continue;
                try { registration.Pool?.Dispose(); }
                catch (Exception exception) { failure ??= exception; }
                if (registration.Root != null) UnityEngine.Object.Destroy(registration.Root.gameObject);
            }
            _registrations.Clear();
            if (failure != null) throw failure;
        }

        private async UniTask MaintainIdleAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await UniTask.Delay(1000, ignoreTimeScale: true, cancellationToken: token);
                    if (token.IsCancellationRequested)
                        break;
                    try { CollectIdle(); }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }

        private static double ReadRealtime() => Time.realtimeSinceStartupAsDouble;
    }
}
