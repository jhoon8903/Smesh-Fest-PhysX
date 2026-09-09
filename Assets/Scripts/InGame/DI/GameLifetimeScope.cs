using Framework.Loop;
using Framework.Pool;
using InGame.Config;
using InGame.Flow;
using InGame.Level;
using InGame.Shot;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace InGame.DI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5000)]
    [RequireComponent(typeof(GameFlow), typeof(UpdateLoop))]
    public class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private GameObject uiRoot;
        [SerializeField] private PoolContainer poolContainer;
        [SerializeField] private Transform obstacleRoot;
        [SerializeField] private BallConfig ballSettings;
        [SerializeField] private ObstacleConfig obstacleSettings;
        [SerializeField] private PhysXConfig physXSettings;
        [SerializeField] private GroundFadeConfig groundFadeSettings;
        [SerializeField] private LevelSpawner levelSpawner;
        [SerializeField] private LevelSession levelSession;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register(_ => new GameClock(), Lifetime.Scoped).AsSelf().As<IGamePause>();
            builder.Register(_ => new LoopDispatcher(), Lifetime.Scoped).AsSelf().As<ILoopEvents>();
            builder.Register(resolver => new UnityGameTime(resolver.Resolve<GameClock>()), Lifetime.Scoped);
            builder.RegisterComponent(poolContainer);
            if (ballSettings == null || obstacleSettings == null) throw new System.InvalidOperationException("The gameplay WorldObjects scope requires Ball Config and Obstacle Config for Controller composition.");
            builder.RegisterInstance(ballSettings);
            builder.RegisterInstance(obstacleSettings);
            builder.Register(resolver => new WorldObjectControllerRegistry(
                    resolver.Resolve<BallConfig>(), resolver.Resolve<ObstacleConfig>()), Lifetime.Scoped)
                .AsSelf().As<IPoolObjectComposer>();
            builder.Register(resolver => new PoolFactory(resolver.Resolve<PoolContainer>(), resolver,
                resolver.Resolve<IPoolObjectComposer>(), retainMinimum: true), Lifetime.Scoped);
            builder.RegisterComponent(GetComponent<UpdateLoop>());
            builder.RegisterComponent(GetComponent<GameFlow>());
            if (groundFadeSettings == null) throw new System.InvalidOperationException("The gameplay WorldObjects scope requires an explicit Ground Fade Config.");
            builder.RegisterInstance(groundFadeSettings);
            if (levelSpawner == null || levelSession == null) throw new System.InvalidOperationException("The gameplay WorldObjects scope requires explicit LevelSpawner and LevelSession references.");
            builder.RegisterComponent(levelSpawner);
            builder.RegisterComponent(levelSession);
            WorldPointerInput pointerInput = GetComponent<WorldPointerInput>();
            if (pointerInput != null)
            {
                if (obstacleRoot == null || physXSettings == null) throw new System.InvalidOperationException("The gameplay WorldObjects scope requires Obstacle Root and PhysX Config.");
                builder.RegisterInstance(physXSettings);
                builder.RegisterComponent(pointerInput);
            }
            builder.RegisterBuildCallback(resolver =>
            {
                resolver.Resolve<UnityGameTime>();
                if (pointerInput != null) resolver.InjectGameObject(obstacleRoot.gameObject);
                resolver.Resolve<PoolFactory>().Initialize();
                if (uiRoot != null) resolver.InjectGameObject(uiRoot);
            });
        }
    }
}
