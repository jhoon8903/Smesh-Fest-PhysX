namespace Framework.Pool
{
    /// <summary>One rental epoch. A lease from an earlier rental cannot return a newer one.</summary>
    public readonly struct PoolLease
    {
        private readonly Pool owner;
        private readonly IPoolable value;
        private readonly uint version;

        internal PoolLease(Pool owner, IPoolable value, uint version)
        {
            this.owner = owner;
            this.value = value;
            this.version = version;
        }

        public IPoolable Value => IsValid ? value : null;
        public bool IsValid => owner != null && owner.IsCurrentRental(value, version);
        internal bool ContinueRentCallbacks => owner != null && owner.ShouldContinueRentCallbacks(value, version);

        /// <summary>
        /// Attempts to end this rental. Returns false for a stale/default/foreign lease, a
        /// duplicate return, or when disposal prevents the object from re-entering inventory.
        /// </summary>
        public bool Return()
        {
            return owner != null && owner.TryReturn(value, version);
        }
    }
}
