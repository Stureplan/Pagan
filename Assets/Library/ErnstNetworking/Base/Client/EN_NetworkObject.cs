using System.Collections.Generic;
using UnityEngine;

public class EN_NetworkObject : MonoBehaviour 
{
    public int network_id;

    private void OnEnable()
    {
    }
    private void OnDisable()
    {
        Remove(network_id);
    }

    private static Dictionary<int, GameObject> networkObjects = new Dictionary<int, GameObject>();
    public static void Add(int id, GameObject go)
    {
        EN_NetworkObject net_obj = go.AddComponent<EN_NetworkObject>();
        net_obj.network_id = id;

        networkObjects.Add(id, go);
    }
    public static void Remove(int id)
    {
        networkObjects.Remove(id);
    }
    public static GameObject Find(int id)
    {
        return networkObjects[id];
    }
    public static bool IsMine(GameObject go)
    {
        if (go.GetComponent<EN_NetworkObject>() == null)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
