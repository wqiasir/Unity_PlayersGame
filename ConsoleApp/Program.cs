using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MultiplayerGameServer
{
    class Server
    {
        private static Random _random = new Random();
        private static TcpListener _listener;
        private static Dictionary<TcpClient, PlayerData> _clients = new Dictionary<TcpClient, PlayerData>();
        private static List<PlayerData> _playerList = new List<PlayerData>();
        private static readonly object _lock = new object();
        private static int _nextPlayerId = 1;

        static void Main(string[] args)
        {
            StartServer();
        }

        public static void StartServer()
        {
            _listener = new TcpListener(IPAddress.Any, 8888);
            _listener.Start();
            Console.WriteLine("服务器启动，监听端口 8888...");

            // 启动一个线程来持续接受新连接
            Thread acceptThread = new Thread(AcceptClients);
            acceptThread.Start();

            Console.WriteLine("按回车键停止服务器...");
            Console.ReadLine();
            StopServer();
        }

        private static void AcceptClients()
        {
            while (true)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    Console.WriteLine($"新客户端连接: {client.Client.RemoteEndPoint}");
                    // 为每个客户端启动一个独立的处理线程
                    Thread clientThread = new Thread(HandleClient);
                    clientThread.Start(client);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"接受连接时出错: {ex.Message}");
                }
            }
        }

        private static void HandleClient(object obj)
        {
            TcpClient client = (TcpClient)obj;
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;

            try
            {
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    string[] parts = message.Split('|');
                    string command = parts[0];

                    switch (command)
                    {
                        case "LOGIN":
                            HandleLogin(client, parts[1]);
                            break;
                        case "MOVE":
                            HandleMove(client, float.Parse(parts[1]), float.Parse(parts[2]), float.Parse(parts[3]));
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"与客户端通信出错: {ex.Message}");
            }
            finally
            {
                HandleDisconnect(client);
                client.Close();
            }
        }

        private static void HandleLogin(TcpClient client, string nickname)
        {
            lock (_lock)
            {
                // 1. 为新玩家创建数据
                PlayerData newPlayer = new PlayerData
                {
                    Id = _nextPlayerId++,
                    Nickname = nickname,
                    Position = new float[] { 0, 0.5f, 0 }, // 初始位置
                    Color = new float[] {
                        (float)_random.NextDouble(),
                        (float)_random.NextDouble(),
                        (float)_random.NextDouble() 
}
                    };

                // 2. 将新玩家加入列表和字典
                _clients[client] = newPlayer;
                _playerList.Add(newPlayer);

                Console.WriteLine($"玩家 '{nickname}' (ID: {newPlayer.Id}) 已登录。");

                // 3. 向新玩家发送当前所有在线玩家的信息（包括他自己）
                StringBuilder allPlayersMsg = new StringBuilder("PLAYERLIST");
                foreach (var player in _playerList)
                {
                    allPlayersMsg.Append($"|{player.Id},{player.Nickname},{player.Position[0]},{player.Position[1]},{player.Position[2]},{player.Color[0]},{player.Color[1]},{player.Color[2]}");
                }
                SendToClient(client, allPlayersMsg.ToString());

                // 4. 向其他所有玩家广播新玩家加入的消息
                string newPlayerMsg = $"PLAYERJOINED|{newPlayer.Id},{newPlayer.Nickname},{newPlayer.Position[0]},{newPlayer.Position[1]},{newPlayer.Position[2]},{newPlayer.Color[0]},{newPlayer.Color[1]},{newPlayer.Color[2]}";
                Broadcast(newPlayerMsg, client); // 排除新玩家自己
            }
        }

        private static void HandleMove(TcpClient client, float x, float y, float z)
        {
            lock (_lock)
            {
                if (_clients.ContainsKey(client))
                {
                    PlayerData player = _clients[client];
                    player.Position = new float[] { x, y, z };

                    // 广播位置更新给所有客户端（包括发送者）
                    string moveMsg = $"PLAYERMOVED|{player.Id},{x},{y},{z}";
                    Broadcast(moveMsg, null); // null 表示广播给所有人
                }
            }
        }

        private static void HandleDisconnect(TcpClient client)
        {
            lock (_lock)
            {
                if (_clients.ContainsKey(client))
                {
                    PlayerData player = _clients[client];
                    Console.WriteLine($"玩家 '{player.Nickname}' 断开连接。");
                    _playerList.Remove(player);
                    _clients.Remove(client);

                    // 广播玩家离开消息
                    string disconnectMsg = $"PLAYERLEFT|{player.Id}";
                    Broadcast(disconnectMsg, null);
                }
            }
        }

        private static void Broadcast(string message, TcpClient excludeClient)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            List<TcpClient> clientsCopy;
            lock (_lock)
            {
                clientsCopy = new List<TcpClient>(_clients.Keys);
            }

            foreach (var client in clientsCopy)
            {
                if (client != excludeClient)
                {
                    try
                    {
                        client.GetStream().Write(data, 0, data.Length);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"广播消息时出错: {ex.Message}");
                    }
                }
            }
        }

        private static void SendToClient(TcpClient client, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            try
            {
                client.GetStream().Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送消息给客户端时出错: {ex.Message}");
            }
        }

        public static void StopServer()
        {
            _listener?.Stop();
            lock (_lock)
            {
                foreach (var client in _clients.Keys)
                {
                    client.Close();
                }
                _clients.Clear();
                _playerList.Clear();
            }
            Console.WriteLine("服务器已停止。");
        }
    }

    // 玩家数据类
    public class PlayerData
    {
        public int Id { get; set; }
        public string Nickname { get; set; }
        public float[] Position { get; set; } // [x, y, z]
        public float[] Color { get; set; }    // [r, g, b]
    }
}
