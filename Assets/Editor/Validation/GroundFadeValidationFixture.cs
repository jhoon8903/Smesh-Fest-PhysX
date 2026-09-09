using InGame.Presentation;
using UnityEditor;
using UnityEngine;

namespace Framework.EditorValidation
{
    internal static class GroundFadeValidationFixture
    {
        internal static void Add(GameObject target, string prefabPath, string fadeMaterialPath)
        {
            if (target == null)
                throw new System.ArgumentNullException(nameof(target));

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException("Ground fade validation requires the production prefab.");

            if (!prefab.TryGetComponent(out Renderer prefabRenderer))
                throw new System.InvalidOperationException("Ground fade validation requires a Renderer on the production prefab root.");
            Material fadeMaterial = AssetDatabase.LoadAssetAtPath<Material>(fadeMaterialPath);
            if (fadeMaterial == null)
                throw new System.InvalidOperationException("Ground fade validation requires the production fade Material.");

            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = prefabRenderer.sharedMaterial;
            GroundFadeReturn fade = target.AddComponent<GroundFadeReturn>();
            SerializedObject serialized = new SerializedObject(fade);
            serialized.FindProperty("targetRenderer").objectReferenceValue = renderer;
            serialized.FindProperty("fadeMaterial").objectReferenceValue = fadeMaterial;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
