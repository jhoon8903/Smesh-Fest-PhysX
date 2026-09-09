namespace Framework.Pool
{
    public interface IPoolLifecycle
    {
        void OnPoolCreated(IPoolable owner);
        void OnPoolRent(PoolLease lease);
        void OnPoolReturn();
        void OnPoolDestroy();
    }
}
