#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Framework.Loop;
using Framework.Pool;
using InGame.Ball;
using InGame.Cannon;
using InGame.Config;
using InGame.DI;
using InGame.Obstacle;
using InGame.Presentation;
using InGame.Shot;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Framework.Test
{
    /// <summary>Play Mode evidence for the click-to-launch seam in a temporary PhysicsScene.</summary>
    public sealed class ClickLaunchRuntimeProbe : MonoBehaviour
    {
        [Serializable]
        private sealed class Result
        {
            public string recordedAtUtc;
            public string unityVersion;
            public bool success;
            public int assertions;
            public int simulatedSteps;
            public int collisionContacts;
            public bool detectedObstacleRaycast;
            public bool rejectedOutOfScreenRaycast;
            public bool rejectedDisabledObstacle;
            public bool rejectedInvalidCurveTime;
            public bool rejectedDuplicateAndStaleLaunch;
            public bool straightDisabledGravityAtOrBelowWorldZ;
            public bool collisionEnabledGravity;
            public bool worldZEnabledGravity;
            public bool continuousCollisionModesApplied;
            public bool capacityFailureWasGraceful;
            public bool timedReturnAndRerent;
            public bool authoredPitchPreserved;
            public bool cannonAimFailureRolledBack;
            public float barrelHorizontalAngleDegrees;
            public string error;
            public string scope = "Temporary camera, cannon, registry-composed pooled ball, and registry-composed pooled obstacle in an isolated LocalPhysicsMode.Physics3D scene. No saved Scene, Prefab, or ScriptableObject asset is changed.";
        }

        private readonly Result _result = new Result();
        private GameObject _fixtureRoot;
        private Scene _ownerScene;
        private PoolFactory _factory;
        private IObjectResolver _resolver;
        private WorldObjectControllerRegistry _controllers;
        private PoolConfig _ballConfig;
        private PoolConfig _obstaclePoolConfig;
        private BallConfig _ballSettings;
        private ObstacleConfig _obstacleSettings;
        private GroundFadeConfig _groundFadeSettings;
        private LoopDispatcher _loopDispatcher;
        private PhysXConfig _physXSettings;
        private ShotDirector _director;
        private string _outputPath;

        public void Begin(GameObject root, PoolContainer container, PoolConfig config, CannonView cannon,
            Camera camera, Transform obstacle, Scene scene, string output)
        {
            _fixtureRoot = root;
            _ballConfig = config;
            _ownerScene = scene;
            _outputPath = output;
            StartCoroutine(Run(container, cannon, camera, obstacle, scene.GetPhysicsScene()));
        }

        private IEnumerator Run(PoolContainer container, CannonView cannon, Camera camera, Transform obstacle,
            PhysicsScene physicsScene)
        {
            _result.recordedAtUtc = DateTime.UtcNow.ToString("O");
            _result.unityVersion = Application.unityVersion;
            try
            {
                Assert(physicsScene.IsValid(), "The temporary Physics3D scene is invalid.");
                Assert(cannon != null && camera != null && obstacle != null,
                    "Temporary click-launch fixtures are incomplete.");

                _physXSettings = ScriptableObject.CreateInstance<PhysXConfig>();
                _ballSettings = ScriptableObject.CreateInstance<BallConfig>();
                _obstacleSettings = ScriptableObject.CreateInstance<ObstacleConfig>();
                _groundFadeSettings = ScriptableObject.CreateInstance<GroundFadeConfig>();
                _loopDispatcher = new LoopDispatcher();
                _loopDispatcher.StartLoop();
                ContainerBuilder builder = new ContainerBuilder();
                builder.RegisterInstance(_ballSettings);
                builder.RegisterInstance(_obstacleSettings);
                builder.RegisterInstance(_groundFadeSettings);
                builder.RegisterInstance<ILoopEvents>(_loopDispatcher);
                _resolver = builder.Build();

                Vector3 obstaclePosition = obstacle.position;
                Quaternion obstacleRotation = obstacle.rotation;
                ConfigurePooledObstacleSource(obstacle.gameObject, _ballConfig.Prefab);
                ObstacleView obstacleSource = obstacle.GetComponent<ObstacleView>();
                _obstaclePoolConfig = CreatePoolConfig(
                    "__ClickLaunchValidation_ObstaclePool",
                    obstacleSource,
                    minPool: 1,
                    maxPool: 1);
                SetPrivateField(container, "configs", new[] { _ballConfig, _obstaclePoolConfig });

                _controllers = new WorldObjectControllerRegistry(_ballSettings, _obstacleSettings);
                _factory = new PoolFactory(container, _resolver, _controllers, retainMinimum: true);
                _factory.Initialize(startMaintenance: false);
                Assert(_factory.TryRent(
                        _obstaclePoolConfig,
                        new PoolSpawnArgs(obstaclePosition, obstacleRotation, _fixtureRoot.transform),
                        out PoolLease obstacleLease),
                    "Temporary target Obstacle rent failed.");
                ObstacleView targetView = obstacleLease.Value as ObstacleView;
                ObstacleController targetController = null;
                Assert(targetView != null && _controllers.TryGet(targetView, out targetController),
                    "Registry did not compose the temporary target Obstacle Controller.");

                Physics.SyncTransforms();
                Vector2 centerScreen = new Vector2(camera.pixelWidth * .5f, camera.pixelHeight * .5f);
                Assert(ObstacleTargetRaycaster.TryGetTarget(
                           camera,
                           centerScreen,
                           _physXSettings,
                           _controllers,
                           out ObstacleController detectedObstacle,
                           out Vector3 target)
                       && ReferenceEquals(detectedObstacle, targetController),
                    "The centre-screen ray did not resolve the registry-owned ObstacleController.");
                _result.detectedObstacleRaycast = true;
                Assert(!ObstacleTargetRaycaster.TryGetTarget(
                        camera,
                        new Vector2(-1f, centerScreen.y),
                        _physXSettings,
                        _controllers,
                        out _,
                        out _),
                    "Obstacle raycast accepted an out-of-screen point.");
                _result.rejectedOutOfScreenRaycast = true;

                targetView.enabled = false;
                Assert(!ObstacleTargetRaycaster.TryGetTarget(
                        camera,
                        centerScreen,
                        _physXSettings,
                        _controllers,
                        out _,
                        out _),
                    "Obstacle raycast accepted a disabled ObstacleView.");
                _result.rejectedDisabledObstacle = true;
                targetView.enabled = true;

                Vector3 gravity = _physXSettings.Gravity;
                Assert(!BallLaunchVelocity.TryCalculate(
                        cannon.MuzzlePosition,
                        target,
                        BallTrajectoryMode.Curve,
                        0f,
                        0f,
                        gravity,
                        out _),
                    "BallLaunchVelocity accepted a non-positive Curve flight time.");
                _result.rejectedInvalidCurveTime = true;
                const float curveFlightSeconds = .55f;
                Assert(BallLaunchVelocity.TryCalculate(
                        cannon.MuzzlePosition,
                        target,
                        BallTrajectoryMode.Curve,
                        0f,
                        curveFlightSeconds,
                        gravity,
                        out Vector3 curveVelocity),
                    "BallLaunchVelocity rejected a valid fixed-flight-time Curve.");
                Vector3 curveArrival = cannon.MuzzlePosition + curveVelocity * curveFlightSeconds
                    + .5f * gravity * curveFlightSeconds * curveFlightSeconds;
                Assert(Vector3.Distance(curveArrival, target) < .001f,
                    "Fixed-flight-time Curve did not resolve to the target.");

                _director = new ShotDirector(
                    _factory,
                    _ballConfig,
                    cannon,
                    _fixtureRoot.transform,
                    _ballSettings,
                    _physXSettings,
                    _controllers);
                Transform pitch = cannon.transform.Find("YawRoot/PitchPivot");
                Assert(pitch != null, "Temporary Cannon pitch fixture is missing.");
                Quaternion authoredPitch = pitch.localRotation;
                Assert(_controllers.TryGet(cannon, out CannonController cannonController),
                    "Registry did not own the ShotDirector CannonController.");
                Vector3 authoredAim = cannonController.Model.AimDirection;
                try
                {
                    pitch.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Expect<InvalidOperationException>(
                        () => _director.TryFire(target),
                        "An unrenderable vertical barrel accepted the first aim command.");
                    Expect<InvalidOperationException>(
                        () => _director.TryFire(target),
                        "The same failed aim direction bypassed Cannon rendering on retry.");
                    Assert(_director.ActiveShotCount == 0
                           && _factory.GetPool(_ballConfig).CountActive == 0
                           && Vector3.Angle(cannonController.Model.AimDirection, authoredAim) < .001f,
                        "Failed Cannon rendering advanced a shot or left CannonModel drift.");
                    _result.cannonAimFailureRolledBack = true;
                }
                finally
                {
                    pitch.localRotation = authoredPitch;
                }

                Assert(_director.TryFire(target),
                    "ShotDirector rejected the valid Obstacle raycast target.");
                Assert(_director.ActiveShotCount == 1,
                    "A valid fire did not create exactly one active shot.");
                Assert(!_factory.TryRent(
                        _ballConfig,
                        new PoolSpawnArgs(Vector3.zero, Quaternion.identity),
                        out PoolLease capacityLease),
                    "Pool capacity did not reject a second simultaneous Ball rent.");
                _result.capacityFailureWasGraceful = true;
                Assert(!_director.TryFire(target),
                    "ShotDirector accepted a duplicate fire while the one-Ball pool was exhausted.");

                BallView ball = FindActiveBall();
                BallController ballController = null;
                Assert(ball != null && _controllers.TryGet(ball, out ballController),
                    "ShotDirector did not produce a registry-owned active BallController.");
                Rigidbody body = ball.GetComponent<Rigidbody>();
                uint collisionEpoch = ballController.RentalEpoch;
                Assert(body != null && ballController.HasLaunched,
                    "Active Ball was not launched through BallController.");
                Assert(body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic,
                    "BallConfig did not apply ContinuousDynamic collision detection to the rented Ball.");
                Rigidbody obstacleBody = targetView.GetComponent<Rigidbody>();
                Assert(obstacleBody != null
                       && obstacleBody.collisionDetectionMode == CollisionDetectionMode.Continuous,
                    "ObstacleConfig did not apply Continuous collision detection to the Obstacle.");
                _result.continuousCollisionModesApplied = true;
                Assert(!body.useGravity && body.position.z <= _ballSettings.GravityActivationWorldZ,
                    "Straight launch did not keep gravity disabled at or below the world-Z threshold.");
                _result.straightDisabledGravityAtOrBelowWorldZ = true;
                Vector3 velocity = body.linearVelocity;
                Assert(BallLaunchVelocity.TryCalculate(
                        cannon.MuzzlePosition,
                        target,
                        BallTrajectoryMode.Straight,
                        _ballSettings.LaunchSpeed,
                        _ballSettings.CurveFlightSeconds,
                        _physXSettings.Gravity,
                        out Vector3 expectedVelocity),
                    "The valid target unexpectedly has no Straight launch velocity.");
                Assert(Vector3.Angle(expectedVelocity, velocity) < .5f,
                    "ShotDirector velocity differs from BallLaunchVelocity output.");
                Vector3 barrelHorizontal = Vector3.ProjectOnPlane(cannon.BarrelDirection, Vector3.up);
                Vector3 targetHorizontal = Vector3.ProjectOnPlane(target - cannon.MuzzlePosition, Vector3.up);
                _result.barrelHorizontalAngleDegrees = Vector3.Angle(barrelHorizontal, targetHorizontal);
                Assert(_result.barrelHorizontalAngleDegrees < 1f,
                    "Cannon barrel did not align horizontally with the target direction.");
                Assert(Quaternion.Angle(authoredPitch, pitch.localRotation) < .01f,
                    "Cannon aiming changed the authored child pitch rotation.");
                _result.authoredPitchPreserved = true;

                ClickLaunchCollisionRecorder recorder = targetView.GetComponent<ClickLaunchCollisionRecorder>();
                Assert(recorder != null, "Temporary Obstacle has no collision recorder.");
                for (int i = 0; i < 100 && recorder.contacts == 0; i++)
                {
                    physicsScene.Simulate(.02f);
                    _director.Tick(.02f);
                    _result.simulatedSteps++;
                }
                _result.collisionContacts = recorder.contacts;
                Assert(recorder.contacts > 0,
                    "The launched Ball did not collide with the temporary PhysX Obstacle.");
                Assert(body.useGravity,
                    "A direct Obstacle collision did not enable Straight Ball gravity.");
                _result.collisionEnabledGravity = true;

                _director.Tick(_ballSettings.MaxLifetimeSeconds + .1f);
                Assert(_director.ActiveShotCount == 0,
                    "Timed shot recycling did not return the active Ball.");
                Assert(_director.TryFire(target),
                    "Timed return did not make the Ball available for re-rent.");
                _result.timedReturnAndRerent = true;
                ball = FindActiveBall();
                body = ball != null ? ball.GetComponent<Rigidbody>() : null;
                BallController rerentedBallController = null;
                Assert(ball != null && body != null
                       && _controllers.TryGet(ball, out rerentedBallController)
                       && rerentedBallController.HasLaunched
                       && !body.useGravity,
                    "Separate Straight rental did not begin with gravity disabled.");
                body.position = new Vector3(
                    body.position.x,
                    body.position.y,
                    _ballSettings.GravityActivationWorldZ);
                _director.TickPhysics();
                Assert(!body.useGravity,
                    "Ball gravity enabled at the world-Z threshold instead of after it.");
                Assert(!rerentedBallController.TryActivateGravityFromObstacleCollision(collisionEpoch)
                       && !body.useGravity,
                    "A stale collision epoch enabled gravity for the current rental.");
                body.position = new Vector3(
                    body.position.x,
                    body.position.y,
                    _ballSettings.GravityActivationWorldZ + .01f);
                _director.TickPhysics();
                Assert(body.useGravity,
                    "ShotDirector did not enable Ball gravity after world Z crossed the threshold.");
                _result.worldZEnabledGravity = true;
                Assert(!rerentedBallController.TryLaunch(
                           rerentedBallController.RentalEpoch - 1u,
                           Vector3.right,
                           BallTrajectoryMode.Straight)
                       && !rerentedBallController.TryLaunch(
                           rerentedBallController.RentalEpoch,
                           Vector3.right,
                           BallTrajectoryMode.Straight),
                    "A stale or duplicate launch affected the re-rented Ball.");
                _result.rejectedDuplicateAndStaleLaunch = true;
                _director.Tick(_ballSettings.MaxLifetimeSeconds + .1f);
                Assert(_director.ActiveShotCount == 0,
                    "Final timed return left an active shot behind.");
                _result.success = true;
            }
            catch (Exception exception)
            {
                _result.success = false;
                _result.error = exception.ToString();
            }
            finally
            {
                try { _director?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("ShotDirector", exception); }
                try { _factory?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("PoolFactory", exception); }
                try { _controllers?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("Controller registry", exception); }
                try { _resolver?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("Resolver", exception); }
                try { if (_fixtureRoot != null) Destroy(_fixtureRoot); }
                catch (Exception exception) { RecordCleanupFailure("Fixture", exception); }
                try { if (_ballConfig != null) Destroy(_ballConfig); }
                catch (Exception exception) { RecordCleanupFailure("Ball PoolConfig", exception); }
                try { if (_obstaclePoolConfig != null) Destroy(_obstaclePoolConfig); }
                catch (Exception exception) { RecordCleanupFailure("Obstacle PoolConfig", exception); }
                try { if (_ballSettings != null) Destroy(_ballSettings); }
                catch (Exception exception) { RecordCleanupFailure("BallConfig", exception); }
                try { if (_obstacleSettings != null) Destroy(_obstacleSettings); }
                catch (Exception exception) { RecordCleanupFailure("ObstacleConfig", exception); }
                try { _loopDispatcher?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("LoopDispatcher", exception); }
                try { if (_groundFadeSettings != null) Destroy(_groundFadeSettings); }
                catch (Exception exception) { RecordCleanupFailure("GroundFadeConfig", exception); }
                try { if (_physXSettings != null) Destroy(_physXSettings); }
                catch (Exception exception) { RecordCleanupFailure("PhysXConfig", exception); }
            }

            yield return null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_outputPath));
                File.WriteAllText(_outputPath, JsonUtility.ToJson(_result, true));
                if (_result.success) Debug.Log("[ClickLaunchValidation] Passed " + _result.assertions + " assertions.");
                else Debug.LogError("[ClickLaunchValidation] " + _result.error);
            }
            catch (Exception exception)
            {
                Debug.LogError("[ClickLaunchValidation] Evidence write failed: " + exception);
            }
            finally
            {
                if (_ownerScene.IsValid()) SceneManager.UnloadSceneAsync(_ownerScene);
                Destroy(gameObject);
            }
        }

        private BallView FindActiveBall()
        {
            BallView[] balls = _fixtureRoot.GetComponentsInChildren<BallView>(true);
            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i].gameObject.activeInHierarchy) return balls[i];
            }
            return null;
        }

        private static void ConfigurePooledObstacleSource(GameObject target, MonoBehaviour pooledBallSource)
        {
            Renderer sourceRenderer = pooledBallSource != null ? pooledBallSource.GetComponent<Renderer>() : null;
            GroundFadeReturn sourceFade = pooledBallSource != null
                ? pooledBallSource.GetComponent<GroundFadeReturn>()
                : null;
            FieldInfo fadeMaterialField = typeof(GroundFadeReturn).GetField(
                "fadeMaterial",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Material fadeMaterial = sourceFade != null
                ? fadeMaterialField?.GetValue(sourceFade) as Material
                : null;
            Renderer targetRenderer = target != null ? target.GetComponent<Renderer>() : null;
            if (target == null || sourceRenderer == null || fadeMaterial == null || targetRenderer == null)
            {
                throw new InvalidOperationException(
                    "Click-launch validation requires the temporary Ball source GroundFadeReturn setup and an Obstacle Renderer.");
            }

            target.SetActive(false);
            targetRenderer.sharedMaterial = fadeMaterial;
            GroundFadeReturn fade = target.AddComponent<GroundFadeReturn>();
            SetPrivateField(fade, "targetRenderer", targetRenderer);
            SetPrivateField(fade, "fadeMaterial", fadeMaterial);
        }

        private static PoolConfig CreatePoolConfig(string rootName, MonoBehaviour prefab, int minPool, int maxPool)
        {
            PoolConfig config = ScriptableObject.CreateInstance<PoolConfig>();
            SetPrivateField(config, "poolRootName", rootName);
            SetPrivateField(config, "prefab", prefab);
            SetPrivateField(config, "minPool", minPool);
            SetPrivateField(config, "maxPool", maxPool);
            SetPrivateField(config, "returnDelaySeconds", 0f);
            config.Validate();
            return config;
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().FullName, fieldName);
            field.SetValue(target, value);
        }

        private void Assert(bool condition, string message)
        {
            _result.assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Expect<T>(Action action, string message) where T : Exception
        {
            try { action(); }
            catch (T)
            {
                _result.assertions++;
                return;
            }
            throw new InvalidOperationException(message);
        }

        private void RecordCleanupFailure(string label, Exception exception)
        {
            _result.success = false;
            _result.error = (_result.error ?? string.Empty) + "\nCleanup " + label + ": " + exception;
        }
    }

    public sealed class ClickLaunchCollisionRecorder : MonoBehaviour
    {
        public int contacts { get; private set; }
        private void OnCollisionEnter(Collision collision) => contacts++;
    }
}
#endif
