using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [Header("Connection Settings")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 8888;

    [Header("Game References")]
    public GameObject playerPrefab;
    public Transform spawnPoint;

    private TcpClient _client;
    private NetworkStream _stream;
    private Thread _receiveThread;
    
    // ★ 关键修复：添加退出标志
    private volatile bool _isConnected = false;
    private volatile bool _shouldExit = false;
    
    private Queue<Action> _mainThreadActions = new Queue<Action>();
    private Dictionary<int, GameObject> _playerObjects = new Dictionary<int, GameObject>();
    private int _localPlayerId = -1;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        while (_mainThreadActions.Count > 0)
        {
            _mainThreadActions.Dequeue()?.Invoke();
        }
    }

    public void ConnectToServer(string nickname)
    {
        try
        {
            _shouldExit = false;
            _client = new TcpClient();
            _client.Connect(serverIP, serverPort);
            _stream = _client.GetStream();
            _isConnected = true;

            SendMessageToServer($"LOGIN|{nickname}");

            _receiveThread = new Thread(ReceiveData);
            _receiveThread.IsBackground = true; // ★ 设置为后台线程
            _receiveThread.Start();

            Debug.Log("成功连接到服务器！");
        }
        catch (Exception e)
        {
            Debug.LogError($"连接服务器失败: {e.Message}");
        }
    }

    private void ReceiveData()
    {
        byte[] buffer = new byte[1024];

        try
        {
            // ★ 关键修复：添加退出检查条件
            while (!_shouldExit && _isConnected && _client.Connected)
            {
                // ★ 使用 ReadTimeout 避免永久阻塞
                _stream.ReadTimeout = 500; // 500毫秒超时
                
                if (_stream.DataAvailable)
                {
                    int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        string[] parts = message.Split('|');
                        string command = parts[0];
                        _mainThreadActions.Enqueue(() => ProcessMessage(command, parts));
                    }
                }
                else
                {
                    // ★ 没有数据时短暂休眠，减少CPU占用并允许检查退出标志
                    Thread.Sleep(10);
                }
            }
        }
        catch (IOException)
        {
            // ★ 超时异常，正常退出时会发生
            if (!_shouldExit)
            {
                Debug.Log("服务器连接超时");
            }
        }
        catch (Exception e)
        {
            if (!_shouldExit)
            {
                Debug.LogError($"接收数据错误: {e.Message}");
            }
        }
        finally
        {
            Debug.Log("接收线程已退出");
        }
    }

    private void ProcessMessage(string command, string[] parts)
    {
        switch (command)
        {
            case "PLAYERLIST":
                HandlePlayerList(parts);
                break;
            case "PLAYERJOINED":
                HandlePlayerJoined(parts[1]);
                break;
            case "PLAYERMOVED":
                HandlePlayerMoved(parts[1]);
                break;
            case "PLAYERLEFT":
                HandlePlayerLeft(int.Parse(parts[1]));
                break;
        }
    }

    private void HandlePlayerList(string[] parts)
    {
        for (int i = 1; i < parts.Length; i++)
        {
            string[] playerData = parts[i].Split(',');
            CreateOrUpdatePlayer(playerData);
        }
        Debug.Log($"已加载 {parts.Length - 1} 个玩家。");
    }

    private void HandlePlayerJoined(string playerDataStr)
    {
        string[] playerData = playerDataStr.Split(',');
        CreateOrUpdatePlayer(playerData);
        Debug.Log($"新玩家加入: {playerData[1]}");
    }

    private void HandlePlayerMoved(string playerDataStr)
    {
        string[] data = playerDataStr.Split(',');
        int playerId = int.Parse(data[0]);
        Vector3 newPos = new Vector3(float.Parse(data[1]), float.Parse(data[2]), float.Parse(data[3]));

        if (_playerObjects.ContainsKey(playerId))
        {
            PlayerController pc = _playerObjects[playerId].GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.SetTargetPosition(newPos);
            }
        }
    }

    private void HandlePlayerLeft(int playerId)
    {
        if (_playerObjects.ContainsKey(playerId))
        {
            Destroy(_playerObjects[playerId]);
            _playerObjects.Remove(playerId);
            Debug.Log($"玩家ID {playerId} 已离开。");
        }
    }

    private void CreateOrUpdatePlayer(string[] data)
    {
        int id = int.Parse(data[0]);
        string nickname = data[1];
        Vector3 pos = new Vector3(float.Parse(data[2]), float.Parse(data[3]), float.Parse(data[4]));
        Color color = new Color(float.Parse(data[5]), float.Parse(data[6]), float.Parse(data[7]));

        GameObject playerObj;

        if (!_playerObjects.ContainsKey(id))
        {
            playerObj = Instantiate(playerPrefab, pos, Quaternion.identity);
            PlayerController pc = playerObj.GetComponent<PlayerController>();
            pc.Initialize(id, nickname, color);
            _playerObjects[id] = playerObj;

            if (_localPlayerId == -1)
            {
                _localPlayerId = id;
                pc.SetAsLocalPlayer();
            }
        }
        else
        {
            playerObj = _playerObjects[id];
            playerObj.transform.position = pos;
            PlayerController pc = playerObj.GetComponent<PlayerController>();
            if (pc != null) pc.UpdateVisuals(nickname, color);
        }
    }

    public void SendMessageToServer(string message)
    {
        if (_isConnected && _stream != null && _stream.CanWrite)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                _stream.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                Debug.LogError($"发送消息失败: {e.Message}");
            }
        }
    }

    public void SendMoveRequest(Vector3 position)
    {
        SendMessageToServer($"MOVE|{position.x}|{position.y}|{position.z}");
    }

    // ★ 关键修复：改进的断开连接方法
    public void Disconnect()
    {
        Debug.Log("开始断开连接...");
        
        // 1. 设置退出标志
        _shouldExit = true;
        _isConnected = false;

        // 2. 关闭网络流（这会使阻塞的 Read 操作抛出异常）
        try
        {
            _stream?.Close();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"关闭流时出错: {e.Message}");
        }

        try
        {
            _client?.Close();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"关闭客户端时出错: {e.Message}");
        }

        // 3. 等待线程结束（设置超时，避免永久等待）
        if (_receiveThread != null && _receiveThread.IsAlive)
        {
            if (!_receiveThread.Join(1000)) // 最多等待1秒
            {
                Debug.LogWarning("接收线程未能正常退出，强制中断");
                // ★ 线程会因为流关闭而自然退出，不需要 Abort
            }
        }

        // 4. 清理玩家对象
        foreach (var obj in _playerObjects.Values)
        {
            if (obj != null) Destroy(obj);
        }
        _playerObjects.Clear();
        _localPlayerId = -1;

        Debug.Log("已断开服务器连接。");
    }

    // ★ 多个生命周期方法中都调用断开连接
    void OnApplicationQuit()
    {
        Disconnect();
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void OnDisable()
    {
        Disconnect();
    }
}
