using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Pool
{
    /// <summary>
    /// Bounded, scene-lifetime pool. The pool owns only instances returned by its create delegate.
    /// It never expires active rentals; callers choose whether idle trimming retains the configured
    /// minimum as inactive ready inventory.
    /// </summary>
    public sealed class Pool : IPool
    {
        private enum EntryState : byte
        {
            Initializing,
            Inactive,
            Renting,
            Active,
            Returning,
            ReturnPending,
            Destroying
        }

        private sealed class Entry
        {
            public IPoolable Value;
            public PoolLifecycleRunner Lifecycle;
            public Vector3 OriginalScale;
            public double IdleSince;
            public uint Version;
            public uint TrimEpoch;
            public EntryState State;
        }

        private readonly PoolConfig config;
        private readonly Transform activeRoot;
        private readonly Func<IPoolable> create;
        private readonly Action<IPoolable> destroy;
        private readonly Func<double> now;
        private readonly List<Entry> entries;
        private readonly List<int> inactiveIndices;
        private readonly Dictionary<IPoolable, int> entryIndices;

        private int activeCount;
        private int creatingCount;
        private int lifecycleDepth;
        private uint trimEpoch;
        private bool disposed;
        private bool disposing;
        private bool trimming;

        public Pool(PoolConfig config, Transform activeRoot, Func<IPoolable> create,
            Action<IPoolable> destroy, Func<double> now)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.activeRoot = activeRoot != null ? activeRoot : throw new ArgumentNullException(nameof(activeRoot));
            this.create = create ?? throw new ArgumentNullException(nameof(create));
            this.destroy = destroy ?? throw new ArgumentNullException(nameof(destroy));
            this.now = now ?? throw new ArgumentNullException(nameof(now));

            config.Validate();
            entries = new List<Entry>(config.MaxPool);
            inactiveIndices = new List<int>(config.MaxPool);
            entryIndices = new Dictionary<IPoolable, int>(config.MaxPool);
        }

        public int CountAll => entries.Count;
        public int CountActive => activeCount;
        public int CountInactive => inactiveIndices.Count;

        public void Prewarm()
        {
            ThrowIfDisposed();
            if (lifecycleDepth > 0)
                return;

            while (!disposed && entries.Count + creatingCount < config.MinPool)
            {
                if (CreateInactive() == null)
                    break;
            }
        }

        public bool TryRent(in PoolSpawnArgs args, out PoolLease lease)
        {
            lease = default;
            if (disposed || lifecycleDepth > 0)
                return false;

            Entry entry;
            if (inactiveIndices.Count > 0)
            {
                int idleListIndex = inactiveIndices.Count - 1;
                int selectedIndex = inactiveIndices[idleListIndex];
                inactiveIndices.RemoveAt(idleListIndex);
                entry = entries[selectedIndex];
            }
            else
            {
                if (entries.Count + creatingCount >= config.MaxPool)
                    return false;
                entry = CreateInactive();
                if (entry == null || !TryGetEntryIndex(entry, out int createdIndex))
                    return false;
                RemoveInactiveIndex(createdIndex);
            }

            entry.State = EntryState.Renting;
            activeCount++;
            entry.Version = NextVersion(entry.Version);
            uint rentalVersion = entry.Version;
            PoolLease candidate = new PoolLease(this, entry.Value, rentalVersion);
            Transform parent = args.Parent != null ? args.Parent : activeRoot;
            PoolSpawnArgs resolvedArgs = new PoolSpawnArgs(args.Position, args.Rotation, parent);

            Exception failure = null;
            BeginLifecycle();
            try
            {
                entry.Lifecycle.RentFromPool(resolvedArgs, candidate, entry.OriginalScale);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            failure = EndLifecycle(failure);

            if (failure != null)
            {
                try { Quarantine(entry); }
                catch { /* The rental preparation failure is the actionable error. */ }
                throw failure;
            }

            if (IsDestroyedUnityObject(entry.Value))
            {
                Quarantine(entry);
                return false;
            }

            if (!TryGetEntryIndex(entry, out int entryIndex))
                return false;

            // Returning from OnPoolRent is legal, but the object is not reusable until the
            // complete rent callback (including OnEnable) has unwound.
            if (entry.State == EntryState.ReturnPending)
            {
                CompletePendingReturn(entry, entryIndex);
                return false;
            }

            if (entry.State == EntryState.Renting && entry.Version == rentalVersion && !disposed)
            {
                entry.State = EntryState.Active;
                lease = candidate;
                return true;
            }

            return false;
        }

        public int TrimIdle(bool retainMinimum)
        {
            if (disposed || trimming || lifecycleDepth > 0 || config.ReturnDelaySeconds == 0f)
                return 0;

            double timestamp = now();
            if (disposed)
                return 0;

            int minimum = retainMinimum ? config.MinPool : 0;
            int removed = 0;
            Exception failure = null;
            uint epoch = trimEpoch = NextVersion(trimEpoch);
            for (int i = 0; i < inactiveIndices.Count; i++)
                entries[inactiveIndices[i]].TrimEpoch = epoch;

            trimming = true;
            try
            {
                while (!disposed && inactiveIndices.Count > minimum)
                {
                    int idleListIndex = FindTrimCandidate(epoch);
                    if (idleListIndex < 0)
                        break;

                    Entry entry = entries[inactiveIndices[idleListIndex]];
                    entry.TrimEpoch = 0;
                    if (timestamp - entry.IdleSince < config.ReturnDelaySeconds)
                        continue;

                    Entry detached = DetachInactiveAt(idleListIndex);
                    removed++;
                    try { DestroyDetached(detached); }
                    catch (Exception exception) { failure ??= exception; }
                }
            }
            finally
            {
                trimming = false;
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
            if (lifecycleDepth > 0 || disposing)
                return;

            CompleteDispose();
        }

        private void CompleteDispose()
        {
            if (disposing)
                return;

            disposing = true;
            Exception failure = null;
            try
            {
                activeCount = 0;
                inactiveIndices.Clear();
                while (entries.Count > 0)
                {
                    Entry detached = DetachAt(entries.Count - 1);
                    try { DestroyDetached(detached); }
                    catch (Exception exception) { failure ??= exception; }
                }
                entryIndices.Clear();
            }
            finally
            {
                disposing = false;
            }

            if (failure != null)
                throw failure;
        }

        internal bool IsCurrentRental(IPoolable value, uint version)
        {
            if (disposed || ReferenceEquals(value, null) || IsDestroyedUnityObject(value)
                || !entryIndices.TryGetValue(value, out int index))
                return false;

            Entry entry = entries[index];
            return ReferenceEquals(entry.Value, value)
                   && (entry.State == EntryState.Renting || entry.State == EntryState.Active)
                   && entry.Version == version;
        }

        internal bool ShouldContinueRentCallbacks(IPoolable value, uint version)
        {
            if (ReferenceEquals(value, null) || IsDestroyedUnityObject(value)
                || !entryIndices.TryGetValue(value, out int index))
                return false;

            Entry entry = entries[index];
            return ReferenceEquals(entry.Value, value)
                   && entry.State == EntryState.Renting
                   && entry.Version == version;
        }

        internal bool TryReturn(IPoolable value, uint version)
        {
            if (!IsCurrentRental(value, version))
                return false;

            int index = entryIndices[value];
            Entry entry = entries[index];
            if (entry.State == EntryState.Renting)
            {
                // Invalidate immediately, but defer cleanup until the current rent callback
                // finishes so work started after Return() in that callback is also cleaned.
                entry.State = EntryState.ReturnPending;
                entry.Version = NextVersion(entry.Version);
                activeCount--;
                return true;
            }

            entry.State = EntryState.Returning;
            entry.Version = NextVersion(entry.Version);
            uint returnVersion = entry.Version;
            activeCount--;

            Exception failure = null;
            BeginLifecycle();
            try
            {
                entry.Lifecycle.ReturnToPool(activeRoot, entry.OriginalScale, this, returnVersion);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            failure = EndLifecycle(failure);

            if (failure != null)
            {
                try { Quarantine(entry); }
                catch { /* Preserve the reset failure after removing the entry. */ }
                throw failure;
            }

            if (disposed || !TryGetEntryIndex(entry, out index))
                return false;

            if (entry.State != EntryState.Returning || entry.Version != returnVersion)
                return false;

            if (IsDestroyedUnityObject(entry.Value))
            {
                Quarantine(entry);
                return false;
            }

            try
            {
                FinalizeInactive(entry, index);
            }
            catch
            {
                try { Quarantine(entry); }
                catch { /* Preserve the clock failure. */ }
                throw;
            }
            return true;
        }

        private bool CompletePendingReturn(Entry entry, int index)
        {
            if (disposed || !TryGetEntryIndex(entry, out int currentIndex) || currentIndex != index
                || entry.State != EntryState.ReturnPending)
                return false;

            entry.State = EntryState.Returning;
            uint returnVersion = entry.Version;
            Exception failure = null;
            BeginLifecycle();
            try
            {
                entry.Lifecycle.ReturnToPool(activeRoot, entry.OriginalScale, this, returnVersion);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            failure = EndLifecycle(failure);

            if (failure != null)
            {
                try { Quarantine(entry); }
                catch { /* Preserve the deferred reset failure. */ }
                throw failure;
            }

            if (disposed || !TryGetEntryIndex(entry, out currentIndex)
                || entry.State != EntryState.Returning || entry.Version != returnVersion)
                return false;

            if (IsDestroyedUnityObject(entry.Value))
            {
                Quarantine(entry);
                return false;
            }

            try
            {
                FinalizeInactive(entry, currentIndex);
            }
            catch
            {
                try { Quarantine(entry); }
                catch { /* Preserve the clock failure. */ }
                throw;
            }
            return true;
        }

        private Entry CreateInactive()
        {
            IPoolable value = null;
            PoolLifecycleRunner lifecycle = null;
            Entry entry = null;
            bool tracked = false;
            bool destructionAttempted = false;
            Exception failure = null;

            creatingCount++;
            BeginLifecycle();
            try
            {
                value = create();
                if (ReferenceEquals(value, null))
                    throw new InvalidOperationException("Pool create delegate returned null.");
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            failure = EndLifecycle(failure);
            creatingCount--;

            if (failure != null)
            {
                if (!ReferenceEquals(value, null) && !entryIndices.ContainsKey(value))
                {
                    try { DestroyUntracked(value, new PoolLifecycleRunner(value)); }
                    catch { /* Preserve the creation/disposal failure. */ }
                }
                throw failure;
            }

            try
            {
                if (disposed)
                {
                    destructionAttempted = true;
                    DestroyUntracked(value, lifecycle ?? new PoolLifecycleRunner(value));
                    return null;
                }
                lifecycle = new PoolLifecycleRunner(value);
                if (lifecycle.PoolObject.activeSelf)
                    throw new InvalidOperationException("Pool create delegate must return an inactive IPoolable.");
                if (entryIndices.ContainsKey(value))
                    throw new InvalidOperationException("Pool create delegate returned an instance already owned by this pool.");

                lifecycle.Transform.SetParent(activeRoot, false);
                entry = new Entry
                {
                    Value = value,
                    Lifecycle = lifecycle,
                    OriginalScale = lifecycle.Transform.localScale,
                    State = EntryState.Initializing
                };
                entries.Add(entry);
                entryIndices.Add(value, entries.Count - 1);
                tracked = true;

                BeginLifecycle();
                try
                {
                    lifecycle.CreatedByPool();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                failure = EndLifecycle(failure);
                if (failure != null)
                    throw failure;
                if (IsDestroyedUnityObject(entry.Value))
                    throw new InvalidOperationException("Pooled object was destroyed during OnPoolCreated.");

                if (disposed || !TryGetEntryIndex(entry, out int index) || entry.State != EntryState.Initializing)
                    return null;

                FinalizeInactive(entry, index);
                return entry;
            }
            catch
            {
                if (tracked)
                {
                    try { Quarantine(entry); }
                    catch { /* Preserve the creation/lifecycle failure. */ }
                }
                else if (!destructionAttempted
                         && !ReferenceEquals(value, null)
                         && !entryIndices.ContainsKey(value))
                {
                    try { DestroyUntracked(value, lifecycle ?? new PoolLifecycleRunner(value)); }
                    catch { /* Preserve the creation/lifecycle failure. */ }
                }
                throw;
            }
        }

        private void Quarantine(Entry entry)
        {
            if (!TryGetEntryIndex(entry, out int index))
                return;

            if (entry.State == EntryState.Active || entry.State == EntryState.Renting)
                activeCount--;
            if (entry.State == EntryState.Inactive)
                RemoveInactiveIndex(index);
            entry.Version = NextVersion(entry.Version);
            Entry detached = DetachAt(index);
            DestroyDetached(detached);
        }

        private void FinalizeInactive(Entry entry, int index)
        {
            double idleSince = now();
            if (disposed || !TryGetEntryIndex(entry, out int currentIndex) || currentIndex != index)
                return;

            entry.IdleSince = idleSince;
            entry.State = EntryState.Inactive;
            inactiveIndices.Add(index);
        }

        internal bool IsReturning(IPoolable value, uint version)
        {
            if (disposed || ReferenceEquals(value, null) || IsDestroyedUnityObject(value)
                || !entryIndices.TryGetValue(value, out int index))
                return false;

            Entry entry = entries[index];
            return ReferenceEquals(entry.Value, value)
                   && entry.State == EntryState.Returning
                   && entry.Version == version;
        }

        private Entry DetachInactiveAt(int idleListIndex)
        {
            int entryIndex = inactiveIndices[idleListIndex];
            int lastIdleIndex = inactiveIndices.Count - 1;
            if (idleListIndex != lastIdleIndex)
                inactiveIndices[idleListIndex] = inactiveIndices[lastIdleIndex];
            inactiveIndices.RemoveAt(lastIdleIndex);
            return DetachAt(entryIndex);
        }

        private void RemoveInactiveIndex(int entryIndex)
        {
            for (int i = inactiveIndices.Count - 1; i >= 0; i--)
            {
                if (inactiveIndices[i] != entryIndex)
                    continue;
                int last = inactiveIndices.Count - 1;
                inactiveIndices[i] = inactiveIndices[last];
                inactiveIndices.RemoveAt(last);
                return;
            }
        }

        private int FindTrimCandidate(uint epoch)
        {
            for (int i = inactiveIndices.Count - 1; i >= 0; i--)
            {
                if (entries[inactiveIndices[i]].TrimEpoch == epoch)
                    return i;
            }
            return -1;
        }

        private bool TryGetEntryIndex(Entry entry, out int index)
        {
            if (entry != null
                && !ReferenceEquals(entry.Value, null)
                && entryIndices.TryGetValue(entry.Value, out index)
                && index >= 0
                && index < entries.Count
                && ReferenceEquals(entries[index], entry))
                return true;

            index = -1;
            return false;
        }

        private Entry DetachAt(int entryIndex)
        {
            int lastIndex = entries.Count - 1;
            Entry detached = entries[entryIndex];
            entryIndices.Remove(detached.Value);
            if (entryIndex != lastIndex)
            {
                Entry moved = entries[lastIndex];
                entries[entryIndex] = moved;
                entryIndices[moved.Value] = entryIndex;
                for (int i = 0; i < inactiveIndices.Count; i++)
                {
                    if (inactiveIndices[i] == lastIndex)
                        inactiveIndices[i] = entryIndex;
                }
            }
            entries.RemoveAt(lastIndex);
            return detached;
        }

        private void DestroyUntracked(IPoolable value, PoolLifecycleRunner lifecycle)
        {
            Exception failure = null;
            BeginLifecycle();
            try { lifecycle.DestroyByPool(); }
            catch (Exception exception) { failure = exception; }
            try { destroy(value); }
            catch (Exception exception) { failure ??= exception; }
            failure = EndLifecycle(failure);
            if (failure != null)
                throw failure;
        }

        private void DestroyDetached(Entry entry)
        {
            EntryState previousState = entry.State;
            entry.State = EntryState.Destroying;
            entry.Version = NextVersion(entry.Version);

            Exception failure = null;
            BeginLifecycle();
            if (previousState == EntryState.Renting
                || previousState == EntryState.Active
                || previousState == EntryState.ReturnPending)
            {
                try { entry.Lifecycle.ReturnToPool(activeRoot, entry.OriginalScale); }
                catch (Exception exception) { failure = exception; }
            }
            try { DestroyUntracked(entry.Value, entry.Lifecycle); }
            catch (Exception exception) { failure ??= exception; }
            failure = EndLifecycle(failure);
            if (failure != null)
                throw failure;
        }

        private void BeginLifecycle()
        {
            lifecycleDepth++;
        }

        private Exception EndLifecycle(Exception failure)
        {
            lifecycleDepth--;
            if (lifecycleDepth == 0 && disposed && !disposing)
            {
                try { CompleteDispose(); }
                catch (Exception exception) { failure ??= exception; }
            }
            return failure;
        }

        private static uint NextVersion(uint version)
        {
            version++;
            return version == 0 ? 1u : version;
        }

        private static bool IsDestroyedUnityObject(IPoolable value) =>
            value is UnityEngine.Object unityObject && unityObject == null;

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Pool));
        }
    }
}
