using System.Collections;
using UnityEngine;

public class EN_SyncTransformClient : MonoBehaviour 
{
    public float t_rate = 10.0f;
    public float r_rate = 10.0f;
    private Vector3 pos;
    private Vector3 rot;
    private Vector3 vel;

    private void Start()
    {
        pos = transform.position;

        StartCoroutine(Sync());
    }

    public void Translate(float tX, float tY, float tZ, float rX, float rY, float rZ, float vX, float vY, float vZ)
    {
        pos.x = tX; pos.y = tY; pos.z = tZ;
        rot.x = rX; rot.y = rY; rot.z = rZ;
        vel.x = vX; vel.y = vY; vel.z = vZ;
    }


    private IEnumerator Sync()
    {
        while(true)
        {
            transform.position = Vector3.Lerp(transform.position, pos + vel, Time.deltaTime * t_rate);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(rot), Time.deltaTime * r_rate);
            yield return null;
        }
    }
}
