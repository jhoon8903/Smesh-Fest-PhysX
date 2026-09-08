namespace Framework.Pool
{
    /// <summary>
    /// Reusable parts attached below an <see cref="IPoolable"/>. Rent/return callbacks own
    /// per-rental state such as subscriptions, async cancellation, tweens and particles.
    /// Destroy releases resources held for the lifetime of the pooled instance.
    /// </summary>
    public interface IPoolLifecycle
    {
        /// <summary>Called once after the inactive instance becomes owned by its pool.</summary>
        void OnPoolCreated(IPoolable owner);

        /// <summary>
        /// Resets and connects one rental. Returning the supplied lease is allowed; recursively
        /// renting from the same pool while a lifecycle callback is running is rejected.
        /// </summary>
        void OnPoolRent(PoolLease lease);

        /// <summary>
        /// Disconnects every per-rental callback and stops/cancels transient work. This is also
        /// called before destroy when a pool is disposed while the instance is rented.
        /// </summary>
        void OnPoolReturn();

        /// <summary>
        /// Releases instance-lifetime resources. It can follow creation without a rent, and for
        /// a live rental it runs only after the complete return cleanup pass.
        /// </summary>
        void OnPoolDestroy();
    }
}
