#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Framework.Pool;
using Framework.Loop;
using InGame.Config;
using InGame.DI;
using InGame.Obstacle;
using InGame.Presentation;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Framework.Test
{
    public sealed class ObstacleMvcRuntimeProbe : MonoBehaviour
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
            scope = "Isolated PoolFactory plus a temporary ObstacleView source with required Rigidbody and BoxCollider; does not edit a saved Scene, Prefab, PoolConfig asset, HP, destruction, or input, and does not simulate physics."
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
                builder.RegisterInstance(obstacleSettings);
                builder.RegisterInstance(groundFadeSettings);
                builder.RegisterInstance<ILoopEvents>(loopDispatcher);
                resolver = builder.Build();
                controllers = new WorldObjectControllerRegistry(ballSettings, obstacleSettings);
                factory = new PoolFactory(container, resolver, controllers, true);
                factory.Initialize(false);

                IPool pool = factory.GetPool(config);
                Assert(pool.CountAll == 1 && pool.CountInactive == 1 && pool.CountActive == 0,
                    "Prewarm did not leave exactly one inactive ObstacleView instance.");

                PoolSpawnArgs args = new PoolSpawnArgs(Vector3.zero, Quaternion.identity);
                Assert(factory.TryRent(config, args, out PoolLease firstLease), "Initial Obstacle rent failed.");
                ObstacleView view = firstLease.Value as ObstacleView;
                Assert(view != null, "Pool did not return an ObstacleView.");
                Assert(controllers.TryGet(view, out ObstacleController controller), "Registry did not compose an ObstacleController.");
                ObstacleModel model = controller.Model;
                uint firstEpoch = model.RentalEpoch;
                Assert(controller.IsObserving &&
                       model.IsRented && controller.IsRented && controller.RentalEpoch == firstEpoch &&
                       controller.IsCurrentRental(firstEpoch) && model.IsCurrentRental(firstEpoch) &&
                       model.ObserverCount == 1,
                    "First rental did not establish one current Obstacle MVC observation.");

                Assert(controller.TryReturn(firstEpoch), "Current ObstacleController return failed.");
                Assert(!firstLease.IsValid && !model.IsRented && !controller.IsRented &&
                       model.ObserverCount == 0 && !controller.IsObserving,
                    "Controller return did not detach the Obstacle MVC observation and rental state.");

                Assert(factory.TryRent(config, args, out PoolLease secondLease), "Obstacle re-rent failed.");
                Assert(ReferenceEquals(secondLease.Value, view) && controllers.TryGet(view, out ObstacleController reused) &&
                       ReferenceEquals(reused, controller),
                    "Obstacle re-rent recreated a View, Model, or Controller instead of reusing the bundle.");

                uint secondEpoch = model.RentalEpoch;
                Assert(secondEpoch != firstEpoch && model.IsRented && controller.IsRented &&
                       model.IsCurrentRental(secondEpoch) && controller.IsCurrentRental(secondEpoch) &&
                       !model.IsCurrentRental(firstEpoch) && !controller.IsCurrentRental(firstEpoch) &&
                       model.ObserverCount == 1 && controller.IsObserving,
                    "Re-rent did not advance the epoch or restore exactly one observation.");
                Assert(!firstLease.Return() && !controller.TryReturn(firstEpoch) && secondLease.IsValid &&
                       model.IsCurrentRental(secondEpoch) && controller.IsCurrentRental(secondEpoch) &&
                       model.ObserverCount == 1,
                    "A stale PoolLease or ObstacleController epoch disturbed the newer rental.");

                Assert(secondLease.Return(), "Current PoolLease return failed.");
                Assert(!secondLease.IsValid && !model.IsRented && !controller.IsRented &&
                       model.ObserverCount == 0 && !controller.IsObserving,
                    "PoolLease return did not detach the Obstacle MVC observation and rental state.");

                Assert(factory.TryRent(config, args, out PoolLease disposeLease),
                    "Obstacle rent before active pool disposal failed.");
                uint disposeEpoch = model.RentalEpoch;
                pool.Dispose();
                Assert(!disposeLease.IsValid && !model.IsRented && !controller.IsRented &&
                       !model.IsCurrentRental(disposeEpoch) && !controller.IsCurrentRental(disposeEpoch) &&
                       model.ObserverCount == 0 && !controller.IsObserving,
                    "Active pool disposal left the Obstacle MVC rental or observer connected.");

                IPool hierarchyFirstPool = factory.GetPool(hierarchyFirstConfig);
                Assert(factory.TryRent(hierarchyFirstConfig, args, out PoolLease hierarchyLease),
                    "Hierarchy-first Obstacle rent failed.");
                ObstacleView hierarchyView = hierarchyLease.Value as ObstacleView;
                Assert(hierarchyView != null, "Hierarchy-first pool did not return an ObstacleView.");
                Assert(controllers.TryGet(hierarchyView, out ObstacleController hierarchyController),
                    "Registry did not compose a hierarchy-first ObstacleController.");
                ObstacleModel hierarchyModel = hierarchyController.Model;
                uint hierarchyEpoch = hierarchyModel.RentalEpoch;

                DestroyAndAssertNoError(hierarchyView.gameObject,
                    "Hierarchy-first Obstacle destruction logged an error.");
                Assert(!hierarchyModel.IsRented && !hierarchyController.IsRented &&
                       !hierarchyModel.IsCurrentRental(hierarchyEpoch) &&
                       !hierarchyController.IsCurrentRental(hierarchyEpoch) &&
                       hierarchyModel.ObserverCount == 0,
                    "Hierarchy-first destruction left the Obstacle MVC bundle active before pool disposal.");
                Assert(!hierarchyLease.IsValid && !hierarchyLease.Return(),
                    "A destroyed Obstacle remained reachable through its lease before pool disposal.");
                Assert(hierarchyFirstPool.CountAll == 0 && hierarchyFirstPool.CountActive == 0 &&
                       hierarchyFirstPool.CountInactive == 0 && controllers.ObstacleCount == 0,
                    "Hierarchy-first Obstacle destruction left an orphaned pool or registry entry.");

                hierarchyFirstPool.Dispose();
                Assert(!hierarchyLease.IsValid,
                    "Hierarchy-first pool disposal did not invalidate the destroyed Obstacle lease.");

                GameObject invalidLeaseObject = new GameObject("__ObstacleMvcValidation_InvalidLease");
                invalidLeaseObject.SetActive(false);
                Rigidbody invalidLeaseBody = invalidLeaseObject.AddComponent<Rigidbody>();
                invalidLeaseBody.useGravity = false;
                invalidLeaseObject.AddComponent<BoxCollider>();
                AddRequiredGroundFade(invalidLeaseObject, config.Prefab);
                ObstacleView invalidLeaseView = invalidLeaseObject.AddComponent<ObstacleView>();
                resolver.InjectGameObject(invalidLeaseObject);
                controllers.Compose(invalidLeaseView);
                invalidLeaseView.OnPoolCreated(invalidLeaseView);
                Assert(controllers.TryGet(invalidLeaseView, out ObstacleController invalidLeaseController),
                    "Registry did not compose invalid-lease fixture.");
                ObstacleModel invalidLeaseModel = invalidLeaseController.Model;
                bool invalidLeaseRejected = false;
                try { invalidLeaseView.OnPoolRent(default); }
                catch (InvalidOperationException) { invalidLeaseRejected = true; }
                Assert(invalidLeaseRejected, "ObstacleView accepted an invalid PoolLease.");
                Assert(!invalidLeaseModel.IsRented && !invalidLeaseController.IsRented &&
                       invalidLeaseModel.ObserverCount == 0 && !invalidLeaseController.IsObserving,
                    "Failed Obstacle rental did not roll back Model, Controller, and View state.");
                DestroyAndAssertNoError(invalidLeaseView.gameObject,
                    "Failed-rental Obstacle destruction logged an error.");

                GameObject neverRentedObject = new GameObject("__ObstacleMvcValidation_CreatedNeverRented");
                neverRentedObject.SetActive(false);
                Rigidbody neverRentedBody = neverRentedObject.AddComponent<Rigidbody>();
                neverRentedBody.useGravity = false;
                neverRentedObject.AddComponent<BoxCollider>();
                AddRequiredGroundFade(neverRentedObject, config.Prefab);
                ObstacleView neverRentedView = neverRentedObject.AddComponent<ObstacleView>();
                resolver.InjectGameObject(neverRentedObject);
                controllers.Compose(neverRentedView);
                neverRentedView.OnPoolCreated(neverRentedView);
                Assert(controllers.TryGet(neverRentedView, out ObstacleController neverRentedController) &&
                       !neverRentedController.Model.IsRented && !neverRentedController.IsRented &&
                       neverRentedController.Model.ObserverCount == 0,
                    "Created-never-rented Obstacle bundle was not safely idle.");
                DestroyAndAssertNoError(neverRentedView.gameObject,
                    "Created-never-rented Obstacle destruction logged an error.");

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
            if (result.success) Debug.Log("[ObstacleMvcValidation] Passed " + result.assertions + " assertions.");
            else Debug.LogError("[ObstacleMvcValidation] " + result.error);
            Destroy(gameObject);
        }

        private static void AddRequiredGroundFade(GameObject target, MonoBehaviour pooledSource)
        {
            Renderer sourceRenderer = pooledSource != null ? pooledSource.GetComponent<Renderer>() : null;
            GroundFadeReturn sourceFade = pooledSource != null ? pooledSource.GetComponent<GroundFadeReturn>() : null;
            FieldInfo fadeMaterialField = typeof(GroundFadeReturn).GetField("fadeMaterial",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Material fadeMaterial = sourceFade != null ? fadeMaterialField?.GetValue(sourceFade) as Material : null;
            if (sourceRenderer == null || fadeMaterial == null)
                throw new InvalidOperationException("Obstacle MVC validation requires the pooled production GroundFadeReturn setup.");

            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            GroundFadeReturn fade = target.AddComponent<GroundFadeReturn>();
            typeof(GroundFadeReturn).GetField("targetRenderer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(fade, renderer);
            fadeMaterialField.SetValue(fade, fadeMaterial);
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
