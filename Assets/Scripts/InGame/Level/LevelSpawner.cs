using System;
using System.Runtime.ExceptionServices;
using Framework.Pool;
using UnityEngine;
using VContainer;

namespace InGame.Level
{
    /// <summary>
    /// Explicitly rents one saved LevelConfig. It does not infer a root, auto-spawn, or alter
    /// authoring obstacles; callers must invoke TrySpawn after those authoring objects are absent.
    /// </summary>
    public sealed class LevelSpawner : MonoBehaviour
    {
        [SerializeField] private LevelConfig levelConfig;
        [SerializeField] private Transform runtimeRoot;

        private PoolFactory poolFactory;
        private PoolLease[] leases = Array.Empty<PoolLease>();
        private int activeLeaseCount;

        public bool IsSpawned => activeLeaseCount > 0;
        public int ActiveLeaseCount => activeLeaseCount;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(PoolFactory injectedPoolFactory)
        {
            if (poolFactory != null)
                throw new InvalidOperationException("LevelSpawner was already configured.");
            poolFactory = injectedPoolFactory ?? throw new ArgumentNullException(nameof(injectedPoolFactory));
        }

        /// <summary>
        /// Rents all entries as one transaction. Invalid data, unavailable pools, or any transform
        /// failure returns false and leaves no leases owned by this spawner.
        /// </summary>
        public bool TrySpawn(out string failure)
        {
            failure = null;
            if (activeLeaseCount != 0)
            {
                failure = "LevelSpawner already owns active level leases. Call ReturnAll first.";
                return false;
            }

            try
            {
                ValidateReadyToSpawn();
                leases = new PoolLease[levelConfig.Count];
                for (int i = 0; i < leases.Length; i++)
                {
                    LevelConfig.Entry entry = levelConfig.GetEntry(i);
                    if (!poolFactory.TryRent(entry.PoolConfig,
                            new PoolSpawnArgs(runtimeRoot.TransformPoint(entry.LocalPosition),
                                runtimeRoot.rotation * entry.LocalRotation, runtimeRoot),
                            out PoolLease lease))
                        throw new InvalidOperationException($"LevelConfig entry {i} could not be rented from its registered pool.");

                    leases[activeLeaseCount++] = lease;
                    Transform spawned = lease.Value != null ? lease.Value.PoolObject.transform : null;
                    if (spawned == null)
                        throw new InvalidOperationException($"LevelConfig entry {i} returned no live pooled object.");

                    // Pool lifecycle establishes the parent first. Apply only the saved local pose after that.
                    spawned.localPosition = entry.LocalPosition;
                    spawned.localRotation = entry.LocalRotation;
                    spawned.localScale = entry.LocalScale;
                }

                return true;
            }
            catch (Exception exception)
            {
                Exception rollbackFailure = ReturnOwnedLeases();
                failure = rollbackFailure == null
                    ? exception.Message
                    : $"{exception.Message} Rollback also reported: {rollbackFailure.Message}";
                return false;
            }
        }

        /// <summary>Returns only the exact leases this spawner still owns; stale leases cannot affect re-rentals.</summary>
        public void ReturnAll()
        {
            Exception failure = ReturnOwnedLeases();
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void OnDestroy()
        {
            try { ReturnAll(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void ValidateReadyToSpawn()
        {
            if (poolFactory == null)
                throw new InvalidOperationException("LevelSpawner requires PoolFactory injection before spawning.");
            if (levelConfig == null)
                throw new InvalidOperationException("LevelSpawner requires a LevelConfig.");
            if (runtimeRoot == null)
                throw new InvalidOperationException("LevelSpawner requires an explicit runtime root.");
            levelConfig.Validate();
        }

        private Exception ReturnOwnedLeases()
        {
            Exception failure = null;
            for (int i = activeLeaseCount - 1; i >= 0; i--)
            {
                PoolLease lease = leases[i];
                leases[i] = default;
                try
                {
                    // Return is epoch-safe: false means another lifecycle already ended this rental.
                    lease.Return();
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }
            }

            activeLeaseCount = 0;
            leases = Array.Empty<PoolLease>();
            return failure;
        }
    }
}
