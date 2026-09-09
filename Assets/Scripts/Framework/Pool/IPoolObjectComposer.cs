namespace Framework.Pool
{
    public interface IPoolObjectComposer
    {
        void Compose(IPoolable owner);
    }
}
