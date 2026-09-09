using System;
using System.IO;
using Framework.Pool;
using Framework.Test;
using InGame.Ball;
using InGame.Config;
using InGame.Obstacle;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framework.EditorValidation
{
    public static class PhysXValidation
    {
        [MenuItem("Tools/Smesh Fest/Validation/PhysX Lifecycle Runtime")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[PhysXValidation] Enter Play Mode first.");
                return;
            }
            if (UnityEngine.Object.FindAnyObjectByType<PhysXRuntimeProbe>() != null)
            {
                Debug.LogError("[PhysXValidation] A probe is already running.");
                return;
            }

            Scene scene = default;
            GameObject fixtures = null;
            GameObject host = null;
            PoolConfig ballConfig = null;
            PoolConfig obstacleConfig = null;
            try
            {
                scene = SceneManager.CreateScene("__PhysXValidation", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                fixtures = new GameObject("__PhysXValidation_Fixtures");
                SceneManager.MoveGameObjectToScene(fixtures, scene);
                GameObject sourceRoot = new GameObject("Source");
                sourceRoot.transform.SetParent(fixtures.transform, false);
                sourceRoot.SetActive(false);

                BallView ball = CreateBallSource(sourceRoot.transform);
                ObstacleView obstacle = CreateObstacleSource(sourceRoot.transform);
                ballConfig = CreateConfig("__PhysXValidation_BallPool", ball);
                obstacleConfig = CreateConfig("__PhysXValidation_ObstaclePool", obstacle);
                PoolContainer container = fixtures.AddComponent<PoolContainer>();
                SerializedObject catalog = new SerializedObject(container);
                SerializedProperty configs = catalog.FindProperty("configs");
                configs.arraySize = 2;
                configs.GetArrayElementAtIndex(0).objectReferenceValue = ballConfig;
                configs.GetArrayElementAtIndex(1).objectReferenceValue = obstacleConfig;
                catalog.ApplyModifiedPropertiesWithoutUndo();

                host = new GameObject("__PhysXValidation_Probe");
                SceneManager.MoveGameObjectToScene(host, scene);
                host.AddComponent<PhysXRuntimeProbe>().Begin(fixtures, container, ballConfig, obstacleConfig, scene,
                    Path.Combine(Application.dataPath, "../Docs/Prototype/evidence/raw/physx-lifecycle-runtime-validation.json"));
            }
            catch (Exception exception)
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                if (fixtures != null) UnityEngine.Object.Destroy(fixtures);
                if (ballConfig != null) UnityEngine.Object.Destroy(ballConfig);
                if (obstacleConfig != null) UnityEngine.Object.Destroy(obstacleConfig);
                if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
                Debug.LogError("[PhysXValidation] Fixture setup failed and was rolled back.\n" + exception);
            }
        }

        private static BallView CreateBallSource(Transform parent)
        {
            GameObject source = new GameObject("Temporary_Ball_PhysX_Source");
            source.transform.SetParent(parent, false);
            source.layer = RequireLayer("Ball");
            Rigidbody body = source.AddComponent<Rigidbody>();
            body.mass = 1.25f;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotationZ;
            body.linearDamping = 0.4f;
            body.angularDamping = 0.8f;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            source.AddComponent<SphereCollider>().radius = 0.5f;
            return source.AddComponent<BallView>();
        }

        private static ObstacleView CreateObstacleSource(Transform parent)
        {
            GameObject source = new GameObject("Temporary_Obstacle_PhysX_Source");
            source.transform.SetParent(parent, false);
            source.layer = RequireLayer("Obstacle");
            Rigidbody body = source.AddComponent<Rigidbody>();
            body.mass = 2f;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotationZ;
            body.linearDamping = 0.6f;
            body.angularDamping = 1.2f;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.interpolation = RigidbodyInterpolation.Extrapolate;
            source.AddComponent<BoxCollider>().size = Vector3.one;
            source.AddComponent<PhysXCollisionRecorder>();
            return source.AddComponent<ObstacleView>();
        }

        private static PoolConfig CreateConfig(string rootName, MonoBehaviour view)
        {
            PoolConfig config = ScriptableObject.CreateInstance<PoolConfig>();
            SerializedObject settings = new SerializedObject(config);
            settings.FindProperty("poolRootName").stringValue = rootName;
            settings.FindProperty("prefab").objectReferenceValue = view;
            settings.FindProperty("minPool").intValue = 1;
            settings.FindProperty("maxPool").intValue = 1;
            settings.FindProperty("returnDelaySeconds").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }

        private static int RequireLayer(string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
                throw new InvalidOperationException("Required physics layer is missing: " + layerName + ".");
            return layer;
        }
    }
}
