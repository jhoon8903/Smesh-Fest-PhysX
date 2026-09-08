using Framework.Flow;
using Framework.Loop;
using Framework.Pool;
using UnityEngine;
using VContainer;
using VContainer.Unity;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-5000)]
[RequireComponent(typeof(GameFlow), typeof(UpdateLoop))]
public class GameLifetimeScope : LifetimeScope
{
    [SerializeField] private GameObject uiRoot;
    [SerializeField] private PoolContainer poolContainer;

    protected override void Configure(IContainerBuilder builder)
    {
        // Explicit factories keep construction reachable when managed stripping is High.
        builder.Register(_ => new GameClock(), Lifetime.Scoped).AsSelf().As<IGamePause>();
        builder.Register(_ => new LoopDispatcher(), Lifetime.Scoped).AsSelf().As<ILoopEvents>();
        builder.Register(resolver => new UnityGameTime(resolver.Resolve<GameClock>()), Lifetime.Scoped);
        builder.RegisterComponent(poolContainer);
        builder.Register(resolver => new PoolFactory(resolver.Resolve<PoolContainer>(), resolver,
            retainMinimum: true), Lifetime.Scoped);
        builder.RegisterComponent(GetComponent<UpdateLoop>());
        builder.RegisterComponent(GetComponent<GameFlow>());
        builder.RegisterBuildCallback(resolver =>
        {
            resolver.Resolve<UnityGameTime>();
            resolver.Resolve<PoolFactory>().Initialize();
            if (uiRoot != null) resolver.InjectGameObject(uiRoot);
        });
    }
}
