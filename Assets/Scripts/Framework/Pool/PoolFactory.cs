using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Pool
{
    /// <summary>Owns one scene's pools, their cloned objects, and idle maintenance.</summary>
    public sealed class PoolFactory : IDisposable
    {
        private sealed class Registration
        {
            public PoolConfig Config;
            public Transform Root;
            public Transform ConstructionRoot;
            public IObjectResolver Resolver;
            public Pool Pool;

            public IPoolable Create()
            {
                // An inactive parent prevents OnEnable before injection. Never toggle the source prefab.
                MonoBehaviour clone = UnityEngine.Object.Instantiate(Config.Prefab, ConstructionRoot, false);
                try
                {
                    clone.gameObject.SetActive(false);
                    Resolver.InjectGameObject(clone.gameObject);
                    return (IPoolable)clone;
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
                // Scene teardown may destroy the component before the separate DI scope is disposed.
                if (value is UnityEngine.Object unityObject && unityObject == null)
                    return;
                if (value == null || value.PoolObject == null)
                    return;
                value.PoolObject.SetActive(false);
                UnityEngine.Object.Destroy(value.PoolObject);
            }
        }

        private readonly PoolContainer container;
        private readonly IObjectResolver resolver;
        private readonly bool retainMinimum;
        private readonly Func<double> now;
        private readonly Dictionary<PoolConfig, Registration> registrations = new Dictionary<PoolConfig, Registration>();
        private Registration[] ordered = Array.Empty<Registration>();
        private CancellationTokenSource maintenanceCancellation;
        private bool initializing;
        private bool initialized;
        private bool disposed;
        private bool collecting;

        public PoolFactory(PoolContainer container, IObjectResolver resolver,
            bool retainMinimum, Func<double> now = null)
        {
            this.container = container != null ? container : throw new ArgumentNullException(nameof(container));
            this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            this.retainMinimum = retainMinimum;
            this.now = now ?? ReadRealtime;
        }

        public void Initialize(bool startMaintenance = true)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PoolFactory));
            if (initialized)
                return;
            if (initializing)
                throw new InvalidOperationException("PoolFactory initialization is already in progress.");

            initializing = true;
            try
            {
                // Check the whole catalog before creating any roots or invoking prefab lifecycle code.
                IReadOnlyList<PoolConfig> configs = container.Configs;
                if (configs == null)
                    throw new InvalidOperationException("PoolContainer requires a config collection.");
                ordered = new Registration[configs.Count];
                for (int i = 0; i < configs.Count; i++)
                {
                    PoolConfig config = configs[i];
                    if (config == null)
                        throw new InvalidOperationException("PoolContainer contains a missing PoolConfig.");
                    config.Validate();
                    if (registrations.ContainsKey(config))
                        throw new InvalidOperationException("PoolContainer contains the same PoolConfig more than once.");
                    Registration registration = new Registration { Config = config, Resolver = resolver };
                    registrations.Add(config, registration);
                    ordered[i] = registration;
                }

                for (int i = 0; i < ordered.Length; i++)
                {
                    Registration registration = ordered[i];
                    registration.Root = new GameObject(registration.Config.PoolRootName).transform;
                    registration.Root.SetParent(container.transform, false);
                    GameObject construction = new GameObject("Construction");
                    construction.SetActive(false);
                    construction.transform.SetParent(registration.Root, false);
                    registration.ConstructionRoot = construction.transform;
                    registration.Pool = new Pool(registration.Config, registration.Root,
                        registration.Create, registration.Destroy, now);
                    registration.Pool.Prewarm();
                    if (disposed)
                        throw new ObjectDisposedException(nameof(PoolFactory));
                }

                initialized = true;
                if (startMaintenance && ordered.Length > 0)
                {
                    maintenanceCancellation = new CancellationTokenSource();
                    MaintainIdleAsync(maintenanceCancellation.Token).Forget();
                }
            }
            catch
            {
                try { Dispose(); }
                catch { /* Preserve the initialization failure after cleaning up all registrations. */ }
                throw;
            }
            finally
            {
                initializing = false;
            }
        }

        public bool TryRent(PoolConfig config, in PoolSpawnArgs args, out PoolLease lease)
        {
            lease = default;
            return !disposed && initialized && config != null
                   && registrations.TryGetValue(config, out Registration registration)
                   && registration.Pool.TryRent(args, out lease);
        }

        public IPool GetPool(PoolConfig config)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(PoolFactory));
            if (!initialized)
                throw new InvalidOperationException("Initialize the PoolFactory before querying a pool.");
            if (config == null || !registrations.TryGetValue(config, out Registration registration))
                throw new ArgumentException("The PoolConfig is not registered in this container.", nameof(config));
            return registration.Pool;
        }

        public int CollectIdle()
        {
            if (disposed || !initialized || collecting)
                return 0;

            collecting = true;
            int removed = 0;
            Exception failure = null;
            try
            {
                for (int i = 0; i < ordered.Length && !disposed; i++)
                {
                    try { removed += ordered[i].Pool.TrimIdle(retainMinimum); }
                    catch (Exception exception) { failure ??= exception; }
                }
            }
            finally
            {
                collecting = false;
            }

            if (failure != null)
                throw failure;
            return removed;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            maintenanceCancellation?.Cancel();
            maintenanceCancellation?.Dispose();
            maintenanceCancellation = null;

            Exception failure = null;
            for (int i = ordered.Length - 1; i >= 0; i--)
            {
                Registration registration = ordered[i];
                if (registration == null)
                    continue;
                try { registration.Pool?.Dispose(); }
                catch (Exception exception) { failure ??= exception; }
                if (registration.Root != null)
                    UnityEngine.Object.Destroy(registration.Root.gameObject);
            }
            registrations.Clear();
            if (failure != null)
                throw failure;
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
