#if UNITY_EDITOR || SMESH_VALIDATION
using System;
using System.Collections;
using System.IO;
using Framework.Loop;
using Framework.Pool;
using InGame.Ball;
using InGame.Config;
using InGame.DI;
using InGame.Obstacle;
using InGame.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace Framework.Test
{
    public sealed class PhysXCollisionRecorder : MonoBehaviour
    {
        public int contacts;
        private void OnCollisionEnter(Collision collision) => contacts++;
    }

    /// <summary>Play Mode evidence for pooled Ball/Obstacle PhysX lifecycle in a local PhysicsScene.</summary>
    public sealed class PhysXRuntimeProbe : MonoBehaviour
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
            public float obstacleDisplacement;
            public bool initialBallSleeping;
            public bool initialObstacleSleeping;
            public bool collisionEnabledGravity;
            public bool worldZEnabledGravity;
            public bool registryDisposeInvalidatedActiveLeases;
            public bool controllerlessRerentQuarantined;
            public string error;
            public string scope;
        }

        private readonly Result _result = new Result
        {
            scope = "Temporary inactive sources plus in-memory PoolConfig, BallConfig, and ObstacleConfig instances in an isolated LocalPhysicsMode.Physics3D scene. No saved Scene, Prefab, PoolConfig, or ScriptableObject asset is edited."
        };

        private GameObject _fixtureRoot;
        private PoolFactory _factory;
        private IObjectResolver _resolver;
        private WorldObjectControllerRegistry _controllers;
        private string _outputPath;
        private Scene _physicsSceneOwner;

        public void Begin(GameObject root, PoolContainer container, PoolConfig ballConfig, PoolConfig obstacleConfig,
            Scene isolatedScene, string output)
        {
            _fixtureRoot = root;
            _physicsSceneOwner = isolatedScene;
            _outputPath = output;
            StartCoroutine(Run(container, ballConfig, obstacleConfig, isolatedScene.GetPhysicsScene()));
        }

        private IEnumerator Run(PoolContainer container, PoolConfig ballConfig, PoolConfig obstacleConfig,
            PhysicsScene physicsScene)
        {
            _result.recordedAtUtc = DateTime.UtcNow.ToString("O");
            _result.unityVersion = Application.unityVersion;
            BallConfig ballSettings = null;
            ObstacleConfig obstacleSettings = null;
            GroundFadeConfig groundFadeSettings = null;
            LoopDispatcher loopDispatcher = null;
            try
            {
                Assert(physicsScene.IsValid(), "The local Physics3D scene did not provide a valid PhysicsScene.");
                Rigidbody ballSourceBody = ballConfig != null && ballConfig.Prefab != null
                    ? ballConfig.Prefab.GetComponent<Rigidbody>()
                    : null;
                Rigidbody obstacleSourceBody = obstacleConfig != null && obstacleConfig.Prefab != null
                    ? obstacleConfig.Prefab.GetComponent<Rigidbody>()
                    : null;
                Assert(ballSourceBody != null && obstacleSourceBody != null,
                    "The temporary PoolConfig sources did not retain their Rigidbody components.");
                RigidbodyState ballInspector = new RigidbodyState(ballSourceBody);
                RigidbodyState obstacleInspector = new RigidbodyState(obstacleSourceBody);

                ballSettings = ScriptableObject.CreateInstance<BallConfig>();
                obstacleSettings = ScriptableObject.CreateInstance<ObstacleConfig>();
                groundFadeSettings = ScriptableObject.CreateInstance<GroundFadeConfig>();
                loopDispatcher = new LoopDispatcher();
                loopDispatcher.StartLoop();
                ContainerBuilder builder = new ContainerBuilder();
                builder.RegisterInstance(ballSettings);
                builder.RegisterInstance(obstacleSettings);
                builder.RegisterInstance(groundFadeSettings);
                builder.RegisterInstance<ILoopEvents>(loopDispatcher);
                _resolver = builder.Build();
                _controllers = new WorldObjectControllerRegistry(ballSettings, obstacleSettings);
                AssertKinematicSourceContracts(_controllers);
                _factory = new PoolFactory(container, _resolver, _controllers, retainMinimum: true);
                _factory.Initialize(startMaintenance: false);

                Assert(_factory.TryRent(ballConfig,
                        new PoolSpawnArgs(new Vector3(-3f, 0f, -.25f), Quaternion.identity),
                        out PoolLease ballLease),
                    "Ball rent failed.");
                Assert(_factory.TryRent(obstacleConfig,
                        new PoolSpawnArgs(new Vector3(0f, 0f, -.25f), Quaternion.identity),
                        out PoolLease obstacleLease),
                    "Obstacle rent failed.");

                BallView ball = ballLease.Value as BallView;
                ObstacleView obstacle = obstacleLease.Value as ObstacleView;
                Assert(ball != null && obstacle != null,
                    "PoolFactory did not return the expected BallView and ObstacleView.");
                BallController ballController = null;
                ObstacleController obstacleController = null;
                Assert(_controllers.TryGet(ball, out ballController)
                       && _controllers.TryGet(obstacle, out obstacleController),
                    "WorldObjectControllerRegistry did not own the rented Ball/Obstacle Controllers.");
                Rigidbody ballBody = ball.GetComponent<Rigidbody>();
                Rigidbody obstacleBody = obstacle.GetComponent<Rigidbody>();
                Assert(ballBody != null && obstacleBody != null && ball.GetComponent<Collider>() != null
                       && obstacle.GetComponent<Collider>() != null,
                    "Production views did not retain required root Rigidbody and Collider components.");
                AssertConfiguredPhysics(ballBody, ballSettings, "Ball first rent");
                AssertConfiguredPhysics(obstacleBody, obstacleSettings, "Obstacle first rent");
                ballInspector.AssertNonConfigUnchanged(ballBody, Assert, "Ball first rent");
                obstacleInspector.AssertNonConfigUnchanged(obstacleBody, Assert, "Obstacle first rent");
                Assert(ballController.IsCurrentRental(ballController.RentalEpoch)
                       && obstacleController.IsCurrentRental(obstacleController.RentalEpoch),
                    "The initial rental epochs are not current.");
                Assert(!ballBody.isKinematic && !obstacleBody.isKinematic,
                    "The PhysX lifecycle accepted a non-dynamic Rigidbody.");
                _result.initialBallSleeping = ballBody.IsSleeping();
                _result.initialObstacleSleeping = obstacleBody.IsSleeping();
                Assert(!ballController.HasLaunched, "Ball rent incorrectly began in a launched state.");
                Assert(IsZero(ballBody.linearVelocity) && IsZero(ballBody.angularVelocity),
                    "Ball rent did not clear its linear and angular velocity.");
                Assert(_result.initialBallSleeping,
                    "Ball activation did not preserve the pre-launch sleep contract.");
                Assert(IsZero(obstacleBody.linearVelocity) && IsZero(obstacleBody.angularVelocity),
                    "Obstacle rent did not clear its linear and angular velocity.");
                Assert(!_result.initialObstacleSleeping,
                    "Obstacle activation did not wake the reset body.");

                uint ballEpoch = ballController.RentalEpoch;
                uint obstacleEpoch = obstacleController.RentalEpoch;
                Assert(!ballController.TryLaunch(ballEpoch - 1u, Vector3.right, BallTrajectoryMode.Straight),
                    "Ball accepted a stale epoch launch.");
                Assert(!ballController.TryLaunch(ballEpoch, Vector3.zero, BallTrajectoryMode.Straight),
                    "Ball accepted a zero launch velocity.");
                Assert(!ballController.TryLaunch(ballEpoch, new Vector3(float.NaN, 0f, 0f), BallTrajectoryMode.Straight),
                    "Ball accepted a non-finite launch velocity.");
                Assert(!ballController.TryLaunch(ballEpoch, Vector3.right, (BallTrajectoryMode)(-1)),
                    "Ball accepted an unknown trajectory mode.");
                bool originalKinematic = ballBody.isKinematic;
                try
                {
                    ballBody.isKinematic = true;
                    Assert(!ballController.TryLaunch(ballEpoch, Vector3.right, BallTrajectoryMode.Straight),
                        "Ball accepted a kinematic launch.");
                }
                finally
                {
                    ballBody.isKinematic = originalKinematic;
                }

                Vector3 obstacleStart = obstacleBody.position;
                Assert(ballController.TryLaunch(ballEpoch, new Vector3(12f, 0f, 0f), BallTrajectoryMode.Straight),
                    "Ball rejected a valid Straight launch.");
                Assert(ballController.HasLaunched && !ballBody.IsSleeping() && !ballBody.useGravity,
                    "Straight Ball launch did not wake the body with gravity disabled.");
                Assert(!ballController.TryLaunch(ballEpoch, Vector3.right, BallTrajectoryMode.Straight),
                    "Ball accepted a duplicate launch in the same epoch.");

                PhysXCollisionRecorder contact = obstacle.GetComponent<PhysXCollisionRecorder>();
                Assert(contact != null, "Obstacle source did not provide its temporary collision recorder.");
                for (int i = 0; i < 60 && contact.contacts == 0; i++)
                {
                    physicsScene.Simulate(.02f);
                    _result.simulatedSteps++;
                }
                Assert(contact.contacts > 0,
                    "Ball launch did not produce OnCollisionEnter contact with Obstacle.");
                for (int i = 0; i < 5; i++)
                {
                    physicsScene.Simulate(.02f);
                    _result.simulatedSteps++;
                }
                _result.collisionContacts = contact.contacts;
                _result.obstacleDisplacement = Vector3.Distance(obstacleBody.position, obstacleStart);
                Assert(ballBody.useGravity,
                    "A direct Obstacle collision did not enable Straight Ball gravity.");
                _result.collisionEnabledGravity = true;
                Assert(_result.obstacleDisplacement > .01f,
                    "Collision did not physically move the dynamic Obstacle.");

                Assert(ballController.TryReturn(ballEpoch) && obstacleController.TryReturn(obstacleEpoch),
                    "Current rental return failed.");
                Assert(!ball.gameObject.activeSelf && !obstacle.gameObject.activeSelf
                       && IsZero(ballBody.linearVelocity) && IsZero(ballBody.angularVelocity)
                       && IsZero(obstacleBody.linearVelocity) && IsZero(obstacleBody.angularVelocity)
                       && ballBody.IsSleeping() && obstacleBody.IsSleeping(),
                    "Return did not deactivate, clear velocity, and sleep both bodies.");
                ballInspector.AssertNonConfigUnchanged(ballBody, Assert, "Ball");
                obstacleInspector.AssertNonConfigUnchanged(obstacleBody, Assert, "Obstacle");

                Assert(_factory.TryRent(ballConfig,
                        new PoolSpawnArgs(Vector3.zero, Quaternion.identity),
                        out PoolLease nextBallLease),
                    "Ball re-rent failed.");
                Assert(_factory.TryRent(obstacleConfig,
                        new PoolSpawnArgs(new Vector3(4f, 0f, 0f), Quaternion.identity),
                        out PoolLease nextObstacleLease),
                    "Obstacle re-rent failed.");
                BallView nextBall = nextBallLease.Value as BallView;
                ObstacleView nextObstacle = nextObstacleLease.Value as ObstacleView;
                Assert(ReferenceEquals(nextBall, ball) && ReferenceEquals(nextObstacle, obstacle),
                    "Re-rent did not reuse the same pooled body instances.");
                BallController rerentedBallController = null;
                ObstacleController rerentedObstacleController = null;
                Assert(_controllers.TryGet(nextBall, out rerentedBallController)
                       && _controllers.TryGet(nextObstacle, out rerentedObstacleController)
                       && ReferenceEquals(rerentedBallController, ballController)
                       && ReferenceEquals(rerentedObstacleController, obstacleController),
                    "Re-rent did not reuse the same registry-owned Controllers.");
                Assert(rerentedBallController.RentalEpoch != ballEpoch
                       && rerentedObstacleController.RentalEpoch != obstacleEpoch,
                    "Re-rent did not advance the lifecycle epochs.");
                Assert(!rerentedBallController.HasLaunched
                       && IsZero(ballBody.linearVelocity) && IsZero(ballBody.angularVelocity)
                       && ballBody.IsSleeping()
                       && IsZero(obstacleBody.linearVelocity) && IsZero(obstacleBody.angularVelocity)
                       && !obstacleBody.IsSleeping(),
                    "Re-rent did not restore cleared velocity, Ball sleep, and Obstacle wake state.");
                AssertConfiguredPhysics(ballBody, ballSettings, "Ball re-rent");
                AssertConfiguredPhysics(obstacleBody, obstacleSettings, "Obstacle re-rent");
                Assert(!ballLease.Return() && !obstacleLease.Return()
                       && !rerentedBallController.TryLaunch(ballEpoch, Vector3.right, BallTrajectoryMode.Straight)
                       && nextBallLease.IsValid && nextObstacleLease.IsValid,
                    "An old lease or epoch disturbed the newer rental.");

                uint worldZBallEpoch = rerentedBallController.RentalEpoch;
                Assert(rerentedBallController.TryLaunch(worldZBallEpoch, Vector3.right, BallTrajectoryMode.Straight)
                       && !ballBody.useGravity,
                    "Separate Straight rental did not begin with gravity disabled.");
                ballBody.position = new Vector3(
                    ballBody.position.x,
                    ballBody.position.y,
                    ballSettings.GravityActivationWorldZ);
                rerentedBallController.TickPhysics(worldZBallEpoch, ballSettings.GravityActivationWorldZ);
                Assert(!ballBody.useGravity,
                    "Straight Ball enabled gravity at world Z equal to the configured threshold.");
                Assert(!rerentedBallController.TryActivateGravityFromObstacleCollision(ballEpoch) && !ballBody.useGravity,
                    "A stale collision epoch enabled gravity for the current rental.");
                ballBody.position = new Vector3(
                    ballBody.position.x,
                    ballBody.position.y,
                    ballSettings.GravityActivationWorldZ + .01f);
                rerentedBallController.TickPhysics(worldZBallEpoch, ballSettings.GravityActivationWorldZ);
                Assert(ballBody.useGravity,
                    "Straight Ball did not enable gravity after world Z crossed the configured threshold.");
                _result.worldZEnabledGravity = true;

                Assert(rerentedBallController.TryReturn(worldZBallEpoch)
                       && rerentedObstacleController.TryReturn(rerentedObstacleController.RentalEpoch),
                    "Separate Straight world-Z rental return failed.");
                Assert(_factory.TryRent(ballConfig,
                           new PoolSpawnArgs(Vector3.zero, Quaternion.identity),
                           out nextBallLease)
                       && _factory.TryRent(obstacleConfig,
                           new PoolSpawnArgs(new Vector3(4f, 0f, 0f), Quaternion.identity),
                           out nextObstacleLease),
                    "Curve verification re-rent failed.");
                nextBall = nextBallLease.Value as BallView;
                nextObstacle = nextObstacleLease.Value as ObstacleView;
                Assert(nextBall != null && nextObstacle != null,
                    "Curve verification re-rent returned an unexpected View.");
                BallController activeBallController = null;
                ObstacleController activeObstacleController = null;
                Assert(_controllers.TryGet(nextBall, out activeBallController)
                       && _controllers.TryGet(nextObstacle, out activeObstacleController),
                    "Curve verification could not resolve registry-owned Controllers.");
                BallModel activeBallModel = activeBallController.Model;
                ObstacleModel activeObstacleModel = activeObstacleController.Model;
                uint activeBallEpoch = activeBallController.RentalEpoch;
                uint activeObstacleEpoch = activeObstacleController.RentalEpoch;
                Assert(activeBallController.TryLaunch(
                           activeBallEpoch,
                           new Vector3(3f, 1f, 0f),
                           BallTrajectoryMode.Curve)
                       && ballBody.useGravity,
                    "Curve Ball launch before active PoolFactory disposal did not enable gravity.");
                obstacleBody.linearVelocity = new Vector3(-2f, .5f, 0f);
                obstacleBody.angularVelocity = new Vector3(0f, 1f, 0f);
                obstacleBody.WakeUp();

                _factory.Dispose();
                _factory = null;
                Assert(!nextBallLease.IsValid && !nextObstacleLease.IsValid
                       && !activeBallModel.IsRented && !activeObstacleModel.IsRented
                       && !activeBallController.IsCurrentRental(activeBallEpoch)
                       && !activeObstacleController.IsCurrentRental(activeObstacleEpoch)
                       && activeBallController.IsDisposed && activeObstacleController.IsDisposed
                       && !_controllers.TryGet(nextBall, out _)
                       && !_controllers.TryGet(nextObstacle, out _),
                    "Active PoolFactory disposal left a lease or registry-owned Ball/Obstacle rental alive.");
                Assert(!nextBall.gameObject.activeSelf && !nextObstacle.gameObject.activeSelf
                       && IsZero(ballBody.linearVelocity) && IsZero(ballBody.angularVelocity)
                       && IsZero(obstacleBody.linearVelocity) && IsZero(obstacleBody.angularVelocity)
                       && ballBody.IsSleeping() && obstacleBody.IsSleeping(),
                    "Active PoolFactory disposal did not deactivate, clear, and sleep both bodies.");
                ballInspector.AssertNonConfigUnchanged(ballBody, Assert, "Ball");
                obstacleInspector.AssertNonConfigUnchanged(obstacleBody, Assert, "Obstacle");

                _controllers.Dispose();
                _controllers = null;
                CheckRegistryDisposeWhileRented(
                    container,
                    ballConfig,
                    obstacleConfig,
                    ballSettings,
                    obstacleSettings);
                _result.success = true;
            }
            catch (Exception exception)
            {
                _result.success = false;
                _result.error = exception.ToString();
            }
            finally
            {
                try { _factory?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("PoolFactory", exception); }
                try { _controllers?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("Controller registry", exception); }
                try { _resolver?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("Resolver", exception); }
                try { if (ballSettings != null) Destroy(ballSettings); }
                catch (Exception exception) { RecordCleanupFailure("BallConfig", exception); }
                try { if (obstacleSettings != null) Destroy(obstacleSettings); }
                catch (Exception exception) { RecordCleanupFailure("ObstacleConfig", exception); }
                try { loopDispatcher?.Dispose(); }
                catch (Exception exception) { RecordCleanupFailure("LoopDispatcher", exception); }
                try { if (groundFadeSettings != null) Destroy(groundFadeSettings); }
                catch (Exception exception) { RecordCleanupFailure("GroundFadeConfig", exception); }
                try { if (_fixtureRoot != null) Destroy(_fixtureRoot); }
                catch (Exception exception) { RecordCleanupFailure("Fixture root", exception); }
                try { if (ballConfig != null) Destroy(ballConfig); }
                catch (Exception exception) { RecordCleanupFailure("Ball PoolConfig", exception); }
                try { if (obstacleConfig != null) Destroy(obstacleConfig); }
                catch (Exception exception) { RecordCleanupFailure("Obstacle PoolConfig", exception); }
            }

            yield return null;
            try
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_outputPath));
                    File.WriteAllText(_outputPath, JsonUtility.ToJson(_result, true));
                }
                catch (Exception exception)
                {
                    _result.success = false;
                    _result.error = (_result.error ?? string.Empty) + "\nEvidence write: " + exception;
                }

                if (_result.success) Debug.Log("[PhysXValidation] Passed " + _result.assertions + " assertions.");
                else Debug.LogError("[PhysXValidation] " + _result.error);
            }
            finally
            {
                if (_physicsSceneOwner.IsValid()) SceneManager.UnloadSceneAsync(_physicsSceneOwner);
                Destroy(gameObject);
            }
        }

        private void CheckRegistryDisposeWhileRented(PoolContainer container, PoolConfig ballConfig,
            PoolConfig obstacleConfig, BallConfig ballSettings, ObstacleConfig obstacleSettings)
        {
            _controllers = new WorldObjectControllerRegistry(ballSettings, obstacleSettings);
            _factory = new PoolFactory(container, _resolver, _controllers, retainMinimum: true);
            _factory.Initialize(startMaintenance: false);
            Assert(_factory.TryRent(ballConfig,
                    new PoolSpawnArgs(Vector3.zero, Quaternion.identity),
                    out PoolLease ballLease),
                "Registry-disposal fixture could not rent Ball.");
            Assert(_factory.TryRent(obstacleConfig,
                    new PoolSpawnArgs(Vector3.right, Quaternion.identity),
                    out PoolLease obstacleLease),
                "Registry-disposal fixture could not rent Obstacle.");
            BallView ball = ballLease.Value as BallView;
            ObstacleView obstacle = obstacleLease.Value as ObstacleView;
            BallController ballController = null;
            ObstacleController obstacleController = null;
            Assert(ball != null && obstacle != null
                   && _controllers.TryGet(ball, out ballController)
                   && _controllers.TryGet(obstacle, out obstacleController),
                "Registry-disposal fixture did not compose both Controllers.");
            BallModel ballModel = ballController.Model;
            ObstacleModel obstacleModel = obstacleController.Model;

            _controllers.Dispose();
            _controllers = null;
            Assert(!ballLease.IsValid && !obstacleLease.IsValid
                   && ballController.IsDisposed && obstacleController.IsDisposed
                   && !ballModel.IsRented && !obstacleModel.IsRented
                   && ballModel.ObserverCount == 0 && obstacleModel.ObserverCount == 0,
                "Registry disposal did not return active leases before detaching Controllers.");
            _result.registryDisposeInvalidatedActiveLeases = true;

            PoolLease unexpectedBallLease = default;
            PoolLease unexpectedObstacleLease = default;
            Expect<InvalidOperationException>(
                () => _factory.TryRent(
                    ballConfig,
                    new PoolSpawnArgs(Vector3.zero, Quaternion.identity),
                    out unexpectedBallLease),
                "Controllerless Ball re-rent did not fail loudly.");
            Assert(!unexpectedBallLease.IsValid && _factory.GetPool(ballConfig).CountAll == 0,
                "Controllerless Ball re-rent yielded a lease or escaped quarantine.");
            Expect<InvalidOperationException>(
                () => _factory.TryRent(
                    obstacleConfig,
                    new PoolSpawnArgs(Vector3.right, Quaternion.identity),
                    out unexpectedObstacleLease),
                "Controllerless Obstacle re-rent did not fail loudly.");
            Assert(!unexpectedObstacleLease.IsValid && _factory.GetPool(obstacleConfig).CountAll == 0,
                "Controllerless Obstacle re-rent yielded a lease or escaped quarantine.");
            _result.controllerlessRerentQuarantined = true;

            _factory.Dispose();
            _factory = null;
        }

        private void AssertKinematicSourceContracts(WorldObjectControllerRegistry controllers)
        {
            GameObject ballSource = new GameObject("__PhysXValidation_KinematicBall");
            ballSource.transform.SetParent(_fixtureRoot.transform, false);
            ballSource.SetActive(false);
            Rigidbody ballBody = ballSource.AddComponent<Rigidbody>();
            ballBody.isKinematic = true;
            ballSource.AddComponent<SphereCollider>();
            ballSource.AddComponent<GroundFadeReturn>();
            BallView ballView = ballSource.AddComponent<BallView>();
            bool ballRejected = false;
            try { controllers.Compose(ballView); }
            catch (InvalidOperationException) { ballRejected = true; }
            Assert(ballRejected && !controllers.TryGet(ballView, out _),
                "Registry did not reject a kinematic Ball Rigidbody before retaining a Controller.");
            Destroy(ballSource);

            GameObject obstacleSource = new GameObject("__PhysXValidation_KinematicObstacle");
            obstacleSource.transform.SetParent(_fixtureRoot.transform, false);
            obstacleSource.SetActive(false);
            Rigidbody obstacleBody = obstacleSource.AddComponent<Rigidbody>();
            obstacleBody.isKinematic = true;
            obstacleSource.AddComponent<BoxCollider>();
            obstacleSource.AddComponent<GroundFadeReturn>();
            ObstacleView obstacleView = obstacleSource.AddComponent<ObstacleView>();
            bool obstacleRejected = false;
            try { controllers.Compose(obstacleView); }
            catch (InvalidOperationException) { obstacleRejected = true; }
            Assert(obstacleRejected && !controllers.TryGet(obstacleView, out _),
                "Registry did not reject a kinematic Obstacle Rigidbody before retaining a Controller.");
            Destroy(obstacleSource);
        }

        private static bool IsZero(Vector3 value) => value.sqrMagnitude < .000001f;

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

        private void AssertConfiguredPhysics(Rigidbody body, BallConfig settings, string label)
        {
            Assert(Mathf.Approximately(body.mass, settings.Mass)
                   && Mathf.Approximately(body.linearDamping, settings.LinearDamping)
                   && Mathf.Approximately(body.angularDamping, settings.AngularDamping)
                   && body.interpolation == settings.Interpolation
                   && body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic,
                label + " did not apply BallConfig-owned Rigidbody settings, including ContinuousDynamic collision detection.");
        }

        private void AssertConfiguredPhysics(Rigidbody body, ObstacleConfig settings, string label)
        {
            Assert(Mathf.Approximately(body.mass, settings.Mass)
                   && Mathf.Approximately(body.linearDamping, settings.LinearDamping)
                   && Mathf.Approximately(body.angularDamping, settings.AngularDamping)
                   && body.interpolation == settings.Interpolation
                   && body.collisionDetectionMode == CollisionDetectionMode.Continuous,
                label + " did not apply ObstacleConfig-owned Rigidbody settings, including Continuous collision detection.");
        }

        private readonly struct RigidbodyState
        {
            public readonly bool useGravity;
            public readonly bool isKinematic;
            public readonly RigidbodyConstraints constraints;
            public readonly bool detectCollisions;

            public RigidbodyState(Rigidbody body)
            {
                useGravity = body.useGravity;
                isKinematic = body.isKinematic;
                constraints = body.constraints;
                detectCollisions = body.detectCollisions;
            }

            public void AssertNonConfigUnchanged(Rigidbody body, Action<bool, string> assert, string label)
            {
                assert(useGravity == body.useGravity && isKinematic == body.isKinematic
                       && constraints == body.constraints && detectCollisions == body.detectCollisions,
                    label + " Rigidbody non-Config properties changed during its pool lifecycle.");
            }
        }
    }
}
#endif
