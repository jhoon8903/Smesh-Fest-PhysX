using System;
using UnityEngine;

namespace Framework.Pool
{
    /// <summary>
    /// Caches and runs the lifecycle parts for one pooled view without imposing a base class.
    /// Parts run first, followed by the owning view. Cleanup continues after individual failures.
    /// </summary>
    internal sealed class PoolLifecycleRunner
    {
        private readonly IPoolable owner;
        private readonly GameObject poolObject;
        private IPoolLifecycle[] lifecycleParts = Array.Empty<IPoolLifecycle>();
        private bool lifecyclePartsDiscovered;

        internal PoolLifecycleRunner(IPoolable owner)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            poolObject = owner.PoolObject;
            if (poolObject == null)
                throw new InvalidOperationException("IPoolable requires a live PoolObject.");
        }

        internal GameObject PoolObject => poolObject;
        internal Transform Transform => poolObject.transform;

        internal void CreatedByPool()
        {
            DiscoverLifecycleParts();
            InvokeCreated();
        }

        internal void RentFromPool(in PoolSpawnArgs args, PoolLease lease, Vector3 originalScale)
        {
            Transform target = Transform;
            target.SetParent(args.Parent, false);
            target.SetPositionAndRotation(args.Position, args.Rotation);
            target.localScale = originalScale;

            InvokeRent(lease);
            if (lease.IsValid)
                poolObject.SetActive(true);
        }

        internal void ReturnToPool(Transform activeRoot, Vector3 originalScale,
            Pool pool = null, uint returnVersion = 0)
        {
            InvokeReturn();
            // Scene teardown can destroy the hierarchy before its lifetime scope disposes the pool.
            // Managed cleanup still runs, but there is no Unity state left to reset.
            if (poolObject == null)
                return;
            if (pool != null && !pool.IsReturning(owner, returnVersion))
                return;

            Transform target = Transform;
            target.SetParent(activeRoot, false);
            target.localScale = originalScale;
            poolObject.SetActive(false);
        }

        internal void DestroyByPool()
        {
            DiscoverLifecycleParts();
            InvokeDestroy();
        }

        private void DiscoverLifecycleParts()
        {
            if (lifecyclePartsDiscovered)
                return;

            lifecyclePartsDiscovered = true;
            if (poolObject == null)
                return;

            MonoBehaviour[] behaviours = poolObject.GetComponentsInChildren<MonoBehaviour>(true);
            int count = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolLifecycle lifecycle && !ReferenceEquals(lifecycle, owner))
                    count++;
            }

            if (count == 0)
                return;

            lifecycleParts = new IPoolLifecycle[count];
            int index = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolLifecycle lifecycle && !ReferenceEquals(lifecycle, owner))
                    lifecycleParts[index++] = lifecycle;
            }
        }

        private void InvokeCreated()
        {
            Exception failure = null;
            for (int i = 0; i < lifecycleParts.Length; i++)
            {
                try { lifecycleParts[i].OnPoolCreated(owner); }
                catch (Exception exception) { failure ??= exception; }
            }
            try { owner.OnPoolCreated(owner); }
            catch (Exception exception) { failure ??= exception; }
            if (failure != null)
                throw failure;
        }

        private void InvokeRent(PoolLease lease)
        {
            Exception failure = null;
            for (int i = 0; i < lifecycleParts.Length; i++)
            {
                try { lifecycleParts[i].OnPoolRent(lease); }
                catch (Exception exception) { failure ??= exception; }
                if (!lease.IsValid && !lease.ContinueRentCallbacks)
                    break;
            }
            if (lease.IsValid || lease.ContinueRentCallbacks)
            {
                try { owner.OnPoolRent(lease); }
                catch (Exception exception) { failure ??= exception; }
            }
            if (failure != null)
                throw failure;
        }

        private void InvokeReturn()
        {
            Exception failure = null;
            for (int i = 0; i < lifecycleParts.Length; i++)
            {
                try { lifecycleParts[i].OnPoolReturn(); }
                catch (Exception exception) { failure ??= exception; }
            }
            try { owner.OnPoolReturn(); }
            catch (Exception exception) { failure ??= exception; }
            if (failure != null)
                throw failure;
        }

        private void InvokeDestroy()
        {
            Exception failure = null;
            for (int i = 0; i < lifecycleParts.Length; i++)
            {
                try { lifecycleParts[i].OnPoolDestroy(); }
                catch (Exception exception) { failure ??= exception; }
            }
            try { owner.OnPoolDestroy(); }
            catch (Exception exception) { failure ??= exception; }
            if (failure != null)
                throw failure;
        }
    }
}
