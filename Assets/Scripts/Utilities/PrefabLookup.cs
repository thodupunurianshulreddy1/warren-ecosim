// ============================================================================
//  PrefabLookup.cs   [H4]
//  Editor-only safety net: if a scene lost a prefab reference (e.g. it was
//  built before the prefabs existed, or the reference was cleared), managers
//  can reload the prefab from Assets/Prefabs by name instead of failing.
//  In a player build there is no AssetDatabase, so it simply returns null and
//  the references assigned by the EcoSim builder are used as usual.
// ============================================================================

using UnityEngine;

namespace ThodupunuriAnshul.EcoSim.Utilities
{
    public static class PrefabLookup
    {
        public const string PrefabsFolder = "Assets/Prefabs";

        /// <summary>
        /// Returns 'current' if it is set, otherwise (in the editor only) the component T
        /// on Assets/Prefabs/{prefabName}.prefab. Logs a clear message if nothing is found.
        /// </summary>
        public static T Resolve<T>(T current, string prefabName) where T : Component
        {
            if (current != null) return current;

#if UNITY_EDITOR
            string path = $"{PrefabsFolder}/{prefabName}.prefab";
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            T component = prefab != null ? prefab.GetComponent<T>() : null;
            if (component == null)
                Debug.LogError($"[EcoSim] Missing prefab '{path}'. Run 'EcoSim > Build Everything'.");
            return component;
#else
            return null;
#endif
        }
    }
}
