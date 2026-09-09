using System;
using System.Collections.Generic;
using Framework.Pool;
using InGame.Level;
using InGame.Obstacle;
using UnityEditor;
using UnityEngine;

namespace Editor.Level
{
    /// <summary>Explicit, non-destructive layout capture. Capture only updates this window preview; Bake writes the chosen SO.</summary>
    public sealed class LevelEditorWindow : EditorWindow
    {
        private sealed class CapturedEntry
        {
            public Transform Source;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        private Transform authoringRoot;
        private PoolConfig defaultPoolConfig;
        private LevelConfig levelConfig;
        private readonly List<CapturedEntry> captured = new List<CapturedEntry>();
        private string validation = "Assign an authoring root, default PoolConfig, and LevelConfig.";
        private MessageType validationType = MessageType.Info;

        [MenuItem("Smesh Fest/Level Editor")]
        private static void Open() => GetWindow<LevelEditorWindow>("Level Editor");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Non-destructive obstacle layout capture", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Capture reads direct child ObstacleView transforms into this window only. Bake is the sole action that writes the selected LevelConfig. It never changes the Scene, children, prefabs, or pool setup.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            Transform selectedRoot = (Transform)EditorGUILayout.ObjectField("Authoring Root", authoringRoot, typeof(Transform), true);
            PoolConfig selectedPoolConfig = (PoolConfig)EditorGUILayout.ObjectField("Default PoolConfig", defaultPoolConfig, typeof(PoolConfig), false);
            LevelConfig selectedLevelConfig = (LevelConfig)EditorGUILayout.ObjectField("LevelConfig", levelConfig, typeof(LevelConfig), false);
            if (EditorGUI.EndChangeCheck())
                ApplySelection(selectedRoot, selectedPoolConfig, selectedLevelConfig);

            bool canCapture = CanCapture(out string captureReason);
            using (new EditorGUI.DisabledScope(!canCapture))
            {
                if (GUILayout.Button("Capture Direct Child Obstacles"))
                    Capture();
            }
            if (!canCapture)
                EditorGUILayout.HelpBox(captureReason, MessageType.Warning);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField($"Captured entries: {captured.Count}");
            for (int i = 0; i < captured.Count; i++)
                EditorGUILayout.ObjectField($"{i}", captured[i].Source, typeof(Transform), true);

            bool canBake = CanBake(out string bakeReason);
            using (new EditorGUI.DisabledScope(!canBake))
            {
                if (GUILayout.Button("Bake Captured Entries to LevelConfig"))
                    Bake();
            }
            if (!canBake)
                EditorGUILayout.HelpBox(bakeReason, MessageType.Warning);

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(validation, validationType);
        }

        private bool CanCapture(out string reason)
        {
            if (authoringRoot == null)
            {
                reason = "Authoring Root is required.";
                return false;
            }
            if (defaultPoolConfig == null)
            {
                reason = "Default PoolConfig is required; this MVP does not infer one from an ObstacleView.";
                return false;
            }
            if (levelConfig == null)
            {
                reason = "LevelConfig is required; Capture never chooses or creates an asset automatically.";
                return false;
            }
            try { defaultPoolConfig.Validate(); }
            catch (Exception exception)
            {
                reason = $"Default PoolConfig is invalid: {exception.Message}";
                return false;
            }

            reason = null;
            return true;
        }

        private void Capture()
        {
            if (!CanCapture(out string reason))
            {
                SetValidation(reason, MessageType.Error);
                return;
            }

            captured.Clear();
            int ignored = 0;
            for (int i = 0; i < authoringRoot.childCount; i++)
            {
                Transform child = authoringRoot.GetChild(i);
                if (!child.TryGetComponent(out ObstacleView _))
                {
                    ignored++;
                    continue;
                }
                captured.Add(new CapturedEntry
                {
                    Source = child,
                    LocalPosition = child.localPosition,
                    LocalRotation = child.localRotation,
                    LocalScale = child.localScale
                });
            }

            SetValidation(captured.Count == 0
                ? "No direct child ObstacleView was found. Nothing was written."
                : $"Captured {captured.Count} direct child ObstacleView transform(s) in hierarchy order. {ignored} non-obstacle direct child(ren) were ignored. Nothing has been baked yet.",
                captured.Count == 0 ? MessageType.Warning : MessageType.Info);
        }

        private void Bake()
        {
            if (!CanBake(out string reason))
            {
                SetValidation(reason, MessageType.Error);
                return;
            }

            Undo.RecordObject(levelConfig, "Bake Level Layout");
            SerializedObject serializedConfig = new SerializedObject(levelConfig);
            SerializedProperty entries = serializedConfig.FindProperty("entries");
            entries.arraySize = captured.Count;
            for (int i = 0; i < captured.Count; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("poolConfig").objectReferenceValue = defaultPoolConfig;
                entry.FindPropertyRelative("localPosition").vector3Value = captured[i].LocalPosition;
                entry.FindPropertyRelative("localRotation").quaternionValue = captured[i].LocalRotation;
                entry.FindPropertyRelative("localScale").vector3Value = captured[i].LocalScale;
            }
            serializedConfig.ApplyModifiedProperties();
            EditorUtility.SetDirty(levelConfig);
            AssetDatabase.SaveAssetIfDirty(levelConfig);
            SetValidation($"Baked {captured.Count} ordered entries to '{levelConfig.name}'. Scene objects were not changed or saved.", MessageType.Info);
        }

        private bool CanBake(out string reason)
        {
            if (levelConfig == null)
            {
                reason = "LevelConfig is required to bake.";
                return false;
            }
            if (defaultPoolConfig == null)
            {
                reason = "Default PoolConfig is required to bake.";
                return false;
            }
            if (captured.Count == 0)
            {
                reason = "Capture at least one direct child ObstacleView before baking.";
                return false;
            }
            try { defaultPoolConfig.Validate(); }
            catch (Exception exception)
            {
                reason = $"Default PoolConfig is invalid: {exception.Message}";
                return false;
            }
            if (captured.Count > defaultPoolConfig.MaxPool)
            {
                reason = $"{captured.Count} entries require '{defaultPoolConfig.name}', but its maximum pool size is {defaultPoolConfig.MaxPool}.";
                return false;
            }
            for (int i = 0; i < captured.Count; i++)
            {
                if (!IsValidCapturedTransform(captured[i], out string entryReason))
                {
                    reason = $"Captured entry {i} is invalid: {entryReason}";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private void RefreshValidation()
        {
            if (!CanCapture(out string reason))
                SetValidation(reason, MessageType.Warning);
            else
                SetValidation($"Ready to capture. Current preview contains {captured.Count} entry/entries and has not been written automatically.", MessageType.Info);
        }

        private void ApplySelection(Transform selectedRoot, PoolConfig selectedPoolConfig, LevelConfig selectedLevelConfig)
        {
            if (authoringRoot == selectedRoot && defaultPoolConfig == selectedPoolConfig && levelConfig == selectedLevelConfig)
                return;

            authoringRoot = selectedRoot;
            defaultPoolConfig = selectedPoolConfig;
            levelConfig = selectedLevelConfig;
            captured.Clear();
            RefreshValidation();
        }

        private void SetValidation(string message, MessageType type)
        {
            validation = message;
            validationType = type;
        }

        private static bool IsValidCapturedTransform(CapturedEntry entry, out string reason)
        {
            if (!IsFinite(entry.LocalPosition) || !IsFinite(entry.LocalScale)
                || !IsFinite(entry.LocalRotation))
            {
                reason = "pose or scale contains a non-finite value";
                return false;
            }
            if (entry.LocalScale.x == 0f || entry.LocalScale.y == 0f || entry.LocalScale.z == 0f)
            {
                reason = "scale contains zero";
                return false;
            }
            float rotationMagnitude = SqrMagnitude(entry.LocalRotation);
            if (rotationMagnitude < 0.999f || rotationMagnitude > 1.001f)
            {
                reason = "rotation is not normalized";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        private static bool IsFinite(Quaternion value) => IsFinite(value.x) && IsFinite(value.y)
                                                          && IsFinite(value.z) && IsFinite(value.w);
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float SqrMagnitude(Quaternion value) => value.x * value.x + value.y * value.y
                                                               + value.z * value.z + value.w * value.w;
    }
}
