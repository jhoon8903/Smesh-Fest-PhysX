#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using DG.Tweening;
using Framework.Object;
using Framework.Pool;
using UnityEngine;
using VContainer;
using PoolCore = Framework.Pool.Pool;

namespace Framework.Test
{
    /// <summary>Explicit, transient Play Mode checks for the Pool and PoolFactory contracts.</summary>
    public sealed class PoolRuntimeProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string name;
            public string recordedAtUtc;
            public string unityVersion;
            public bool success;
            public int assertions;
            public string error;
            public bool allocationMeasured;
            public long allocationBytes;
            public string allocationCondition;
        }

        public sealed class Dependency { }

        public sealed class ProbeSignal
        {
            private Action listeners;
            public int SubscriberCount { get; private set; }

            public void Subscribe(Action listener) { listeners += listener; SubscriberCount++; }
            public void Unsubscribe(Action listener) { listeners -= listener; SubscriberCount--; }
            public void Publish() => listeners?.Invoke();
        }

        public sealed class ProbePoolable : ObView, IPoolable
        {
            public GameObject PoolObject => gameObject;
            public bool InjectedBeforeEnable { get; private set; }
            public bool RentInitializedBeforeEnable { get; private set; }
            public int InjectCount { get; private set; }
            public int RentCount { get; private set; }
            public int EnableCount { get; private set; }
            public ProbeSignal Signal { get; } = new ProbeSignal();

            [Inject, UnityEngine.Scripting.Preserve]
            private void Construct(Dependency dependency)
            {
                InjectCount++;
                InjectedBeforeEnable = dependency != null;
            }

            private void OnEnable()
            {
                EnableCount++;
                if (InjectedBeforeEnable && RentCount == EnableCount)
                    RentInitializedBeforeEnable = true;
            }

            public void OnPoolCreated(IPoolable owner) { }
            public void OnPoolRent(PoolLease lease) { RentCount++; }
            public void OnPoolReturn() { }
            public void OnPoolDestroy() { }
        }

        public sealed class EmptyPoolable : ObView, IPoolable
        {
            public GameObject PoolObject => gameObject;
            public void OnPoolCreated(IPoolable owner) { }
            public void OnPoolRent(PoolLease lease) { }
            public void OnPoolReturn() { }
            public void OnPoolDestroy() { }
        }

        /// <summary>Owns real resources so cleanup assertions inspect state instead of setting a pass flag.</summary>
        public sealed class CleanupPart : MonoBehaviour, IPoolLifecycle
        {
            private ProbePoolable owner;
            private Tween tween;
            private ParticleSystem particle;
            private CancellationTokenSource cancellation;
            private CancellationToken capturedToken;
            private PoolLease capturedLease;
            private Action subscription;

            public bool LastTweenKilled { get; private set; }
            public int LastParticleCount { get; private set; } = -1;
            public bool LastTokenCanceled { get; private set; }
            public int LastSubscriptionCount { get; private set; } = -1;
            public bool OldLeaseInvalidOnTweenKill { get; private set; }
            public int ReturnCount { get; private set; }
            public int DestroyCount { get; private set; }
            public int ParticleCount => particle != null ? particle.particleCount : 0;
            public bool ReturnedClean => LastTweenKilled && LastParticleCount == 0 && LastTokenCanceled &&
                                         LastSubscriptionCount == 0 && OldLeaseInvalidOnTweenKill;

            public void OnPoolCreated(IPoolable poolOwner)
            {
                owner = poolOwner as ProbePoolable;
                particle = GetComponent<ParticleSystem>();
            }

            public void OnPoolRent(PoolLease lease)
            {
                LastTweenKilled = false;
                LastParticleCount = -1;
                LastTokenCanceled = false;
                LastSubscriptionCount = -1;
                OldLeaseInvalidOnTweenKill = false;
                capturedLease = lease;
                cancellation = new CancellationTokenSource();
                capturedToken = cancellation.Token;
                subscription = OnSignal;
                owner.Signal.Subscribe(subscription);
                tween = DOTween.To(() => transform.localPosition.x,
                        value => transform.localPosition = new Vector3(value, 0f, 0f), 10f, 10f)
                    .SetUpdate(false)
                    .OnKill(() => OldLeaseInvalidOnTweenKill = !capturedLease.IsValid);
                particle.Play();
                particle.Emit(1);
            }

            public void OnPoolReturn() { ReturnCount++; Clean(); }
            public void OnPoolDestroy() { DestroyCount++; Clean(); }
            public void EnsureParticleEmitted()
            {
                if (particle != null && particle.particleCount == 0) particle.Emit(1);
            }
            private void OnSignal() { }

            private void Clean()
            {
                Tween ownedTween = tween;
                if (ownedTween != null && ownedTween.IsActive()) ownedTween.Kill(false);
                LastTweenKilled = ownedTween == null || !ownedTween.IsActive();
                tween = null;
                if (particle != null) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                LastParticleCount = particle != null ? particle.particleCount : 0;
                cancellation?.Cancel();
                LastTokenCanceled = !capturedToken.CanBeCanceled || capturedToken.IsCancellationRequested;
                cancellation?.Dispose();
                cancellation = null;
                if (subscription != null && owner != null) owner.Signal.Unsubscribe(subscription);
                subscription = null;
                LastSubscriptionCount = owner != null ? owner.Signal.SubscriberCount : 0;
            }
        }

        private static class ScenarioHooks
        {
            public static Action<ScenarioPart, IPoolable> Created;
            public static Action<ScenarioPart, PoolLease> Rent;
            public static Action<ScenarioPart> Returned;
            public static Action<ScenarioPart> Destroyed;
            public static Action<ScenarioPoolable, IPoolable> OwnerCreated;
            public static Action<ScenarioPoolable, PoolLease> OwnerRent;
            public static Action<ScenarioPoolable> OwnerReturned;
            public static Action<ScenarioPoolable> OwnerDestroyed;
            public static Action<ScenarioPoolable> Enable;

            public static void Reset()
            {
                Created = null; Rent = null; Returned = null; Destroyed = null;
                OwnerCreated = null; OwnerRent = null; OwnerReturned = null; OwnerDestroyed = null;
                Enable = null;
            }
        }

        public sealed class ScenarioPoolable : ObView, IPoolable
        {
            public GameObject PoolObject => gameObject;
            public PoolLease CurrentLease { get; private set; }
            public int EnableCount { get; private set; }
            public int OwnedState;
            public void OnPoolCreated(IPoolable owner) => ScenarioHooks.OwnerCreated?.Invoke(this, owner);
            public void OnPoolRent(PoolLease lease)
            {
                CurrentLease = lease;
                ScenarioHooks.OwnerRent?.Invoke(this, lease);
            }
            public void OnPoolReturn() => ScenarioHooks.OwnerReturned?.Invoke(this);
            public void OnPoolDestroy() => ScenarioHooks.OwnerDestroyed?.Invoke(this);
            private void OnEnable() { EnableCount++; ScenarioHooks.Enable?.Invoke(this); }
        }

        public sealed class ScenarioPart : MonoBehaviour, IPoolLifecycle
        {
            public int Marker;
            public int OwnedState;
            public void OnPoolCreated(IPoolable owner) => ScenarioHooks.Created?.Invoke(this, owner);
            public void OnPoolRent(PoolLease lease) => ScenarioHooks.Rent?.Invoke(this, lease);
            public void OnPoolReturn() => ScenarioHooks.Returned?.Invoke(this);
            public void OnPoolDestroy() => ScenarioHooks.Destroyed?.Invoke(this);
        }

        private sealed class TransientPoolFixture : IDisposable
        {
            private readonly GameObject rootObject;
            private readonly GameObject sourceObject;
            private bool disposed;
            public readonly PoolConfig Config;
            public readonly ScenarioPoolable Source;
            public ScenarioPoolable LastCreated { get; private set; }
            public PoolCore Pool { get; private set; }
            public double Clock;
            public bool ThrowFromCreate;
            public bool ThrowFromDestroy;
            public Action<IPoolable> BeforeDestroy;
            public int CreateCalls;
            public int DestroyCalls;

            public TransientPoolFixture(string name, int min, int max, float delay, int partCount = 1)
            {
                rootObject = new GameObject(name + "_Root");
                sourceObject = new GameObject(name + "_Source");
                sourceObject.SetActive(false);
                Source = sourceObject.AddComponent<ScenarioPoolable>();
                for (int i = 0; i < partCount; i++)
                {
                    ScenarioPart part = sourceObject.AddComponent<ScenarioPart>();
                    part.Marker = i + 1;
                }
                Config = CreateRuntimeConfig(Source, min, max, delay, name + "_Pool");
                Pool = new PoolCore(Config, rootObject.transform, Create, Destroy, () => Clock);
            }

            private IPoolable Create()
            {
                CreateCalls++;
                if (ThrowFromCreate) throw new InvalidOperationException("expected create failure");
                LastCreated = Instantiate(Source, rootObject.transform, false);
                LastCreated.gameObject.SetActive(false);
                return LastCreated;
            }

            private void Destroy(IPoolable value)
            {
                DestroyCalls++;
                BeforeDestroy?.Invoke(value);
                if (value?.PoolObject != null) UnityEngine.Object.Destroy(value.PoolObject);
                if (ThrowFromDestroy) throw new InvalidOperationException("expected destroy failure");
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                Exception failure = null;
                try { Pool?.Dispose(); }
                catch (Exception exception) { failure = exception; }
                Pool = null;
                UnityEngine.Object.Destroy(rootObject);
                UnityEngine.Object.Destroy(sourceObject);
                UnityEngine.Object.Destroy(Config);
                if (failure != null) throw failure;
            }
        }

        private Result result;
        private PoolFactory factory;
        private PoolFactory maintenanceFactory;
        private IObjectResolver resolver;
        private PoolContainer container;
        private PoolContainer maintenanceContainer;
        private PoolConfig config;
        private PoolConfig maintenanceConfig;
        private MonoBehaviour sourcePrefab;
        private PoolFactory sceneScopedFactory;
        private string outputPath;
        private float maintenancePreviousScale;
        private bool maintenanceScaleChanged;
        private bool completed;
        private readonly List<IDisposable> ownedFixtures = new List<IDisposable>();

        public void Begin(PoolContainer poolContainer, PoolConfig poolConfig,
            PoolContainer maintenancePoolContainer, PoolConfig maintenancePoolConfig,
            PoolFactory scopedFactory, string path)
        {
            container = poolContainer;
            config = poolConfig;
            maintenanceContainer = maintenancePoolContainer;
            maintenanceConfig = maintenancePoolConfig;
            sceneScopedFactory = scopedFactory;
            sourcePrefab = poolConfig != null ? poolConfig.Prefab : null;
            outputPath = path;
            StartCoroutine(GuardedRun());
        }

        private IEnumerator GuardedRun()
        {
            IEnumerator routine = Run();
            while (true)
            {
                object current;
                bool moved;
                try { moved = routine.MoveNext(); current = moved ? routine.Current : null; }
                catch (Exception exception) { Complete(false, exception.ToString()); yield break; }
                if (!moved) break;
                yield return current;
            }
            Complete(true, null);
        }

        private IEnumerator Run()
        {
            result = NewResult();
            Assert(container != null && config != null && maintenanceContainer != null && maintenanceConfig != null,
                "Transient pool inputs are required.");
            Assert(sceneScopedFactory != null, "The active GameLifetimeScope did not resolve PoolFactory.");
            bool scopedFactoryInitialized = false;
            try { sceneScopedFactory.GetPool(null); }
            catch (ArgumentException) { scopedFactoryInitialized = true; }
            Assert(scopedFactoryInitialized, "The scene-scoped PoolFactory build callback did not initialize the factory.");
            RunStandaloneObViewContractCheck();

            var builder = new ContainerBuilder();
            builder.Register<Dependency>(Lifetime.Singleton);
            resolver = builder.Build();
            double clock = 0d;
            bool originalSourceActiveSelf = sourcePrefab != null && sourcePrefab.gameObject.activeSelf;
            factory = new PoolFactory(container, resolver, true, () => clock);
            PoolSpawnArgs args = new PoolSpawnArgs(new Vector3(2f, 0f, 0f), Quaternion.identity);
            Assert(!factory.TryRent(config, args, out _), "TryRent succeeded before Initialize.");
            factory.Initialize(false);
            IPool pool = factory.GetPool(config);
            Assert(pool.CountAll == config.MinPool && pool.CountInactive == config.MinPool,
                "MinPool prewarm is incorrect.");
            Assert(sourcePrefab != null && sourcePrefab.gameObject.activeSelf == originalSourceActiveSelf,
                "Initialization changed source prefab activeSelf.");

            Assert(factory.TryRent(config, args, out PoolLease first), "First rental failed.");
            ProbePoolable firstValue = first.Value as ProbePoolable;
            CleanupPart cleanup = firstValue != null ? firstValue.GetComponent<CleanupPart>() : null;
            Assert(firstValue != null && firstValue.InjectedBeforeEnable && firstValue.InjectCount == 1 &&
                   firstValue.RentCount == 1 && firstValue.EnableCount == 1 && firstValue.RentInitializedBeforeEnable &&
                   firstValue.transform.parent != null && firstValue.transform.position == args.Position,
                "DI, rent-before-enable order, root placement, or spawn transform is incorrect.");
            Assert(cleanup != null && firstValue.Signal.SubscriberCount == 1,
                "Cleanup fixture did not acquire its real subscription.");
            cleanup.EnsureParticleEmitted();
            Assert(cleanup.ParticleCount > 0, "Cleanup fixture did not contain a real particle before return.");
            Assert(first.Return(), "First return failed.");
            Assert(cleanup.ReturnedClean && cleanup.ReturnCount == 1,
                "Return left a tween, particle, token, subscription, or valid old lease.");
            Assert(!first.IsValid && first.Value == null && !first.Return(), "Returned lease remained usable.");
            Assert(factory.TryRent(config, args, out PoolLease second) && ReferenceEquals(second.Value, firstValue),
                "Inactive instance was not reused by identity.");
            Assert(firstValue.RentCount == 2 && firstValue.EnableCount == 2 && firstValue.RentInitializedBeforeEnable,
                "Re-rent initialization did not finish before OnEnable.");
            Assert(!first.Return() && second.Return(), "Old or current lease handling is incorrect after re-rent.");
            Assert(!default(PoolLease).IsValid && default(PoolLease).Value == null && !default(PoolLease).Return(),
                "Default lease was accepted.");

            PoolLease maxOne = default;
            PoolLease maxTwo = default;
            bool maxOneRented = factory.TryRent(config, args, out maxOne);
            bool maxTwoRented = factory.TryRent(config, args, out maxTwo);
            Assert(maxOneRented && maxTwoRented, "MaxPool fixtures did not rent.");
            Assert(!factory.TryRent(config, args, out _), "MaxPool limit was exceeded.");
            Assert(maxOne.Return() && maxTwo.Return(), "MaxPool fixtures did not return.");
            clock = config.ReturnDelaySeconds - 0.001d;
            Assert(factory.CollectIdle() == 0, "Idle inventory was removed before its exact time boundary.");
            clock = config.ReturnDelaySeconds;
            Assert(factory.CollectIdle() == 1 && pool.CountAll == config.MinPool && pool.CountInactive == config.MinPool,
                "Factory maintenance did not retain MinPool inactive inventory at the exact boundary.");
            Assert(sourcePrefab.gameObject.activeSelf == originalSourceActiveSelf,
                "Rent, return, or trim changed source prefab activeSelf.");

            RunReentryChecks(args);
            RunFailureChecks(args);
            RunIdlePolicyChecks(args);

            factory.Dispose();
            Assert(!factory.TryRent(config, args, out _), "Disposed factory rented an object.");
            bool disposedRejected = false;
            try { factory.GetPool(config); }
            catch (ObjectDisposedException) { disposedRejected = true; }
            Assert(disposedRejected, "Disposed factory GetPool did not fail.");

            maintenanceFactory = new PoolFactory(maintenanceContainer, resolver, true);
            maintenanceFactory.Initialize();
            Assert(maintenanceFactory.TryRent(maintenanceConfig, args, out PoolLease maintenanceOne) &&
                   maintenanceFactory.TryRent(maintenanceConfig, args, out PoolLease maintenanceTwo) &&
                   maintenanceOne.Return() && maintenanceTwo.Return(), "Maintenance fixture setup failed.");
            maintenancePreviousScale = Time.timeScale;
            try
            {
                maintenanceScaleChanged = true;
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(1.5f);
            }
            finally { RestoreTimeScale(); }
            IPool maintenancePool = maintenanceFactory.GetPool(maintenanceConfig);
            Assert(maintenancePool.CountAll == maintenanceConfig.MinPool &&
                   maintenancePool.CountInactive == maintenanceConfig.MinPool && maintenancePool.CountActive == 0,
                "Real-time maintenance at timeScale zero did not retain MinPool inactive inventory.");

            GameObject teardownContainerObject = new GameObject("__PoolValidation_HierarchyTeardownContainer");
            PoolContainer teardownContainer = teardownContainerObject.AddComponent<PoolContainer>();
            GameObject teardownPrefabObject = new GameObject("__PoolValidation_HierarchyTeardownPrefab");
            teardownPrefabObject.SetActive(false);
            EmptyPoolable teardownPrefab = teardownPrefabObject.AddComponent<EmptyPoolable>();
            PoolConfig teardownConfig = CreateRuntimeConfig(teardownPrefab, 1, 2, 0f,
                "__PoolValidation_HierarchyTeardownRoot");
            SetPrivateField(teardownContainer, "configs", new[] { teardownConfig });
            PoolFactory teardownFactory = new PoolFactory(teardownContainer, resolver, true, () => 0d);
            try
            {
                teardownFactory.Initialize(false);
                IPool teardownPool = teardownFactory.GetPool(teardownConfig);
                Assert(teardownFactory.TryRent(teardownConfig, args, out PoolLease teardownActive) &&
                       teardownFactory.TryRent(teardownConfig, args, out PoolLease teardownReturned) &&
                       teardownReturned.Return() && teardownPool.CountActive == 1 && teardownPool.CountInactive == 1,
                    "Hierarchy teardown fixture did not hold one active and one inactive instance.");
                Destroy(teardownContainerObject);
                yield return null;
                teardownFactory.Dispose();
                Assert(teardownPool.CountAll == 0 && teardownPool.CountActive == 0 && teardownPool.CountInactive == 0 &&
                       !teardownActive.IsValid,
                    "Pool disposal after native hierarchy teardown retained counts or an active lease.");
            }
            finally
            {
                TryDispose(teardownFactory, null);
                Destroy(teardownConfig);
                Destroy(teardownPrefabObject);
            }
            MeasureAllocation(args);
        }

        private void RunReentryChecks(PoolSpawnArgs args)
        {
            ScenarioHooks.Reset();
            TransientPoolFixture rentReturn = Own(new TransientPoolFixture("__PoolValidation_ReturnOnRent", 0, 1, 1f));
            bool callbackReturn = false;
            ScenarioHooks.Rent = (part, lease) => { callbackReturn = lease.Return(); part.OwnedState = 1; };
            ScenarioHooks.Returned = part => part.OwnedState = 0;
            bool outerRent = rentReturn.Pool.TryRent(args, out PoolLease escaped);
            Assert(callbackReturn && !outerRent && !escaped.IsValid && rentReturn.Pool.CountAll == 1 &&
                   rentReturn.Pool.CountActive == 0 && rentReturn.Pool.CountInactive == 1 &&
                   !rentReturn.LastCreated.gameObject.activeSelf &&
                   rentReturn.LastCreated.GetComponent<ScenarioPart>().OwnedState == 0,
                "OnPoolRent return exposed a stale lease or left work created after Return dirty.");
            Release(rentReturn);

            ScenarioHooks.Reset();
            TransientPoolFixture enableReturn = Own(new TransientPoolFixture("__PoolValidation_ReturnOnEnable", 0, 1, 1f));
            callbackReturn = false;
            ScenarioHooks.Enable = value => { callbackReturn = value.CurrentLease.Return(); value.OwnedState = 1; };
            ScenarioHooks.OwnerReturned = value => value.OwnedState = 0;
            outerRent = enableReturn.Pool.TryRent(args, out escaped);
            Assert(callbackReturn && !outerRent && !escaped.IsValid && enableReturn.Pool.CountAll == 1 &&
                   enableReturn.Pool.CountInactive == 1 && !enableReturn.LastCreated.gameObject.activeSelf &&
                   enableReturn.LastCreated.OwnedState == 0,
                "OnEnable return exposed a stale lease or left post-Return work dirty.");
            Release(enableReturn);

            ScenarioHooks.Reset();
            TransientPoolFixture disposeCreated = Own(new TransientPoolFixture("__PoolValidation_DisposeCreated", 0, 1, 1f, 2));
            int createdPasses = 0;
            ScenarioHooks.Created = (part, owner) => { createdPasses++; if (part.Marker == 1) disposeCreated.Pool.Dispose(); };
            ScenarioHooks.OwnerCreated = (value, owner) => createdPasses++;
            Assert(!disposeCreated.Pool.TryRent(args, out _) && createdPasses == 3 && disposeCreated.Pool.CountAll == 0,
                "Dispose during OnPoolCreated revived inventory or skipped callbacks.");
            Release(disposeCreated);

            ScenarioHooks.Reset();
            TransientPoolFixture disposeRent = Own(new TransientPoolFixture("__PoolValidation_DisposeRent", 1, 1, 1f, 2));
            disposeRent.Pool.Prewarm();
            int rentPasses = 0, rentReturns = 0, rentDestroys = 0;
            ScenarioHooks.Rent = (part, lease) => { rentPasses++; if (part.Marker == 1) disposeRent.Pool.Dispose(); };
            ScenarioHooks.OwnerRent = (value, lease) => rentPasses++;
            ScenarioHooks.Returned = part => rentReturns++;
            ScenarioHooks.OwnerReturned = value => rentReturns++;
            ScenarioHooks.Destroyed = part => rentDestroys++;
            ScenarioHooks.OwnerDestroyed = value => rentDestroys++;
            Assert(!disposeRent.Pool.TryRent(args, out _) && rentPasses == 3 && rentReturns == 3 && rentDestroys == 3 &&
                   disposeRent.Pool.CountAll == 0, "Dispose during OnPoolRent skipped cleanup or destroy callbacks.");
            Release(disposeRent);

            ScenarioHooks.Reset();
            TransientPoolFixture disposeReturn = Own(new TransientPoolFixture("__PoolValidation_DisposeReturn", 1, 1, 1f, 2));
            disposeReturn.Pool.Prewarm();
            Assert(disposeReturn.Pool.TryRent(args, out PoolLease returnLease), "Dispose-on-return fixture did not rent.");
            int returnPasses = 0, returnDestroys = 0;
            ScenarioHooks.Returned = part => { returnPasses++; if (part.Marker == 1) disposeReturn.Pool.Dispose(); };
            ScenarioHooks.OwnerReturned = value => returnPasses++;
            ScenarioHooks.Destroyed = part => returnDestroys++;
            ScenarioHooks.OwnerDestroyed = value => returnDestroys++;
            Assert(!returnLease.Return() && returnPasses == 3 && returnDestroys == 3 &&
                   disposeReturn.Pool.CountAll == 0 && disposeReturn.Pool.CountActive == 0 && disposeReturn.Pool.CountInactive == 0,
                "Dispose during OnPoolReturn restored inventory or skipped callbacks.");
            Release(disposeReturn);

            ScenarioHooks.Reset();
            TransientPoolFixture primary = Own(new TransientPoolFixture("__PoolValidation_PrimaryLease", 1, 1, 1f));
            TransientPoolFixture foreign = Own(new TransientPoolFixture("__PoolValidation_ForeignLease", 1, 1, 1f));
            primary.Pool.Prewarm(); foreign.Pool.Prewarm();
            PoolLease primaryLease = default;
            PoolLease foreignLease = default;
            bool primaryRented = primary.Pool.TryRent(args, out primaryLease);
            bool foreignRented = foreign.Pool.TryRent(args, out foreignLease);
            Assert(primaryRented && foreignRented, "Foreign lease fixtures did not rent.");
            Assert(foreignLease.Return() && primaryLease.IsValid && primary.Pool.CountActive == 1 &&
                   foreign.Pool.CountActive == 0 && foreign.Pool.CountInactive == 1,
                "A foreign lease changed the primary pool rental.");
            Assert(primaryLease.Return(), "Primary lease did not return after the foreign lease check.");
            Release(foreign); Release(primary); ScenarioHooks.Reset();
        }

        private void RunFailureChecks(PoolSpawnArgs args)
        {
            ScenarioHooks.Reset();
            TransientPoolFixture createFailure = Own(new TransientPoolFixture("__PoolValidation_CreateFailure", 0, 1, 1f));
            createFailure.ThrowFromCreate = true;
            Assert(ThrowsInvalidOperation(() => createFailure.Pool.TryRent(args, out _)) && createFailure.Pool.CountAll == 0 &&
                   createFailure.Pool.CountActive == 0 && createFailure.Pool.CountInactive == 0,
                "Throwing create delegate changed counts.");
            Release(createFailure);

            ScenarioHooks.Reset();
            TransientPoolFixture createdFailure = Own(new TransientPoolFixture("__PoolValidation_CreatedFailure", 0, 1, 1f, 2));
            int created = 0, createdDestroyed = 0;
            ScenarioHooks.Created = (part, owner) => { created++; if (part.Marker == 1) throw new InvalidOperationException("expected create lifecycle failure"); };
            ScenarioHooks.OwnerCreated = (value, owner) => created++;
            ScenarioHooks.Destroyed = part => createdDestroyed++;
            ScenarioHooks.OwnerDestroyed = value => createdDestroyed++;
            Assert(ThrowsInvalidOperation(() => createdFailure.Pool.TryRent(args, out _)) && created == 3 &&
                   createdDestroyed == 3 && createdFailure.DestroyCalls == 1 && createdFailure.Pool.CountAll == 0,
                "OnPoolCreated failure skipped callbacks or retained the instance.");
            Release(createdFailure);

            ScenarioHooks.Reset();
            TransientPoolFixture rentFailure = Own(new TransientPoolFixture("__PoolValidation_RentFailure", 1, 1, 1f, 2));
            rentFailure.Pool.Prewarm();
            int rented = 0, rentDestroyed = 0;
            ScenarioHooks.Rent = (part, lease) => { rented++; if (part.Marker == 1) throw new InvalidOperationException("expected rent failure"); };
            ScenarioHooks.OwnerRent = (value, lease) => rented++;
            ScenarioHooks.Destroyed = part => rentDestroyed++;
            ScenarioHooks.OwnerDestroyed = value => rentDestroyed++;
            Assert(ThrowsInvalidOperation(() => rentFailure.Pool.TryRent(args, out _)) && rented == 3 && rentDestroyed == 3 &&
                   rentFailure.Pool.CountAll == 0 && rentFailure.Pool.CountActive == 0 && rentFailure.Pool.CountInactive == 0,
                "OnPoolRent failure skipped callbacks or failed to quarantine.");
            Release(rentFailure);

            ScenarioHooks.Reset();
            TransientPoolFixture returnFailure = Own(new TransientPoolFixture("__PoolValidation_ReturnFailure", 1, 1, 1f, 2));
            returnFailure.Pool.Prewarm();
            Assert(returnFailure.Pool.TryRent(args, out PoolLease failingReturn), "Return failure fixture did not rent.");
            int returned = 0, returnDestroyed = 0;
            ScenarioHooks.Returned = part => { returned++; if (part.Marker == 1) throw new InvalidOperationException("expected return failure"); };
            ScenarioHooks.OwnerReturned = value => returned++;
            ScenarioHooks.Destroyed = part => returnDestroyed++;
            ScenarioHooks.OwnerDestroyed = value => returnDestroyed++;
            Assert(ThrowsInvalidOperation(() => failingReturn.Return()) && returned == 3 && returnDestroyed == 3 &&
                   returnFailure.Pool.CountAll == 0 && returnFailure.Pool.CountActive == 0 && returnFailure.Pool.CountInactive == 0,
                "OnPoolReturn failure skipped callbacks or failed to quarantine.");
            Release(returnFailure);

            ScenarioHooks.Reset();
            TransientPoolFixture destroyFailure = Own(new TransientPoolFixture("__PoolValidation_DestroyFailure", 0, 2, 1f, 2));
            Assert(destroyFailure.Pool.TryRent(args, out PoolLease destroyOne) &&
                   destroyFailure.Pool.TryRent(args, out PoolLease destroyTwo) && destroyOne.Return() && destroyTwo.Return(),
                "Destroy failure fixture setup failed.");
            int destroyPasses = 0;
            ScenarioHooks.Destroyed = part => { destroyPasses++; if (part.Marker == 1) throw new InvalidOperationException("expected destroy failure"); };
            ScenarioHooks.OwnerDestroyed = value => destroyPasses++;
            destroyFailure.ThrowFromDestroy = true;
            Assert(ThrowsInvalidOperation(destroyFailure.Pool.Dispose) && destroyPasses == 6 && destroyFailure.DestroyCalls == 2 &&
                   destroyFailure.Pool.CountAll == 0 && destroyFailure.Pool.CountInactive == 0,
                "Dispose stopped after one failure or retained failed instances.");
            destroyFailure.ThrowFromDestroy = false;
            Release(destroyFailure); ScenarioHooks.Reset();
        }

        private void RunIdlePolicyChecks(PoolSpawnArgs args)
        {
            ScenarioHooks.Reset();
            TransientPoolFixture idle = Own(new TransientPoolFixture("__PoolValidation_IdlePolicy", 2, 6, 10f));
            idle.Pool.Prewarm();
            PoolLease[] leases = new PoolLease[6];
            for (int i = 0; i < leases.Length; i++)
                Assert(idle.Pool.TryRent(args, out leases[i]), "Idle policy fixture could not fill MaxPool.");
            Assert(leases[3].Return() && leases[4].Return() && leases[5].Return(),
                "Idle policy fixture did not produce three inactive entries.");
            idle.Clock = 9.999d;
            Assert(idle.Pool.TrimIdle(true) == 0, "TrimIdle removed inventory before the fake-time boundary.");
            idle.Clock = 10d;
            Assert(idle.Pool.TrimIdle(true) == 1 && idle.Pool.CountActive == 3 && idle.Pool.CountInactive == 2 &&
                   idle.Pool.CountAll == 5 && leases[0].IsValid && leases[1].IsValid && leases[2].IsValid,
                "TrimIdle(true) did not retain MinPool inactive inventory or changed active rentals.");
            Assert(idle.Pool.TrimIdle(false) == 2 && idle.Pool.CountActive == 3 && idle.Pool.CountInactive == 0 &&
                   idle.Pool.CountAll == 3 && leases[0].IsValid && leases[1].IsValid && leases[2].IsValid,
                "TrimIdle(false) removed active rentals or retained expired inactive inventory.");
            Assert(leases[0].Return() && leases[1].Return() && leases[2].Return(),
                "Active rentals were not returnable after trimming.");
            Release(idle);

            ScenarioHooks.Reset();
            TransientPoolFixture disabled = Own(new TransientPoolFixture("__PoolValidation_TrimDisabled", 1, 2, 0f));
            disabled.Pool.Prewarm();
            Assert(disabled.Pool.TryRent(args, out PoolLease disabledOne) &&
                   disabled.Pool.TryRent(args, out PoolLease disabledTwo) && disabledOne.Return() && disabledTwo.Return(),
                "Disabled trim fixture setup failed.");
            disabled.Clock = 100000d;
            Assert(disabled.Pool.TrimIdle(true) == 0 && disabled.Pool.TrimIdle(false) == 0 && disabled.Pool.CountInactive == 2,
                "ReturnDelaySeconds zero did not disable both trim policies.");
            Release(disabled);

            ScenarioHooks.Reset();
            TransientPoolFixture reentry = Own(new TransientPoolFixture("__PoolValidation_TrimReentry", 0, 2, 1f));
            Assert(reentry.Pool.TryRent(args, out PoolLease reentryOne) && reentry.Pool.TryRent(args, out PoolLease reentryTwo) &&
                   reentryOne.Return() && reentryTwo.Return(), "Trim reentry fixture setup failed.");
            reentry.Clock = 1d;
            int nestedTrim = -1, destroyPasses = 0;
            ScenarioHooks.Destroyed = part =>
            {
                destroyPasses++;
                nestedTrim = reentry.Pool.TrimIdle(false);
                reentry.Pool.Prewarm();
                Assert(!reentry.Pool.TryRent(args, out _), "TryRent re-entered during TrimIdle destroy.");
            };
            int outerTrim = reentry.Pool.TrimIdle(false);
            Assert(outerTrim == 2 && nestedTrim == 0 && destroyPasses == 2 && reentry.Pool.CountAll == 0,
                "TrimIdle reentry mutated inventory or skipped an expired entry.");
            Release(reentry); ScenarioHooks.Reset();
        }

        private void MeasureAllocation(PoolSpawnArgs args)
        {
            GameObject containerObject = new GameObject("__PoolValidation_AllocationContainer");
            PoolContainer allocationContainer = containerObject.AddComponent<PoolContainer>();
            GameObject prefabObject = new GameObject("__PoolValidation_AllocationPrefab");
            prefabObject.SetActive(false);
            EmptyPoolable allocationPrefab = prefabObject.AddComponent<EmptyPoolable>();
            PoolConfig allocationConfig = CreateRuntimeConfig(allocationPrefab, 1, 1, 0f, "__PoolValidation_AllocationRoot");
            SetPrivateField(allocationContainer, "configs", new[] { allocationConfig });
            var allocationFactory = new PoolFactory(allocationContainer, resolver, true, () => 0d);
            try
            {
                allocationFactory.Initialize(false);
                bool loopSucceeded = true;
                for (int i = 0; i < 100; i++)
                {
                    bool rented = allocationFactory.TryRent(allocationConfig, args, out PoolLease lease);
                    loopSucceeded &= rented && lease.Return();
                }
                Assert(loopSucceeded, "Allocation warmup failed.");
                MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",
                    BindingFlags.Public | BindingFlags.Static);
                result.allocationCondition = "100 warmup, 1000 cached rent/return, empty IPoolable without effects, tokens, child lifecycle parts, or assertions inside the measured interval";
                if (method == null) return;
                Func<long> read = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
                long before = read();
                loopSucceeded = true;
                for (int i = 0; i < 1000; i++)
                {
                    bool rented = allocationFactory.TryRent(allocationConfig, args, out PoolLease lease);
                    loopSucceeded &= rented && lease.Return();
                }
                long after = read();
                result.allocationMeasured = true;
                result.allocationBytes = after - before;
                Assert(loopSucceeded, "Allocation measurement rent/return failed.");
                Assert(result.allocationBytes == 0, "Stable rent/return allocated " + result.allocationBytes + " bytes.");
            }
            finally
            {
                TryDispose(allocationFactory, null);
                Destroy(containerObject); Destroy(allocationConfig); Destroy(prefabObject);
            }
        }

        private static PoolConfig CreateRuntimeConfig(MonoBehaviour prefab, int min, int max, float delay, string rootName)
        {
            PoolConfig created = ScriptableObject.CreateInstance<PoolConfig>();
            SetPrivateField(created, "poolRootName", rootName);
            SetPrivateField(created, "prefab", prefab);
            SetPrivateField(created, "minPool", min);
            SetPrivateField(created, "maxPool", max);
            SetPrivateField(created, "returnDelaySeconds", delay);
            return created;
        }

        private void RunStandaloneObViewContractCheck()
        {
            GameObject source = new GameObject("__PoolValidation_StandaloneObView");
            source.SetActive(false);
            ObView standalone = source.AddComponent<ObView>();
            PoolConfig standaloneConfig = CreateRuntimeConfig(standalone, 0, 1, 1f,
                "__PoolValidation_StandaloneObViewPool");
            bool rejected = false;
            try { standaloneConfig.Validate(); }
            catch (InvalidOperationException) { rejected = true; }
            finally
            {
                Destroy(source);
                Destroy(standaloneConfig);
            }
            Assert(rejected && !(standalone is IPoolable),
                "Standalone ObView was forced into or accepted by the pooling contract.");
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, name);
            field.SetValue(target, value);
        }

        private T Own<T>(T fixture) where T : IDisposable { ownedFixtures.Add(fixture); return fixture; }
        private void Release(IDisposable fixture) { ownedFixtures.Remove(fixture); fixture.Dispose(); }
        private static bool ThrowsInvalidOperation(Action action)
        {
            try { action(); }
            catch (InvalidOperationException) { return true; }
            return false;
        }

        private Result NewResult() => new Result
        {
            name = "Pool Runtime Validation",
            recordedAtUtc = DateTime.UtcNow.ToString("O"),
            unityVersion = Application.unityVersion
        };

        private void Assert(bool condition, string message)
        {
            result.assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Complete(bool success, string error)
        {
            if (completed) return;
            completed = true;
            if (result == null) result = NewResult();
            result.success = success;
            result.error = error;
            RestoreTimeScale();
            TryDispose(factory, "transient factory");
            TryDispose(maintenanceFactory, "maintenance factory");
            for (int i = ownedFixtures.Count - 1; i >= 0; i--)
                TryDispose(ownedFixtures[i], "direct pool fixture " + i);
            ownedFixtures.Clear();
            TryDispose(resolver, "transient resolver");
            ScenarioHooks.Reset();
            if (container != null) Destroy(container.gameObject);
            if (maintenanceContainer != null) Destroy(maintenanceContainer.gameObject);
            if (config != null) Destroy(config);
            if (maintenanceConfig != null) Destroy(maintenanceConfig);
            if (sourcePrefab != null)
            {
                Transform parent = sourcePrefab.transform.parent;
                Destroy(parent != null ? parent.gameObject : sourcePrefab.gameObject);
            }
            Destroy(gameObject);
            if (!string.IsNullOrEmpty(outputPath))
            {
                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            }
        }

        private void TryDispose(IDisposable disposable, string label)
        {
            if (disposable == null) return;
            try { disposable.Dispose(); }
            catch (Exception exception)
            {
                result ??= NewResult();
                result.success = false;
                string cleanupError = string.IsNullOrEmpty(label) ? exception.ToString() : label + ": " + exception;
                result.error = string.IsNullOrEmpty(result.error) ? cleanupError : result.error + "\n" + cleanupError;
            }
        }

        private void RestoreTimeScale()
        {
            if (!maintenanceScaleChanged) return;
            Time.timeScale = maintenancePreviousScale;
            maintenanceScaleChanged = false;
        }

        private void OnDestroy()
        {
            RestoreTimeScale();
            if (completed) return;
            TryDispose(factory, null);
            TryDispose(maintenanceFactory, null);
            for (int i = ownedFixtures.Count - 1; i >= 0; i--) TryDispose(ownedFixtures[i], null);
            ownedFixtures.Clear();
            TryDispose(resolver, null);
            ScenarioHooks.Reset();
        }
    }
}
#endif
