using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;

using ErnstNetworking.Server;
using ErnstNetworking.Protocol;


namespace ErnstNetworking
{
#if !UNITY_EDITOR && !UNITY_5 && !UNITY_STANDALONE
    class EN_Server
    {
        private static readonly string LOADING_IP = "<finding IP address...>";


        static EN_Server SERVER;
        static void Main(string[] args)
        {
            SERVER = new EN_Server();
        }


        UdpClient udp_server;
        TcpListener tcp_server;
        IPEndPoint udp_source;
        List<IPEndPoint> udp_clients;
        List<TcpClient> tcp_clients;
        List<byte[]> packet_stack;
        Dictionary<int, int> networkIDs;
        Dictionary<int, EN_PacketSpawnObject> objects;
        int current_networkID = -1;

        int loops = 0;
        int poll_framerate = 100;


        private string IPConfig(string arg)
        {
            System.Diagnostics.Process cmd = new System.Diagnostics.Process();

            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.Arguments = arg;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;

            cmd.Start();

            string output = cmd.StandardOutput.ReadToEnd();
            return Sanitize(output);
        }

        private string Sanitize(string output)
        {
            output = output.Remove(0, output.IndexOf("DNS Servers"));

            int endl = output.IndexOf('\n');
            int length = output.Length;

            output = output.Remove(endl, length-endl);
            output = output.Remove(0, output.IndexOf(':')+1);

            return output;
        }
        
        public EN_Server()
        {


            /*EN_ClientInfo c1 = new EN_ClientInfo(Guid.NewGuid(), "123");
            EN_ClientInfo c2 = new EN_ClientInfo(Guid.NewGuid(), "123456");

            byte[] b1 = EN_Protocol.ObjectToBytes(c1);
            byte[] b2 = EN_Protocol.ObjectToBytes(c2);

            EN_ClientInfo bc1 = EN_Protocol.BytesToObject<EN_ClientInfo>(b1);
            EN_ClientInfo bc2 = EN_Protocol.BytesToObject<EN_ClientInfo>(b2);*/

            //TODO: Marshal the shit out of these EN_ClientInfos.
            // This allows us to send them straight over the network as byte[] in sequence.


            udp_server = new UdpClient();
            udp_server.Client.IOControl((IOControlCode)EN_ServerSettings.SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
            udp_server.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp_server.Client.Bind(new IPEndPoint(IPAddress.Any, EN_ServerSettings.PORT));

            tcp_server = new TcpListener(IPAddress.Any,EN_ServerSettings.PORT);
            udp_source = new IPEndPoint(IPAddress.Any, 0);
            udp_clients = new List<IPEndPoint>();
            tcp_clients = new List<TcpClient>();
            packet_stack = new List<byte[]>();
            networkIDs = new Dictionary<int, int>();
            objects = new Dictionary<int, EN_PacketSpawnObject>();


            Console.WriteLine("\t\t::ErnstNetworking Server::\n");
            Console.Write("Your external IP is: ");
            Console.Write(LOADING_IP);

            string ip = IPConfig("/c ipconfig /all") + '\n';

            for (int i = 0; i < LOADING_IP.Length; i++)
            {
                Console.Write("\b \b");
            }

            Console.Write(ip);

            Console.WriteLine("Waiting for connections...");

            tcp_server.Start();

            while (true)
            {
                // Quit if we pressed Escape
                // TODO: Disconnect all clients and maybe some cleanup (?)
                if ((Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)){ break; }

                // Searches the tcp listener for new connections.
                DiscoverClients();

                // Check if clients have disconnected.
                PollClients(loops);

                // Receive messages over UDP & TCP
                ReceiveUDP(loops);
                ReceiveTCP();

                loops++;
                if (loops > 999)
                {
                    loops = 0;
                }
            }

            udp_server.Close();
            for (int i = 0; i < tcp_clients.Count; i++)
            {
                tcp_clients[i].GetStream().Close();
                tcp_clients[i].Close();
            }
        }

        private void DiscoverClients()
        {
            if (tcp_server.Pending())
            {
                tcp_clients.Add(tcp_server.AcceptTcpClient());
            }

            if (tcp_clients.Count < 8)
            {
                //TcpClient client = await tcp_server.AcceptTcpClientAsync();
                //tcp_clients.Add(client);
            }
        }

        private void PollClients(int loop)
        {
            if (loop % poll_framerate == 0)
            {
                for (int i = 0; i < tcp_clients.Count; i++)
                {
                    if (tcp_clients[i].Client.Poll(0, SelectMode.SelectRead) == true)
                    {
                        byte[] bytes = new byte[1];
                        if (tcp_clients[i].Client.Receive(bytes, SocketFlags.Peek) == 0)
                        {
                            //TODO: Construct EN_PacketDisconnect and logic behind it
                            DisconnectClient(tcp_clients[i]);
                        }
                    }
                }
            }
        }

        private void DisconnectClient(TcpClient client)
        {
            IPEndPoint ep = (IPEndPoint)client.Client.RemoteEndPoint;

            Console.WriteLine(string.Format("{0}:{1}: User disconnected.", ep.Address.ToString(), ep.Port.ToString()));
            client.Client.Disconnect(true);
            client.Close();
            tcp_clients.Remove(client);
            udp_clients.Remove(ep);
        }

        private void ReceiveUDP(int loops)
        {
            if (udp_server.Available > 0)
            {
                byte[] bytes = udp_server.Receive(ref udp_source); //TODO: If Receive fails again after DC, try {} catch {} and spit it into console

                if (bytes.Length > 0)
                {
                    // Get & translate first 4 bytes
                    EN_UDP_PACKET_TYPE packet_type = EN_Protocol.BytesToUDPType(bytes);

                    TranslateUDP(udp_source, packet_type, bytes);
                }
            }
        }

        private void ReceiveTCP()
        {
            for (int i = 0; i < tcp_clients.Count; i++)
            {
                if (tcp_clients[i].Available > 0)
                {
                    NetworkStream stream = tcp_clients[i].GetStream();


                    byte[] bytes_size = new byte[4];
                    stream.Read(bytes_size, 0, 4);
                    int bytesize = BitConverter.ToInt32(bytes_size, 0);

                    byte[] bytes_data = new byte[bytesize];
                    stream.Read(bytes_data, 0, bytesize);


                    EN_TCP_PACKET_TYPE packet_type = EN_Protocol.BytesToTCPType(bytes_data, 0);

                    IPEndPoint source = (IPEndPoint)tcp_clients[i].Client.RemoteEndPoint;
                    Console.WriteLine("TCP " + (source.Address.ToString() + ":" + source.Port.ToString() + ": " + TranslateTCP(tcp_clients[i], packet_type, bytes_data)));
                }
            }
        }

        private void TranslateUDP(IPEndPoint source, EN_UDP_PACKET_TYPE type, byte[] bytes)
        {
            if (type == EN_UDP_PACKET_TYPE.TRANSFORM)
            {
                EN_PacketTransform packet = EN_Protocol.BytesToObject<EN_PacketTransform>(bytes);

                // This comes in as a Unity InstanceID, we need to networkID-it
                packet.packet_network_id = networkIDs[packet.packet_network_id];

                byte[] bytes_data = EN_Protocol.ObjectToBytes(packet);

                BroadcastUDP(source, bytes_data);
            }
        }

        private string TranslateTCP(TcpClient client, EN_TCP_PACKET_TYPE type, byte[] bytes)
        {
            string s = "";
            if (type == EN_TCP_PACKET_TYPE.CONNECT)
            {
                EN_PacketConnect packet = EN_Protocol.BytesToObject<EN_PacketConnect>(bytes);

                // Resend older important messages from before
                ResendStackTCP(client);

                // Send out this connection packet to the rest of the clients
                BroadcastAllTCP(bytes);

                // Add connect request to the stack of important messages
                packet_stack.Add(bytes);

                s = packet.packet_client_name + " connected.";

                IPEndPoint temp = (IPEndPoint)client.Client.RemoteEndPoint;
                udp_clients.Add(temp);
                // Add client to list of unique ID's
                //clients.Add(new EN_ClientInfo(client, packet.packet_client_guid, packet.packet_client_name));
            }
            if (type == EN_TCP_PACKET_TYPE.MESSAGE)
            {
                EN_PacketMessage packet = EN_Protocol.BytesToObject<EN_PacketMessage>(bytes);
                s = packet.packet_message;

                // Add connect request to the stack of important messages
                packet_stack.Add(bytes);
            }
            if(type == EN_TCP_PACKET_TYPE.SPAWN_OBJECT)
            {
                EN_PacketSpawnObject packet = EN_Protocol.BytesToObject<EN_PacketSpawnObject>(bytes);

                current_networkID++;
                networkIDs.Add(packet.packet_network_id, current_networkID);

                packet.packet_network_id = networkIDs[packet.packet_network_id];

                byte[] bytes_data = EN_Protocol.ObjectToBytes(packet);

                BroadcastTCP(client, bytes_data);

                s = packet.packet_prefab + " with network ID " + packet.packet_network_id + " was spawned.";

                // Add connect request to the stack of important messages
                packet_stack.Add(bytes);
                objects.Add(packet.packet_network_id, packet);
            }
            if (type == EN_TCP_PACKET_TYPE.REMOVE_OBJECT)
            {
                EN_PacketRemoveObject packet = EN_Protocol.BytesToObject<EN_PacketRemoveObject>(bytes);

                packet.packet_network_id = networkIDs[packet.packet_network_id];

                byte[] bytes_data = EN_Protocol.ObjectToBytes(packet);

                BroadcastTCP(client, bytes_data);

                s = "Prefab with network ID " + packet.packet_network_id + " was removed.";

                // Add connect request to the stack of important messages
                packet_stack.Add(bytes);

                networkIDs.Remove(packet.packet_network_id);
                objects.Remove(packet.packet_network_id);
            }

            return s;
        }

        private void BroadcastUDP(IPEndPoint source, byte[] bytes)
        {
            for (int i = 0; i < udp_clients.Count; i++)
            {
                if (source.Equals(udp_clients[i]) == false)
                {
                    // Only broadcast to clients that didn't send the original UDP packet
                    udp_server.Send(bytes, bytes.Length, udp_clients[i]);
                }
            }
        }

        private void BroadcastTCP(TcpClient source, byte[] bytes)
        {
            for (int i = 0; i < tcp_clients.Count; i++)
            {
                if (source.Equals(tcp_clients[i]) == false)
                {
                    // Only broadcast to clients that didn't send the original TCP packet
                    NetworkStream stream = tcp_clients[i].GetStream();

                    stream.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
                    stream.Write(bytes, 0, bytes.Length);
                }
            }
        }

        private void BroadcastAllTCP(byte[] bytes)
        {
            for (int i = 0; i < tcp_clients.Count; i++)
            {
                // Broadcast to all
                NetworkStream stream = tcp_clients[i].GetStream();

                stream.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        private void ResendStackTCP(TcpClient client)
        {
            NetworkStream stream = client.GetStream();

            // Re-broadcast all old messages
            for (int i = 0; i < packet_stack.Count; i++)
            {
                //stream.Write(BitConverter.GetBytes(packet_stack[i].Length), 0, 4);
                //stream.Write(packet_stack[i], 0, packet_stack[i].Length);
            }

            foreach (int key in objects.Keys)
            {
                EN_Protocol.SendTCP(stream, objects[key]);
            }
            for (int i = 0; i < networkIDs.Count; i++)
            {
            }
        }

        private void SendStateTCP(TcpClient client)
        {
            //NetworkStream stream = client.GetStream();

            //EN_PacketGameState state;
            //state.packet_type = EN_TCP_PACKET_TYPE.GAME_STATE;
            //state.packet_client_amount = clients.Count;
            //state.packet_clients = clients.ToArray();
        }
    }
#endif
}



/*
 * 
 * 
 * 
 * 
                // Setup an ID to replace the old -1 ID from the packet
                byte[] newID = new byte[4];
                newID = BitConverter.GetBytes(tcp_clients.Count);
                for (int i = 0; i < 4; i++)
                {
                    // Good ol-fashioned byte swap to insert the new ID
                    byte b = newID[i];
                    bytes[4 + i] = b;
                }
*/




// Algo for finding new UDP users, shouldn't be needed since we use same IP/PORT in TCP
/*int count = udp_clients.Count;
bool found = false;
for (int i = 0; i < count; i++)
{
    if (udp_clients[i].Equals(udp_source) == true)
    {
        found = true;
        break;
    }
}
if (found == false) { udp_clients.Add(udp_source); Console.WriteLine("UDP ADDED"); }
*/
