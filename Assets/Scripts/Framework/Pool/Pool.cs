using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.Pool
{
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

        private readonly PoolConfig _config;
        private readonly Transform _activeRoot;
        private readonly Func<IPoolable> _create;
        private readonly Action<IPoolable> _destroy;
        private readonly Func<double> _now;
        private readonly List<Entry> _entries;
        private readonly List<int> _inactiveIndices;
        private readonly Dictionary<IPoolable, int> _entryIndices;

        private int _creatingCount;
        private int _lifecycleDepth;
        private uint _trimEpoch;
        private bool _disposed;
        private bool _disposing;
        private bool _trimming;

        public Pool(PoolConfig config, Transform activeRoot, Func<IPoolable> create, Action<IPoolable> destroy, Func<double> now)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _activeRoot = activeRoot != null ? activeRoot : throw new ArgumentNullException(nameof(activeRoot));
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _destroy = destroy ?? throw new ArgumentNullException(nameof(destroy));
            _now = now ?? throw new ArgumentNullException(nameof(now));
            config.Validate();
            _entries = new List<Entry>(config.MaxPool);
            _inactiveIndices = new List<int>(config.MaxPool);
            _entryIndices = new Dictionary<IPoolable, int>(config.MaxPool);
        }

        public int CountAll => _entries.Count;
        public int CountActive { get; private set; }
        public int CountInactive => _inactiveIndices.Count;

        public void Prewarm()
        {
            ThrowIfDisposed();
            if (_lifecycleDepth > 0) return;
            while (!_disposed && _entries.Count + _creatingCount < _config.MinPool)
            {
                if (CreateInactive() == null) break;
            }
        }

        public bool TryRent(in PoolSpawnArgs args, out PoolLease lease)
        {
            lease = default;
            if (_disposed || _lifecycleDepth > 0) return false;

            Entry entry;
            if (_inactiveIndices.Count > 0)
            {
                int idleListIndex = _inactiveIndices.Count - 1;
                int selectedIndex = _inactiveIndices[idleListIndex];
                _inactiveIndices.RemoveAt(idleListIndex);
                entry = _entries[selectedIndex];
            }
            else
            {
                if (_entries.Count + _creatingCount >= _config.MaxPool) return false;
                entry = CreateInactive();
                if (entry == null || !TryGetEntryIndex(entry, out int createdIndex)) return false;
                RemoveInactiveIndex(createdIndex);
            }

            entry.State = EntryState.Renting;
            CountActive++;
            entry.Version = NextVersion(entry.Version);
            uint rentalVersion = entry.Version;
            PoolLease candidate = new PoolLease(this, entry.Value, rentalVersion);
            Transform parent = args.Parent != null ? args.Parent : _activeRoot;
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

            switch (entry.State)
            {
                case EntryState.ReturnPending:
                    CompletePendingReturn(entry, entryIndex);
                    return false;
                case EntryState.Renting when entry.Version == rentalVersion && !_disposed:
                    entry.State = EntryState.Active;
                    lease = candidate;
                    return true;
                default:
                    return false;
            }
        }

        public int TrimIdle(bool retainMinimum)
        {
            if (_disposed || _trimming || _lifecycleDepth > 0 || _config.ReturnDelaySeconds == 0f) return 0;
            double timestamp = _now();
            if (_disposed) return 0;
            int minimum = retainMinimum ? _config.MinPool : 0;
            int removed = 0;
            Exception failure = null;
            uint epoch = _trimEpoch = NextVersion(_trimEpoch);
            for (int i = 0; i < _inactiveIndices.Count; i++) _entries[_inactiveIndices[i]].TrimEpoch = epoch;
            _trimming = true;
            try
            {
                while (!_disposed && _inactiveIndices.Count > minimum)
                {
                    int idleListIndex = FindTrimCandidate(epoch);
                    if (idleListIndex < 0) break;
                    Entry entry = _entries[_inactiveIndices[idleListIndex]];
                    entry.TrimEpoch = 0;
                    if (timestamp - entry.IdleSince < _config.ReturnDelaySeconds) continue;
                    Entry detached = DetachInactiveAt(idleListIndex);
                    removed++;
                    try { DestroyDetached(detached); }
                    catch (Exception exception) { failure ??= exception; }
                }
            }
            finally
            {
                _trimming = false;
            }

            return failure != null ? throw failure : removed;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_lifecycleDepth > 0 || _disposing) return;
            CompleteDispose();
        }

        private void CompleteDispose()
        {
            if (_disposing) return;

            _disposing = true;
            Exception failure = null;
            try
            {
                CountActive = 0;
                _inactiveIndices.Clear();
                while (_entries.Count > 0)
                {
                    Entry detached = DetachAt(_entries.Count - 1);
                    try { DestroyDetached(detached); }
                    catch (Exception exception) { failure ??= exception; }
                }
                _entryIndices.Clear();
            }
            finally
            {
                _disposing = false;
            }

            if (failure != null) throw failure;
        }

        internal bool IsCurrentRental(IPoolable value, uint version)
        {
            if (_disposed || ReferenceEquals(value, null) || IsDestroyedUnityObject(value)
                || !_entryIndices.TryGetValue(value, out int index))
                return false;

            Entry entry = _entries[index];
            return ReferenceEquals(entry.Value, value)
                   && (entry.State == EntryState.Renting || entry.State == EntryState.Active)
                   && entry.Version == version;
        }

        internal bool ShouldContinueRentCallbacks(IPoolable value, uint version)
        {
            if (ReferenceEquals(value, null) || IsDestroyedUnityObject(value)
                || !_entryIndices.TryGetValue(value, out int index))
                return false;

            Entry entry = _entries[index];
            return ReferenceEquals(entry.Value, value)
                   && entry.State == EntryState.Renting
                   && entry.Version == version;
        }

        internal bool TryDetachDestroyedValue(IPoolable value, uint version)
        {
            if (_disposed || ReferenceEquals(value, null)
                || !_entryIndices.TryGetValue(value, out int index))
                return false;

            Entry entry = _entries[index];
            if (!ReferenceEquals(entry.Value, value)
                || entry.Version != version
                || entry.State is not (EntryState.Renting or EntryState.Active))
                return false;

            CountActive--;
            entry.State = EntryState.Destroying;
            entry.Version = NextVersion(entry.Version);
            DetachAt(index);
            return true;
        }

        internal bool TryReturn(IPoolable value, uint version)
        {
            if (!IsCurrentRental(value, version)) return false;

            int index = _entryIndices[value];
            Entry entry = _entries[index];
            if (entry.State == EntryState.Renting)
            {
                entry.State = EntryState.ReturnPending;
                entry.Version = NextVersion(entry.Version);
                CountActive--;
                return true;
            }

            entry.State = EntryState.Returning;
            entry.Version = NextVersion(entry.Version);
            uint returnVersion = entry.Version;
            CountActive--;

            Exception failure = null;
            BeginLifecycle();
            try
            {
                entry.Lifecycle.ReturnToPool(_activeRoot, entry.OriginalScale, this, returnVersion);
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

            if (_disposed || !TryGetEntryIndex(entry, out index)) return false;

            if (entry.State != EntryState.Returning || entry.Version != returnVersion) return false;

            if (IsDestroyedUnityObject(entry.Value))
            {
                Quarantine(entry); return false;
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
            if (_disposed || !TryGetEntryIndex(entry, out int currentIndex) || currentIndex != index
                || entry.State != EntryState.ReturnPending)
                return false;

            entry.State = EntryState.Returning;
            uint returnVersion = entry.Version;
            Exception failure = null;
            BeginLifecycle();
            try
            {
                entry.Lifecycle.ReturnToPool(_activeRoot, entry.OriginalScale, this, returnVersion);
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

            if (_disposed || !TryGetEntryIndex(entry, out currentIndex)
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
            _creatingCount++;
            BeginLifecycle();
            try
            {
                value = _create();
                if (ReferenceEquals(value, null)) throw new InvalidOperationException("Pool create delegate returned null.");
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            failure = EndLifecycle(failure);
            _creatingCount--;

            if (failure != null)
            {
                if (ReferenceEquals(value, null) || _entryIndices.ContainsKey(value)) throw failure;
                try { DestroyUntracked(value, new PoolLifecycleRunner(value)); }
                catch { /* Preserve the creation/disposal failure. */ }
                throw failure;
            }

            try
            {
                if (_disposed)
                {
                    destructionAttempted = true;
                    DestroyUntracked(value, new PoolLifecycleRunner(value));
                    return null;
                }
                lifecycle = new PoolLifecycleRunner(value);
                if (lifecycle.PoolObject.activeSelf) throw new InvalidOperationException("Pool create delegate must return an inactive IPoolable.");
                if (value != null && _entryIndices.ContainsKey(value)) throw new InvalidOperationException("Pool create delegate returned an instance already owned by this pool.");

                lifecycle.Transform.SetParent(_activeRoot, false);
                entry = new Entry
                {
                    Value = value,
                    Lifecycle = lifecycle,
                    OriginalScale = lifecycle.Transform.localScale,
                    State = EntryState.Initializing
                };
                _entries.Add(entry);
                if (value != null) _entryIndices.Add(value, _entries.Count - 1);
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
                if (failure != null) throw failure;
                if (IsDestroyedUnityObject(entry.Value)) throw new InvalidOperationException("Pooled object was destroyed during OnPoolCreated.");
                if (_disposed || !TryGetEntryIndex(entry, out int index) || entry.State != EntryState.Initializing) return null;
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
                         && !_entryIndices.ContainsKey(value))
                {
                    try { DestroyUntracked(value, lifecycle ?? new PoolLifecycleRunner(value)); }
                    catch { /* Preserve the creation/lifecycle failure. */ }
                }
                throw;
            }
        }

        private void Quarantine(Entry entry)
        {
            if (!TryGetEntryIndex(entry, out int index)) return;

            switch (entry.State)
            {
                case EntryState.Active or EntryState.Renting:
                    CountActive--;
                    break;
                case EntryState.Inactive:
                    RemoveInactiveIndex(index);
                    break;
            }

            entry.Version = NextVersion(entry.Version);
            Entry detached = DetachAt(index);
            DestroyDetached(detached);
        }

        private void FinalizeInactive(Entry entry, int index)
        {
            double idleSince = _now();
            if (_disposed || !TryGetEntryIndex(entry, out int currentIndex) || currentIndex != index) return;
            entry.IdleSince = idleSince;
            entry.State = EntryState.Inactive;
            _inactiveIndices.Add(index);
        }

        internal bool IsReturning(IPoolable value, uint version)
        {
            if (_disposed || ReferenceEquals(value, null) || IsDestroyedUnityObject(value)
                || !_entryIndices.TryGetValue(value, out int index))
                return false;

            Entry entry = _entries[index];
            return ReferenceEquals(entry.Value, value)
                   && entry.State == EntryState.Returning
                   && entry.Version == version;
        }

        private Entry DetachInactiveAt(int idleListIndex)
        {
            int entryIndex = _inactiveIndices[idleListIndex];
            int lastIdleIndex = _inactiveIndices.Count - 1;
            if (idleListIndex != lastIdleIndex) _inactiveIndices[idleListIndex] = _inactiveIndices[lastIdleIndex];
            _inactiveIndices.RemoveAt(lastIdleIndex);
            return DetachAt(entryIndex);
        }

        private void RemoveInactiveIndex(int entryIndex)
        {
            for (int i = _inactiveIndices.Count - 1; i >= 0; i--)
            {
                if (_inactiveIndices[i] != entryIndex) continue;
                int last = _inactiveIndices.Count - 1;
                _inactiveIndices[i] = _inactiveIndices[last];
                _inactiveIndices.RemoveAt(last);
                return;
            }
        }

        private int FindTrimCandidate(uint epoch)
        {
            for (int i = _inactiveIndices.Count - 1; i >= 0; i--)
            {
                if (_entries[_inactiveIndices[i]].TrimEpoch == epoch) return i;
            }
            return -1;
        }

        private bool TryGetEntryIndex(Entry entry, out int index)
        {
            if (entry != null
                && !ReferenceEquals(entry.Value, null)
                && _entryIndices.TryGetValue(entry.Value, out index)
                && index >= 0
                && index < _entries.Count
                && ReferenceEquals(_entries[index], entry))
                return true;
            index = -1;
            return false;
        }

        private Entry DetachAt(int entryIndex)
        {
            int lastIndex = _entries.Count - 1;
            Entry detached = _entries[entryIndex];
            _entryIndices.Remove(detached.Value);
            if (entryIndex != lastIndex)
            {
                Entry moved = _entries[lastIndex];
                _entries[entryIndex] = moved;
                _entryIndices[moved.Value] = entryIndex;
                for (int i = 0; i < _inactiveIndices.Count; i++)
                {
                    if (_inactiveIndices[i] == lastIndex) _inactiveIndices[i] = entryIndex;
                }
            }
            _entries.RemoveAt(lastIndex);
            return detached;
        }

        private void DestroyUntracked(IPoolable value, PoolLifecycleRunner lifecycle)
        {
            Exception failure = null;
            BeginLifecycle();
            try { lifecycle.DestroyByPool(); }
            catch (Exception exception) { failure = exception; }
            try { _destroy(value); }
            catch (Exception exception) { failure ??= exception; }
            failure = EndLifecycle(failure);
            if (failure != null) throw failure;
        }

        private void DestroyDetached(Entry entry)
        {
            EntryState previousState = entry.State;
            entry.State = EntryState.Destroying;
            entry.Version = NextVersion(entry.Version);

            Exception failure = null;
            BeginLifecycle();
            if (previousState is EntryState.Renting or EntryState.Active or EntryState.ReturnPending)
            {
                try { entry.Lifecycle.ReturnToPool(_activeRoot, entry.OriginalScale); }
                catch (Exception exception) { failure = exception; }
            }
            try { DestroyUntracked(entry.Value, entry.Lifecycle); }
            catch (Exception exception) { failure ??= exception; }
            failure = EndLifecycle(failure);
            if (failure != null) throw failure;
        }

        private void BeginLifecycle()
        {
            _lifecycleDepth++;
        }

        private Exception EndLifecycle(Exception failure)
        {
            _lifecycleDepth--;
            if (_lifecycleDepth != 0 || !_disposed || _disposing) return failure;
            try { CompleteDispose(); }
            catch (Exception exception) { failure ??= exception; }
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
            if (_disposed) throw new ObjectDisposedException(nameof(Pool));
        }
    }
}
