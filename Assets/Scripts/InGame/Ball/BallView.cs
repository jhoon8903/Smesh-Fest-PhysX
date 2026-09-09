using System;
using Framework.Object;
using Framework.Pool;
using UnityEngine;

namespace InGame.Ball
{
    public sealed class BallView : ObView, IPoolable
    {
        public GameObject PoolObject => gameObject;

        internal event Action Created;
        internal event Action<PoolLease> Rented;
        internal event Action Returned;
        internal event Action<Collision> CollisionEntered;

        public void OnPoolCreated(IPoolable owner)
        {
            if (!ReferenceEquals(owner, this)) throw new InvalidOperationException("BallView must be the owner of its PoolObject.");
            if (Created == null || Rented == null || Returned == null)
                throw new InvalidOperationException("BallView requires Controller composition before pool creation.");
            Created.Invoke();
        }

        public void OnPoolRent(PoolLease lease)
        {
            if (Rented == null) throw new InvalidOperationException("BallView has no pool-rent lifecycle owner.");
            Rented.Invoke(lease);
        }

        public void OnPoolReturn()
        {
            if (Returned == null) throw new InvalidOperationException("BallView has no pool-return lifecycle owner.");
            Returned.Invoke();
        }
        public void OnPoolDestroy() => NotifyDestroying();

        private void OnCollisionEnter(Collision collision)
        {
            CollisionEntered?.Invoke(collision);
        }
    }
}
