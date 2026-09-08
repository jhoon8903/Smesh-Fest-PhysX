using Framework.Object;
using Framework.Pool;
using UnityEngine;

namespace InGame.Ball
{
    public class BallView : ObView, IPoolable
    {
        public GameObject PoolObject => gameObject;

        public void OnPoolCreated(IPoolable owner) { }
        public void OnPoolRent(PoolLease lease) { }
        public void OnPoolReturn() { }
        public void OnPoolDestroy() { }
    }
}
