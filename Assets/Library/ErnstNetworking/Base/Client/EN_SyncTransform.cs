using UnityEngine;

using ErnstNetworking.Protocol;

public class EN_SyncTransform : MonoBehaviour
{
    [Header("Network Traffic Framerate (Higher = Less traffic)")]
    public int syncFrameRate = 1;

    private int frame = 0;
    private int instanceID = -1;

    private Vector3 lastpos = Vector3.zero;
    private Vector3 velocity = Vector3.zero;

    public float t_rate = 5.0f;
    public float r_rate = 5.0f;
    private Vector3 pos;
    private Vector3 rot;
    private Vector3 vel;

    public bool is_mine = false;
    
    private void OnEnable()
    {
        instanceID = gameObject.GetInstanceID();

        pos = transform.position;
        rot = transform.rotation.eulerAngles;
        vel = Vector3.zero;
        lastpos = pos;
    }

    private void Update()
    {
        if (is_mine == true) //send
        {
            frame++;
            if (frame > 999)
            {
                frame = 0;
            }

            if (frame % syncFrameRate == 0)
            {
                if (velocity.magnitude > 0.01f)
                {
                    //SendUDP();
                }

                Send();
                lastpos = transform.position;
            }
        }
        else //receive
        {
            transform.position = Vector3.Lerp(transform.position, pos + vel, Time.deltaTime * t_rate);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(rot), Time.deltaTime * r_rate);
        }
    }

    private void Send()
    {
        Vector3 pos = transform.position;
        Vector3 rot = transform.rotation.eulerAngles;
        Vector3 vel = (transform.position - lastpos).normalized * Vector3.Distance(transform.position, lastpos);
        velocity = vel;

        EN_PacketTransform data;
        data.packet_type = EN_UDP_PACKET_TYPE.TRANSFORM;
        data.packet_network_id = instanceID;
        data.tX = pos.x; data.tY = pos.y; data.tZ = pos.z;
        data.rX = rot.x; data.rY = rot.y; data.rZ = rot.z;
        data.vX = vel.x; data.vY = vel.y; data.vZ = vel.z;

        EN_Client.Contact().SendUDP(data);
    }

    public void Translate(float tX, float tY, float tZ, float rX, float rY, float rZ, float vX, float vY, float vZ)
    {
        pos.x = tX; pos.y = tY; pos.z = tZ;
        rot.x = rX; rot.y = rY; rot.z = rZ;
        vel.x = vX; vel.y = vY; vel.z = vZ;
    }
}
