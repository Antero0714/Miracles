using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System;
using System.Collections.Generic;
using GameServer;

public class Client : MonoBehaviour
{
    public static Client instance;
    public static int dataBufferSize = 4096;

    public string ip = "127.0.0.1";
    public int port = 8880;
    public int myId = 0;
    public TCP tcp;

    private bool isConnected = false;
    private delegate void PacketHandler(Packet _packet);
    private static Dictionary<int, PacketHandler> packetHandlers;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.Log("���������� ��� ����������, ����� ������ ���������");
            Destroy(this);
        }
    }

    private void Start()
    {
        InitializeClientData(); // ��������� ������������� ������������ �������
        tcp = new TCP();
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void ConnectToServer()
    {
        InitializeClientData();

        isConnected = true;
        tcp.Connect();
    }

    public class TCP
    {
        public TcpClient socket;

        private NetworkStream stream;
        private Packet receivedData;
        private byte[] receiveBuffer;

        public void Connect()
        {
            socket = new TcpClient
            {
                ReceiveBufferSize = dataBufferSize,
                SendBufferSize = dataBufferSize
            };

            receiveBuffer = new byte[dataBufferSize];
            socket.BeginConnect(instance.ip, instance.port, ConnectCallback, socket);
        }

        private void ConnectCallback(IAsyncResult _result)
        {
            socket.EndConnect(_result);

            if (!socket.Connected)
            {
                return;
            }

            stream = socket.GetStream();

            receivedData = new Packet();
            stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
        }

        public void SendData(Packet _packet)
        {
            try
            {
                if (socket != null)
                {
                    _packet.WriteLength(); // ��������� ����� ������
                    stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null);
                }
            }
            catch (Exception _ex)
            {
                Debug.Log($"Error sending data to server via TCP: {_ex}");
            }
        }

        private void ReceiveCallback(IAsyncResult _result)
        {
            try
            {
                int _byteLength = stream.EndRead(_result);
                if (_byteLength <= 0)
                {
                    instance.Disconnect();
                    return;
                }

                byte[] _data = new byte[_byteLength];
                Array.Copy(receiveBuffer, _data, _byteLength);

                receivedData.Reset(HandleData(_data));
                stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        private bool HandleData(byte[] _data)
        {
            int _packetLength = 0;

            receivedData.SetBytes(_data);

            if (receivedData.UnreadLength() >= 4)
            {
                _packetLength = receivedData.ReadInt();
                if (_packetLength <= 0)
                {
                    return true;
                }
            }

            while (_packetLength > 0 && _packetLength <= receivedData.UnreadLength())
            {
                byte[] _packetBytes = receivedData.ReadBytes(_packetLength);
                ThreadManager.ExecuteOnMainThread(() =>
                {
                    using (Packet _packet = new Packet(_packetBytes))
                    {
                        int _packetId = _packet.ReadInt();
                        Debug.Log($"������� ����� � ID: {_packetId}"); // �������� ID ������
                        if (packetHandlers.ContainsKey(_packetId))
                        {
                            packetHandlers[_packetId](_packet); // ������������ �����
                        }
                        else
                        {
                            Debug.LogError($"����������� ����� ID: {_packetId}"); // ������, ���� ID ���
                        }
                    }
                });

                _packetLength = 0;
                if (receivedData.UnreadLength() >= 4)
                {
                    _packetLength = receivedData.ReadInt();
                    if (_packetLength <= 0)
                    {
                        return true;
                    }
                }
            }

            if (_packetLength <= 1)
            {
                return true;
            }

            return false;
        }

        private void Disconnect()
        {
            instance.Disconnect();

            stream = null;
            receivedData = null;
            receiveBuffer = null;
            socket = null;
        }
    }

    private void InitializeClientData()
    {
        packetHandlers = new Dictionary<int, PacketHandler>()
    {
        { (int)ServerPackets.welcome, ClientHandle.Welcome },
        { (int)ServerPackets.spawnPlayer, ClientHandle.SpawnPlayer },
        { (int)ServerPackets.playerDisconnected, ClientHandle.PlayerDisconnected },
        { (int)ServerPackets.playerData, ClientHandle.PlayerData },
        { (int)ServerPackets.playerPosition, ClientHandle.PlayerPosition },
        { (int)ServerPackets.playerRotation, ClientHandle.PlayerRotation },
        { (int)ServerPackets.drumSpinResult, ClientHandle.DrumSpinResult },
        { (int)ServerPackets.ratingUpdate, ClientHandle.PlayerData }, // ���� PlayerData ��������� �������
        { (int)ServerPackets.letterResult, ClientHandle.LetterResult },
        { (int)ServerPackets.winAnnouncement, ClientHandle.WinAnnouncement }
    };

        Debug.Log("���������������� ������.");
    }

    private void Disconnect()
    {
        if (isConnected)
        {
            isConnected = false;
            tcp.socket.Close();

            Debug.Log("Disconnected from server.");
        }
    }
}