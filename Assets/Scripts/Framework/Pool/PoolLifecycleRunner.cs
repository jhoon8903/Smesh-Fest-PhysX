using System;
using UnityEngine;

namespace Framework.Pool
{
    internal sealed class PoolLifecycleRunner
    {
        private readonly IPoolable _owner;
        private IPoolLifecycle[] _lifecycleParts = Array.Empty<IPoolLifecycle>();
        private bool _lifecyclePartsDiscovered;

        internal PoolLifecycleRunner(IPoolable owner)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            PoolObject = owner.PoolObject;
            if (PoolObject == null) throw new InvalidOperationException("IPoolable requires a live PoolObject.");
        }

        internal GameObject PoolObject { get; }

        internal Transform Transform => PoolObject.transform;

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
            if (lease.IsValid) PoolObject.SetActive(true);
        }

        internal void ReturnToPool(Transform activeRoot, Vector3 originalScale, Pool pool = null, uint returnVersion = 0)
        {
            InvokeReturn();
            if (PoolObject == null) return;
            if (pool != null && !pool.IsReturning(_owner, returnVersion)) return;
            Transform target = Transform;
            target.SetParent(activeRoot, false);
            target.localScale = originalScale;
            PoolObject.SetActive(false);
        }

        internal void DestroyByPool()
        {
            DiscoverLifecycleParts();
            InvokeDestroy();
        }

        private void DiscoverLifecycleParts()
        {
            if (_lifecyclePartsDiscovered) return;

            _lifecyclePartsDiscovered = true;
            if (PoolObject == null) return;

            MonoBehaviour[] behaviours = PoolObject.GetComponentsInChildren<MonoBehaviour>(true);
            int count = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolLifecycle lifecycle && !ReferenceEquals(lifecycle, _owner)) count++;
            }
            if (count == 0) return;
            _lifecycleParts = new IPoolLifecycle[count];
            int index = 0;
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPoolLifecycle lifecycle && !ReferenceEquals(lifecycle, _owner)) _lifecycleParts[index++] = lifecycle;
            }
        }

        private void InvokeCreated()
        {
            Exception failure = null;
            for (int i = 0; i < _lifecycleParts.Length; i++)
            {
                try { _lifecycleParts[i].OnPoolCreated(_owner); }
                catch (Exception exception) { failure ??= exception; }
            }
            try { _owner.OnPoolCreated(_owner); }
            catch (Exception exception) { failure ??= exception; }
            if (failure != null) throw failure;
        }

        private void InvokeRent(PoolLease lease)
        {
            Exception failure = null;
            for (int i = 0; i < _lifecycleParts.Length; i++)
            {
                try { _lifecycleParts[i].OnPoolRent(lease); }
                catch (Exception exception) { failure ??= exception; }
                if (!lease.IsValid && !lease.ContinueRentCallbacks) break;
            }
            if (lease.IsValid || lease.ContinueRentCallbacks)
            {
                try { _owner.OnPoolRent(lease); }
                catch (Exception exception) { failure ??= exception; }
            }
            if (failure != null) throw failure;
        }

        private void InvokeReturn()
        {
            Exception failure = null;
            for (int i = 0; i < _lifecycleParts.Length; i++)
            {
                try { _lifecycleParts[i].OnPoolReturn(); }
                catch (Exception exception) { failure ??= exception; }
            }
            try { _owner.OnPoolReturn(); }
            catch (Exception exception) { failure ??= exception; }
            if (failure != null) throw failure;
        }

        private void InvokeDestroy()
        {
            Exception failure = null;
            for (int i = 0; i < _lifecycleParts.Length; i++)
            {
                try { _lifecycleParts[i].OnPoolDestroy(); }
                catch (Exception exception) { failure ??= exception; }
            }
            try { _owner.OnPoolDestroy(); }
            catch (Exception exception) { failure ??= exception; }
            if (failure != null) throw failure;
        }
    }
}
