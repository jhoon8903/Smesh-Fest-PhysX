using System;
using System.Runtime.ExceptionServices;
using Framework.Pool;
using UnityEngine;
using VContainer;

namespace InGame.Level
{
    public sealed class LevelSpawner : MonoBehaviour
    {
        [SerializeField] private LevelConfig levelConfig;
        [SerializeField] private Transform runtimeRoot;

        private PoolFactory _poolFactory;
        private PoolLease[] _leases = Array.Empty<PoolLease>();

        public bool IsSpawned => ActiveLeaseCount > 0;
        public int ActiveLeaseCount { get; private set; }

        public Transform RuntimeRoot => runtimeRoot;

        [Inject, UnityEngine.Scripting.Preserve]
        private void Construct(PoolFactory injectedPoolFactory)
        {
            if (_poolFactory != null) throw new InvalidOperationException("LevelSpawner was already configured.");
            _poolFactory = injectedPoolFactory ?? throw new ArgumentNullException(nameof(injectedPoolFactory));
        }
        
        public bool TrySpawn(out string failure)
        {
            failure = null;
            if (ActiveLeaseCount != 0)
            {
                failure = "LevelSpawner already owns active level leases. Call ReturnAll first.";
                return false;
            }

            try
            {
                ValidateReadyToSpawn();
                _leases = new PoolLease[levelConfig.Count];
                for (int i = 0; i < _leases.Length; i++)
                {
                    LevelConfig.Entry entry = levelConfig.GetEntry(i);
                    if (!_poolFactory.TryRent(entry.PoolConfig, new PoolSpawnArgs(runtimeRoot.TransformPoint(entry.LocalPosition), runtimeRoot.rotation * entry.LocalRotation, runtimeRoot), out PoolLease lease)) throw new InvalidOperationException($"LevelConfig entry {i} could not be rented from its registered pool.");
                    _leases[ActiveLeaseCount++] = lease;
                    Transform spawned = lease.Value?.PoolObject.transform;
                    if (spawned == null) throw new InvalidOperationException($"LevelConfig entry {i} returned no live pooled object.");
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
        
        public void ReturnAll()
        {
            Exception failure = ReturnOwnedLeases();
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private void OnDestroy()
        {
            try { ReturnAll(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void ValidateReadyToSpawn()
        {
            if (_poolFactory == null)
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
            for (int i = ActiveLeaseCount - 1; i >= 0; i--)
            {
                PoolLease lease = _leases[i];
                _leases[i] = default;
                try
                {
                    lease.Return();
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }
            }

            ActiveLeaseCount = 0;
            _leases = Array.Empty<PoolLease>();
            return failure;
        }
    }
}
