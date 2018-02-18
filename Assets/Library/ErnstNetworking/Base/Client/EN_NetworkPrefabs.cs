using ErnstNetworking.Client;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using ErnstNetworking.Protocol;

[CreateAssetMenu(fileName = "EN_NetworkPrefabs", menuName = "ErnstNetworking/Create Network Prefab List", order = 52)]
public class EN_NetworkPrefabs : ScriptableObject
{
    private Dictionary<EN_PREFABS, GameObject> networkPrefabs;

    private static EN_NetworkPrefabs instance;

    public static void BuildPrefabList()
    {
        if (Application.isEditor)
        {
#if UNITY_EDITOR
            // Find game path asset
            string[] assets = AssetDatabase.FindAssets("t:EN_NetworkPrefabs");
            if (assets == null || assets.Length < 1)
            {
                return;
            }

            // Find game path through asset
            string asset = AssetDatabase.GUIDToAssetPath(assets[0]);
            EN_NetworkPrefabs prefabs_editor = AssetDatabase.LoadAssetAtPath<EN_NetworkPrefabs>(asset);
            prefabs_editor.networkPrefabs = new Dictionary<EN_PREFABS, GameObject>();
            GameObject[] prefabs = Resources.LoadAll<GameObject>("ErnstNetworking/NetworkPrefabs");

            for (int i = 0; i < prefabs.Length; i++)
            {
                EN_PrefabType prefab = prefabs[i].GetComponent<EN_PrefabType>();
                prefabs_editor.networkPrefabs.Add(prefab.type, prefabs[i]);
            }

            AssetDatabase.Refresh();
            EditorUtility.SetDirty(prefabs_editor);
            AssetDatabase.SaveAssets();

            instance = prefabs_editor;
#endif
        }
        else
        {
            EN_NetworkPrefabs prefabs_build = Resources.Load<EN_NetworkPrefabs>("ErnstNetworking/NetworkDatabase/EN_NetworkPrefabs");
            prefabs_build.networkPrefabs = new Dictionary<EN_PREFABS, GameObject>();
            GameObject[] prefabs = Resources.LoadAll<GameObject>("ErnstNetworking/NetworkPrefabs");

            for (int i = 0; i < prefabs.Length; i++)
            {
                EN_PrefabType prefab = prefabs[i].GetComponent<EN_PrefabType>();
                prefabs_build.networkPrefabs.Add(prefab.type, prefabs[i]);
            }

            instance = prefabs_build;
        }

    }

    public static GameObject Prefab(EN_PREFABS type)
    {
        return instance.networkPrefabs[type];
    }

}
