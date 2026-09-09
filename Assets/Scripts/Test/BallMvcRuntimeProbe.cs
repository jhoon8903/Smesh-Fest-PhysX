#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.IO;
using Framework.Pool;
using Framework.Loop;
using InGame.Ball;
using InGame.Config;
using InGame.DI;
using UnityEngine;
using VContainer;

namespace Framework.Test
{
    public sealed class BallMvcRuntimeProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string recordedAtUtc;
            public string unityVersion;
            public bool success;
            public int assertions;
            public string error;
            public string scope;
        }

        private readonly Result result = new Result
        {
            scope = "Isolated PoolFactory plus a temporary BallView source with required Rigidbody and SphereCollider; does not edit a saved Scene, Prefab, PoolConfig asset, or input, and does not simulate physics."
        };

        private GameObject fixtureRoot;
        private PoolFactory factory;
        private IObjectResolver resolver;
        private BallConfig ballSettings;
        private ObstacleConfig obstacleSettings;
        private GroundFadeConfig groundFadeSettings;
        private LoopDispatcher loopDispatcher;
        private WorldObjectControllerRegistry controllers;
        private string outputPath;

        public void Begin(GameObject root, PoolContainer container, PoolConfig config,
            PoolConfig hierarchyFirstConfig, string output)
        {
            fixtureRoot = root;
            outputPath = output;
            StartCoroutine(Run(container, config, hierarchyFirstConfig));
        }

        private IEnumerator Run(PoolContainer container, PoolConfig config, PoolConfig hierarchyFirstConfig)
        {
            result.recordedAtUtc = DateTime.UtcNow.ToString("O");
            result.unityVersion = Application.unityVersion;
            try
            {
                ballSettings = ScriptableObject.CreateInstance<BallConfig>();
                obstacleSettings = ScriptableObject.CreateInstance<ObstacleConfig>();
                groundFadeSettings = ScriptableObject.CreateInstance<GroundFadeConfig>();
                loopDispatcher = new LoopDispatcher();
                loopDispatcher.StartLoop();
                ContainerBuilder builder = new ContainerBuilder();
                builder.RegisterInstance(ballSettings);
                builder.RegisterInstance(groundFadeSettings);
                builder.RegisterInstance<ILoopEvents>(loopDispatcher);
                resolver = builder.Build();
                controllers = new WorldObjectControllerRegistry(ballSettings, obstacleSettings);
                factory = new PoolFactory(container, resolver, controllers, true);
                factory.Initialize(false);

                IPool pool = factory.GetPool(config);
                Assert(pool.CountAll == 1 && pool.CountInactive == 1 && pool.CountActive == 0,
                    "Prewarm did not leave exactly one inactive BallView instance.");

                PoolSpawnArgs args = new PoolSpawnArgs(Vector3.zero, Quaternion.identity);
                Assert(factory.TryRent(config, args, out PoolLease firstLease), "Initial Ball rent failed.");
                BallView view = firstLease.Value as BallView;
                Assert(view != null, "Pool did not return a BallView.");
                Assert(controllers.TryGet(view, out BallController controller), "Registry did not compose a BallController.");
                BallModel model = controller.Model;
                uint firstEpoch = model.RentalEpoch;
                Assert(controller.IsObserving &&
                       model.IsRented && controller.IsRented && controller.RentalEpoch == firstEpoch &&
                       controller.IsCurrentRental(firstEpoch) && model.IsCurrentRental(firstEpoch) &&
                       model.ObserverCount == 1,
                    "First rental did not establish one current Ball MVC observation.");

                Assert(controller.TryReturn(firstEpoch), "Current BallController return failed.");
                Assert(!firstLease.IsValid && !model.IsRented && !controller.IsRented &&
                       model.ObserverCount == 0 && !controller.IsObserving,
                    "Controller return did not detach the Ball MVC observation and rental state.");

                Assert(factory.TryRent(config, args, out PoolLease secondLease), "Ball re-rent failed.");
                Assert(ReferenceEquals(secondLease.Value, view) && controllers.TryGet(view, out BallController reused) &&
                       ReferenceEquals(reused, controller),
                    "Ball re-rent recreated a View, Model, or Controller instead of reusing the bundle.");

                uint secondEpoch = model.RentalEpoch;
                Assert(secondEpoch != firstEpoch && model.IsRented && controller.IsRented &&
                       model.IsCurrentRental(secondEpoch) && controller.IsCurrentRental(secondEpoch) &&
                       !model.IsCurrentRental(firstEpoch) && !controller.IsCurrentRental(firstEpoch) &&
                       model.ObserverCount == 1 && controller.IsObserving,
                    "Re-rent did not advance the epoch or restore exactly one observation.");
                Assert(!firstLease.Return() && !controller.TryReturn(firstEpoch) && secondLease.IsValid &&
                       model.IsCurrentRental(secondEpoch) && controller.IsCurrentRental(secondEpoch) &&
                       model.ObserverCount == 1,
                    "A stale PoolLease or BallController epoch disturbed the newer rental.");

                Assert(secondLease.Return(), "Current PoolLease return failed.");
                Assert(!secondLease.IsValid && !model.IsRented && !controller.IsRented &&
                       model.ObserverCount == 0 && !controller.IsObserving,
                    "PoolLease return did not detach the Ball MVC observation and rental state.");

                Assert(factory.TryRent(config, args, out PoolLease disposeLease),
                    "Ball rent before active pool disposal failed.");
                uint disposeEpoch = model.RentalEpoch;
                pool.Dispose();
                Assert(!disposeLease.IsValid && !model.IsRented && !controller.IsRented &&
                       !model.IsCurrentRental(disposeEpoch) && !controller.IsCurrentRental(disposeEpoch) &&
                       model.ObserverCount == 0 && !controller.IsObserving,
                    "Active pool disposal left the Ball MVC rental or observer connected.");

                IPool hierarchyFirstPool = factory.GetPool(hierarchyFirstConfig);
                Assert(factory.TryRent(hierarchyFirstConfig, args, out PoolLease hierarchyLease),
                    "Hierarchy-first Ball rent failed.");
                BallView hierarchyView = hierarchyLease.Value as BallView;
                Assert(hierarchyView != null, "Hierarchy-first pool did not return a BallView.");
                Assert(controllers.TryGet(hierarchyView, out BallController hierarchyController),
                    "Registry did not compose a hierarchy-first BallController.");
                BallModel hierarchyModel = hierarchyController.Model;
                uint hierarchyEpoch = hierarchyModel.RentalEpoch;

                DestroyAndAssertNoError(hierarchyView.gameObject,
                    "Hierarchy-first Ball destruction logged an error.");
                Assert(!hierarchyModel.IsRented && !hierarchyController.IsRented &&
                       !hierarchyModel.IsCurrentRental(hierarchyEpoch) &&
                       !hierarchyController.IsCurrentRental(hierarchyEpoch) &&
                       hierarchyModel.ObserverCount == 0,
                    "Hierarchy-first destruction left the Ball MVC bundle active before pool disposal.");
                Assert(!hierarchyLease.IsValid && !hierarchyLease.Return() &&
                       !hierarchyController.TryLaunch(hierarchyEpoch, Vector3.right, BallTrajectoryMode.Straight),
                    "A destroyed Ball remained reachable through its lease or controller before pool disposal.");
                Assert(hierarchyFirstPool.CountAll == 0 && hierarchyFirstPool.CountActive == 0 &&
                       hierarchyFirstPool.CountInactive == 0 && controllers.BallCount == 0,
                    "Hierarchy-first Ball destruction left an orphaned pool or registry entry.");

                hierarchyFirstPool.Dispose();
                Assert(!hierarchyLease.IsValid,
                    "Hierarchy-first pool disposal did not invalidate the destroyed Ball lease.");

                factory.Dispose();
                factory = null;
                result.success = true;
            }
            catch (Exception exception)
            {
                result.success = false;
                result.error = exception.ToString();
            }
            finally
            {
                try { factory?.Dispose(); }
                catch (Exception exception)
                {
                    result.success = false;
                    result.error = (result.error ?? string.Empty) + "\nCleanup: " + exception;
                }
                resolver?.Dispose();
                controllers?.Dispose();
                if (fixtureRoot != null) Destroy(fixtureRoot);
                if (config != null) Destroy(config);
                if (hierarchyFirstConfig != null) Destroy(hierarchyFirstConfig);
                if (ballSettings != null) Destroy(ballSettings);
                if (obstacleSettings != null) Destroy(obstacleSettings);
                loopDispatcher?.Dispose();
                if (groundFadeSettings != null) Destroy(groundFadeSettings);
            }

            yield return null;
            yield return null;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true));
            if (result.success) Debug.Log("[BallMvcValidation] Passed " + result.assertions + " assertions.");
            else Debug.LogError("[BallMvcValidation] " + result.error);
            Destroy(gameObject);
        }

        private void Assert(bool condition, string message)
        {
            result.assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private void DestroyAndAssertNoError(GameObject target, string message)
        {
            string loggedFailure = null;
            void CaptureLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    loggedFailure ??= condition + "\n" + stackTrace;
            }

            Application.logMessageReceived += CaptureLog;
            try { DestroyImmediate(target); }
            finally { Application.logMessageReceived -= CaptureLog; }

            Assert(loggedFailure == null, loggedFailure == null ? message : message + "\n" + loggedFailure);
        }
    }
}
#endif
