using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ErnstNetworking.Protocol
{
    public enum EN_PREFABS
    {
        Client = 0,
        Cube,
        StartUI
    }

    public enum EN_UDP_PACKET_TYPE
    {
        TRANSFORM = 0
    }

    public enum EN_TCP_PACKET_TYPE
    {
        CONNECT = 0,
        MESSAGE,
        GAME_STATE,
        SPAWN_OBJECT,
        REMOVE_OBJECT
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct EN_ClientInfo
    {
        public EN_ClientInfo(TcpClient client, Guid guid, string name)
        {
            client_tcp  = client;
            client_guid = guid;
            client_name = name;
        }

        public EN_ClientInfo(Guid guid, string name)
        {
            client_guid = guid;
            client_name = name;
            client_tcp = null;
        }

        public TcpClient client_tcp;
        public Guid client_guid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
        public string client_name;
    }

    public struct EN_PacketGameState
    {
        public EN_TCP_PACKET_TYPE   packet_type;
        public int                  packet_client_amount;
        public EN_ClientInfo[] packet_clients;
    }
    
    //TODO: Get rid of perhaps packet_client_id in all the TCP packets.
    //and maybe the Guid's aswell.
    struct EN_PacketTransform
    {
        public EN_UDP_PACKET_TYPE   packet_type;
        public int                  packet_network_id;
        //public Guid                 packet_client_guid;

        public float tX; public float tY; public float tZ;
        public float rX; public float rY; public float rZ;
        public float vX; public float vY; public float vZ;

        public string ToReadable()
        {
            string s = "T: " + tX + tY + tZ + "\tR: " + rX + rY + rZ;
            return s;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct EN_PacketConnect
    {
        public EN_TCP_PACKET_TYPE   packet_type;
        public Guid                 packet_client_guid;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 12)]
        public string               packet_client_name;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    struct EN_PacketMessage
    {
        public EN_TCP_PACKET_TYPE   packet_type;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string               packet_message;

        public EN_PacketMessage(string msg)
        {
            packet_type = EN_TCP_PACKET_TYPE.MESSAGE;
            packet_message = msg;
        }
    }

    struct EN_PacketSpawnObject
    {
        public EN_TCP_PACKET_TYPE   packet_type;
        public EN_PREFABS           packet_prefab;
        public int                  packet_network_id;

        public float tX; public float tY; public float tZ;
        public float rX; public float rY; public float rZ;
    }

    struct EN_PacketRemoveObject
    {
        public EN_TCP_PACKET_TYPE   packet_type;
        public int                  packet_network_id;
    }

    public class EN_Protocol
    {
        public static void Connect(UdpClient client, IPEndPoint server)
        {
            client.Connect(server);
        }

        public static bool Connect(TcpClient client, IPEndPoint server, string name, Guid guid)
        {
            if (client.Connected)
            {
                client.Client.Disconnect(true);
            }

            try
            {
                EN_PacketConnect packet;
                packet.packet_type = EN_TCP_PACKET_TYPE.CONNECT;
                packet.packet_client_guid = guid;
                packet.packet_client_name = name;

                byte[] bytes = ObjectToBytes(packet);

                client.Client.Connect(server);

                NetworkStream stream = client.GetStream();
                stream.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
                stream.Write(bytes, 0, bytes.Length);
                //connected
                return true;
            }
            catch(SocketException e)
            {
                //not connected
                string s = e.Message;
                return false;
            }
            catch(Exception e)
            {
                string s = e.Message;
                return false;
            }
        }

        public static void SendUDP(UdpClient client, IPEndPoint ip, object msg)
        {
            byte[] bytes = ObjectToBytes(msg);
            client.Send(bytes, bytes.Length);
        }

        public static void SendTCP(NetworkStream stream, object msg)
        {
            byte[] bytes = ObjectToBytes(msg);
            stream.Write(ObjectToBytes(bytes.Length), 0, 4);
            stream.Write(bytes, 0, bytes.Length);
        }

        public static byte[] StringToBytes(string str)
        {
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(str);
            return bytes;
        }

        public static string BytesToString(byte[] bytes)
        {
            string s = System.Text.Encoding.ASCII.GetString(bytes);
            return s;
        }

        public static byte[] ObjectToBytes(object o)
        {
            int size = Marshal.SizeOf(o);

            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);
            
            Marshal.StructureToPtr(o, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);
            
            return arr;
        }

        public static byte[] ObjectToBytes(object o, int skip)
        {
            int size = Marshal.SizeOf(o);

            byte[] arr = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(o, ptr, true);
            Marshal.Copy(ptr, arr, skip, size);
            Marshal.FreeHGlobal(ptr);

            return arr;
        }

        public static T BytesToObject<T>(byte[] bytes)
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            object result = Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));

            handle.Free();

            return (T)result;
        }

        public static EN_UDP_PACKET_TYPE BytesToUDPType(byte[] bytes)
        {
            return (EN_UDP_PACKET_TYPE)BitConverter.ToInt32(bytes, 0);
        }

        public static EN_TCP_PACKET_TYPE BytesToTCPType(byte[] bytes, int offset)
        {
            return (EN_TCP_PACKET_TYPE)BitConverter.ToInt32(bytes, offset);
        }
    }
}