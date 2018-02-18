using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EN_Behaviour : MonoBehaviour 
{
    public int network_id = -1;

    public static GameObject EN_Instantiate(GameObject prefab, Vector3 pos, Quaternion rot, int id)
    {
        GameObject go = Instantiate(prefab, pos, rot);
        go.AddComponent<EN_Behaviour>().network_id = id;

        return go;
    }
}
