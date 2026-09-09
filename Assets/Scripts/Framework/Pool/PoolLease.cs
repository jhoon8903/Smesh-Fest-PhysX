namespace Framework.Pool
{
    public readonly struct PoolLease
    {
        private readonly Pool _owner;
        private readonly IPoolable _value;
        private readonly uint _version;

        internal PoolLease(Pool owner, IPoolable value, uint version)
        {
            _owner = owner;
            _value = value;
            _version = version;
        }
        public IPoolable Value => IsValid ? _value : null;
        public bool IsValid => _owner != null && _owner.IsCurrentRental(_value, _version);
        internal bool ContinueRentCallbacks => _owner != null && _owner.ShouldContinueRentCallbacks(_value, _version);
        internal bool DetachDestroyedValue() => _owner != null && _owner.TryDetachDestroyedValue(_value, _version);
        public bool Return() => _owner != null && _owner.TryReturn(_value, _version);
    }
}
