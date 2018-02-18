using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using ErnstNetworking.Server;
using ErnstNetworking.Client;
using ErnstNetworking.Protocol;



public class EN_Client : MonoBehaviour
{
    // This client (PC)
    private UdpClient udp_client;
    private TcpClient tcp_client;

    // Target connection
    private IPEndPoint server;
    private bool connected = false;

    // Network stream
    private NetworkStream stream;

    // Client list (connected players)
    private List<EN_ClientInfo> clients;

    // UI Stuff
    public Text text_clients;
    public Text text_name;

    public Text udp_in;
    public Text tcp_in;

    // Network Tracking
    private uint udpBytesIn = 0;
    private uint tcpBytesIn = 0;


    // Debug Console
    private static System.Diagnostics.Process cmd;
    private static System.IO.StreamWriter console;



    // Issue Tracking


    public static EN_Client Client;

    public static EN_Client Contact()
    {
        if (Client == null)
        {
            Client = FindObjectOfType<EN_Client>();
        }

        return Client;
    }

    private void Console()
    {
        cmd = new System.Diagnostics.Process();
        cmd.StartInfo.FileName = Application.streamingAssetsPath + "/DebugConsole.exe";
        cmd.StartInfo.UseShellExecute = false;
        cmd.StartInfo.RedirectStandardInput = true;
        cmd.Start();
        
        console = cmd.StandardInput;
        console.AutoFlush = true;
        console.Flush();
    }

    public static void ConsoleMessage(string msg)
    {
        if (Application.isEditor == true)
        {
            Debug.Log(msg);
        }
        else
        {
            console.Write(msg);
            console.Write(Environment.NewLine);
        }
    }

    public static void ConsoleExit()
    {
        if (cmd != null)
        {
            if (cmd.HasExited == false)
            {
                console.Close();
                cmd.CloseMainWindow();
            }
        }
    }

    private void OnSceneLoad(Scene scene, LoadSceneMode mode)
    {
        /*
                if (Application.isEditor)
                {
                    EN_NetworkPrefabs.BuildPrefabListEditor();
                }
                else
                {
                    EN_NetworkPrefabs.BuildPrefabListStandalone();
                    text_clients.text = "Standalone";
                }*/

        if (Application.isEditor == false)
        {
            Console();
        }

        EN_NetworkPrefabs.BuildPrefabList();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoad;
    }

    private void OnDisable()
    {
        ConsoleExit();

        SceneManager.sceneLoaded -= OnSceneLoad;
    }


    private void Start()
    {
        Application.runInBackground = true;
        Client = this;

        //TODO: Move Send/Translate UDP/TCP to EN_Protocol
        // It shouldn't bother the Unity code, really.

    }

    private void OnDestroy()
    {
        if (udp_client != null)
        {
            udp_client.Close();
        }

        if (tcp_client != null)
        {
            tcp_client.Client.Disconnect(true);
            tcp_client.GetStream().Close();
            tcp_client.Close();
        }
    }

    public static GameObject SpawnObject(EN_PREFABS prefab, Vector3 pos, Vector3 rot)
    {
        GameObject go = Instantiate(EN_NetworkPrefabs.Prefab(prefab), pos, Quaternion.Euler(rot));
        EN_SyncTransform trs = go.GetComponent<EN_SyncTransform>();
        trs.is_mine = true;
        trs.syncFrameRate = 10;

        return go;
    }


    /*  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -
        Constantly send updates (translate/rotate etc)
        -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  -  */
    public void SendUDP(object obj)
    {
        if (connected == false) { return; }

        EN_Protocol.SendUDP(udp_client,server, obj);
    }

    private void SendTCP(byte[] bytes)
    {
        // client.Send(ObjectToBytes(packet), )
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
        {
            EN_Protocol.SendTCP(stream, new EN_PacketMessage("Hello!"));
        }
        if (Input.GetKeyDown(KeyCode.F1))
        {
            GameObject go = SpawnObject(EN_PREFABS.Cube, new Vector3(UnityEngine.Random.Range(-5, 5), 0, 5), Vector3.zero);

            Vector3 pos = go.transform.position;
            Vector3 rot = go.transform.rotation.eulerAngles;

            EN_PacketSpawnObject packet;
            packet.packet_type = EN_TCP_PACKET_TYPE.SPAWN_OBJECT;
            packet.packet_prefab = EN_PREFABS.Cube;
            packet.packet_network_id = go.GetInstanceID();
            packet.tX = pos.x; packet.tY = pos.y; packet.tZ = pos.z;
            packet.rX = rot.x; packet.rY = rot.y; packet.rZ = rot.z;

            EN_Protocol.SendTCP(stream, packet);
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            //remove
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                int id = hit.transform.gameObject.GetInstanceID();
                Destroy(hit.transform.gameObject);


                EN_PacketRemoveObject packet;
                packet.packet_type = EN_TCP_PACKET_TYPE.REMOVE_OBJECT;
                packet.packet_network_id = id;

                EN_Protocol.SendTCP(stream, packet);
            }

        }

        udp_in.text = TrafficInUDP().ToString();
        tcp_in.text = TrafficInTCP().ToString();

        udpBytesIn = 0;
        tcpBytesIn = 0;
    }

    private uint TrafficInUDP()
    {
        uint bytes = udpBytesIn;
        return bytes;
    }

    private uint TrafficInTCP()
    {
        uint bytes = tcpBytesIn;
        return bytes;
    }


    private IEnumerator ReceiveUDP()
    {
        while (true)
        {
            while(udp_client.Available > 0)
            {
                byte[] bytes = udp_client.Receive(ref server);

                if (bytes.Length > 0)
                {
                    EN_UDP_PACKET_TYPE type = EN_Protocol.BytesToUDPType(bytes);
                    TranslateUDP(type, bytes);

                    udpBytesIn += (uint)bytes.Length;
                }
            }

            yield return null;
        }
    }

    private IEnumerator ReceiveTCP()
    {
        while(true)
        {
            if (tcp_client.Available > 0)
            {
                byte[] bytes_size = new byte[4];
                stream.Read(bytes_size, 0, 4);
                int bytesize = BitConverter.ToInt32(bytes_size, 0);

                byte[] bytes_data = new byte[bytesize];
                stream.Read(bytes_data, 0, bytesize);

                EN_TCP_PACKET_TYPE type = EN_Protocol.BytesToTCPType(bytes_data, 0);
                TranslateTCP(type, bytes_data);

                tcpBytesIn += (uint)bytes_size.Length + (uint)bytes_data.Length;
            }
            yield return null;
        }
    }
    private void TranslateUDP(EN_UDP_PACKET_TYPE type, byte[] bytes)
    {
        if (type == EN_UDP_PACKET_TYPE.TRANSFORM)
        {
            EN_PacketTransform packet = EN_Protocol.BytesToObject<EN_PacketTransform>(bytes);

            GameObject go = EN_NetworkObject.Find(packet.packet_network_id);
            if (go != null)
            {
                go.GetComponent<EN_SyncTransform>().Translate(packet.tX, packet.tY, packet.tZ, packet.rX, packet.rY, packet.rZ, packet.vX, packet.vY, packet.vZ);
            }
        }
    }

    private void TranslateTCP(EN_TCP_PACKET_TYPE type, byte[] bytes)
    {
        if (type == EN_TCP_PACKET_TYPE.CONNECT)
        {
            // Someone connected and we want to establish who it is
            EN_PacketConnect packet = EN_Protocol.BytesToObject<EN_PacketConnect>(bytes);

            if (packet.packet_client_guid.Equals(EN_ClientSettings.CLIENT_GUID) == true)
            {
                packet.packet_client_name += " (you)";
            }

            AddClient(packet.packet_client_guid, packet.packet_client_name);
        }
        if (type == EN_TCP_PACKET_TYPE.GAME_STATE)
        {
            //EN_PacketGameState packet = EN_Protocol.BytesToObject<EN_PacketGameState>(bytes);
        }
        if (type == EN_TCP_PACKET_TYPE.SPAWN_OBJECT)
        {
            EN_PacketSpawnObject packet = EN_Protocol.BytesToObject<EN_PacketSpawnObject>(bytes);
            Vector3     pos = new Vector3(packet.tX, packet.tY, packet.tZ);
            Quaternion  rot = Quaternion.Euler(packet.rX, packet.rY, packet.rZ);
            GameObject  go = Instantiate(EN_NetworkPrefabs.Prefab(packet.packet_prefab), pos, rot);
            EN_NetworkObject.Add(packet.packet_network_id, go);

            ConsoleMessage(string.Format("Spawned {0} with network ID {1}", go.name, packet.packet_network_id));
        }
        if (type == EN_TCP_PACKET_TYPE.REMOVE_OBJECT)
        {
            EN_PacketRemoveObject packet = EN_Protocol.BytesToObject<EN_PacketRemoveObject>(bytes);

            GameObject go = EN_NetworkObject.Find(packet.packet_network_id);
            string n = go.name;
            Destroy(go);

            ConsoleMessage(string.Format("Removed {0} with network ID {1}", n, packet.packet_network_id));
        }
    }


    private void AddClient(Guid guid, string n)
    {
        clients.Add(new EN_ClientInfo(guid,n));

        string s = "";
        for (int i = 0; i < clients.Count; i++)
        {
            s += clients[i].client_name + '\n';
        }

        text_clients.text = s;
    }

    public void ConnectClient()
    {
        udp_client = new UdpClient();// EN_ServerSettings.HOSTNAME, EN_ServerSettings.PORT);
        tcp_client = new TcpClient();// EN_ServerSettings.HOSTNAME, EN_ServerSettings.PORT);
        //tcp_client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);





        server = new IPEndPoint(IPAddress.Parse(EN_ServerSettings.HOSTNAME), EN_ServerSettings.PORT);
        clients = new List<EN_ClientInfo>();

        EN_ClientSettings.CLIENT_NAME = text_name.text;
        EN_ClientSettings.CLIENT_GUID = Guid.NewGuid();

        if (EN_Protocol.Connect(tcp_client, server, EN_ClientSettings.CLIENT_NAME, EN_ClientSettings.CLIENT_GUID) == false)
        {
            Debug.Log("Not connected (TCP). Returning.");
            return;
        }


        IPEndPoint ep = (IPEndPoint)tcp_client.Client.LocalEndPoint;
        int p = ep.Port;

        udp_client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp_client.Client.Bind(new IPEndPoint(IPAddress.Any, p));
        EN_Protocol.Connect(udp_client, server);



        stream = tcp_client.GetStream();

        //StartCoroutine(SendUDP(EN_ClientSettings.SEND_INTERVAL));
        StartCoroutine(ReceiveUDP());


        StartCoroutine(ReceiveTCP());

        StartCoroutine(WaitForConnection(1.0f));
    }

    private IEnumerator WaitForConnection(float timer)
    {
        float t = 0.0f;
        while (t < timer)
        {
            t += Time.deltaTime;

            yield return null;
        }

        connected = true;
    }
}
