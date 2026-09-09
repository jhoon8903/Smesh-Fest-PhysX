using System;
using System.IO;
using Framework.Pool;
using Framework.Test;
using InGame.Ball;
using InGame.Cannon;
using InGame.Obstacle;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Framework.EditorValidation
{
    /// <summary>Creates disposable Play Mode fixtures only; it never opens, saves, or edits authoring assets.</summary>
    public static class ClickLaunchValidation
    {
        [MenuItem("Tools/Smesh Fest/Validation/Click Launch Runtime")]
        public static void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("[ClickLaunchValidation] Enter Play Mode first.");
                return;
            }
            if (UnityEngine.Object.FindAnyObjectByType<ClickLaunchRuntimeProbe>() != null)
            {
                Debug.LogError("[ClickLaunchValidation] A probe is already running.");
                return;
            }

            Scene scene = default;
            GameObject fixtures = null;
            GameObject host = null;
            PoolConfig ballConfig = null;
            try
            {
                scene = SceneManager.CreateScene("__ClickLaunchValidation", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                fixtures = new GameObject("__ClickLaunchValidation_Fixtures");
                SceneManager.MoveGameObjectToScene(fixtures, scene);
                GameObject sourceRoot = new GameObject("Source");
                sourceRoot.transform.SetParent(fixtures.transform, false);
                sourceRoot.SetActive(false);

                BallView ballSource = CreateBallSource(sourceRoot.transform);
                ballConfig = CreateConfig("__ClickLaunchValidation_BallPool", ballSource);
                PoolContainer container = fixtures.AddComponent<PoolContainer>();
                SerializedObject catalog = new SerializedObject(container);
                SerializedProperty configs = catalog.FindProperty("configs");
                configs.arraySize = 1;
                configs.GetArrayElementAtIndex(0).objectReferenceValue = ballConfig;
                catalog.ApplyModifiedPropertiesWithoutUndo();

                Camera camera = CreateCamera(fixtures.transform);
                CannonView cannon = CreateCannon(fixtures.transform);
                Transform obstacle = CreateObstacle(fixtures.transform);
                host = new GameObject("__ClickLaunchValidation_Probe");
                SceneManager.MoveGameObjectToScene(host, scene);
                host.AddComponent<ClickLaunchRuntimeProbe>().Begin(fixtures, container, ballConfig, cannon, camera, obstacle, scene,
                    Path.Combine(Application.dataPath, "../Docs/Prototype/evidence/raw/click-launch-runtime-validation.json"));
            }
            catch (Exception exception)
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                if (fixtures != null) UnityEngine.Object.Destroy(fixtures);
                if (ballConfig != null) UnityEngine.Object.Destroy(ballConfig);
                if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
                Debug.LogError("[ClickLaunchValidation] Fixture setup failed and was rolled back.\n" + exception);
            }
        }

        private static BallView CreateBallSource(Transform parent)
        {
            GameObject source = new GameObject("Temporary_ClickLaunch_Ball_Source");
            source.transform.SetParent(parent, false);
            int ballLayer = LayerMask.NameToLayer("Ball");
            if (ballLayer < 0)
                throw new InvalidOperationException("The Ball layer is not configured.");
            source.layer = ballLayer;
            Rigidbody body = source.AddComponent<Rigidbody>();
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            source.AddComponent<SphereCollider>().radius = .05f;
            return source.AddComponent<BallView>();
        }

        private static Camera CreateCamera(Transform parent)
        {
            GameObject value = new GameObject("Temporary_ClickLaunch_Camera");
            value.transform.SetParent(parent, false);
            value.transform.position = new Vector3(0f, 8f, -10f);
            value.transform.LookAt(new Vector3(0f, .5f, -.625f));
            Camera camera = value.AddComponent<Camera>();
            camera.pixelRect = new Rect(0f, 0f, 1000f, 1000f);
            return camera;
        }

        private static CannonView CreateCannon(Transform parent)
        {
            GameObject root = new GameObject("Temporary_ClickLaunch_Cannon");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(0f, .3f, -4f);
            Transform yaw = new GameObject("YawRoot").transform;
            yaw.SetParent(root.transform, false);
            Transform pitch = new GameObject("PitchPivot").transform;
            pitch.SetParent(yaw, false);
            pitch.localRotation = Quaternion.Euler(15f, 0f, 0f);
            Transform barrel = new GameObject("BarrelReference").transform;
            barrel.SetParent(pitch, false);
            barrel.localPosition = new Vector3(0f, 0f, .5f);
            CannonView cannon = root.AddComponent<CannonView>();
            SerializedObject serialized = new SerializedObject(cannon);
            serialized.FindProperty("yawRoot").objectReferenceValue = yaw;
            serialized.FindProperty("barrelReference").objectReferenceValue = barrel;
            serialized.FindProperty("muzzleOffset").vector3Value = new Vector3(0f, 0f, .75f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(true);
            return cannon;
        }

        private static Transform CreateObstacle(Transform parent)
        {
            GameObject value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = "Temporary_ClickLaunch_Obstacle";
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer < 0)
                throw new InvalidOperationException("The Obstacle layer is not configured.");
            value.layer = obstacleLayer;
            value.transform.SetParent(parent, false);
            value.transform.position = new Vector3(0f, .5f, -.625f);
            value.transform.localScale = new Vector3(2f, 1f, .05f);
            Rigidbody body = value.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            value.AddComponent<ObstacleView>();
            value.AddComponent<ClickLaunchCollisionRecorder>();
            return value.transform;
        }

        private static PoolConfig CreateConfig(string rootName, MonoBehaviour view)
        {
            PoolConfig config = ScriptableObject.CreateInstance<PoolConfig>();
            SerializedObject settings = new SerializedObject(config);
            settings.FindProperty("poolRootName").stringValue = rootName;
            settings.FindProperty("prefab").objectReferenceValue = view;
            settings.FindProperty("minPool").intValue = 0;
            settings.FindProperty("maxPool").intValue = 1;
            settings.FindProperty("returnDelaySeconds").floatValue = 0f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return config;
        }
    }
}
