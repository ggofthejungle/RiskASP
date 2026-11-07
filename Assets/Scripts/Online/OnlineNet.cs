using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Text messages over the room's Relay connection (Netcode for GameObjects named messages).
// No networked objects: every device runs its own copy of the game and only actions travel.
public static class OnlineNet
{
    public const string ToHost = "ie.toHost";
    public const string ToClients = "ie.toClients";
    const string Hello = "ie.hello";
    const int MaxMessageBytes = 256 * 1024;

    static NetworkManager _nm;
    static bool _listening;

    // Host only: which room player (UGS id) sits behind each network client.
    static readonly Dictionary<ulong, string> _playerOfClient = new();

    // (sender client, channel, text)
    public static event Action<ulong, string, string> Received;
    // Host: a client's connection dropped (UGS player id).
    public static event Action<string> PlayerLeft;
    // Host: a client said hello (first join or coming back).
    public static event Action<string> PlayerJoined;
    // Client: the host's connection dropped.
    public static event Action HostLost;
    // Client: connected to a host (first join, or the new host after a migration).
    public static event Action ConnectedToHost;

    public static bool IsHost => _nm != null && _nm.IsHost;
    public static bool IsRunning => _nm != null && _nm.IsListening;

    // Host: the other devices that are connected now.
    public static IReadOnlyList<ulong> ClientIds =>
        _nm != null && _nm.IsServer
            ? _nm.ConnectedClientsIds.Where(id => id != NetworkManager.ServerClientId).ToList()
            : (IReadOnlyList<ulong>)Array.Empty<ulong>();

    public static string PlayerOfClient(ulong clientId)
    {
        if (_nm != null && clientId == NetworkManager.ServerClientId) return OnlineRoom.MyId;
        return _playerOfClient.TryGetValue(clientId, out var id) ? id : null;
    }

    // The session's Relay handler looks for NetworkManager.Singleton with a UnityTransport.
    public static void EnsureManager()
    {
        if (_nm != null) return;
        var go = new GameObject("OnlineNetwork");
        UnityEngine.Object.DontDestroyOnLoad(go);
        var transport = go.AddComponent<UnityTransport>();
        _nm = go.AddComponent<NetworkManager>();
        _nm.NetworkConfig ??= new NetworkConfig();
        _nm.NetworkConfig.NetworkTransport = transport;
        _nm.NetworkConfig.EnableSceneManagement = false;
        _nm.NetworkConfig.ConnectionApproval = false;
        _nm.OnClientStarted += Listen;
        _nm.OnServerStarted += Listen;
        _nm.OnClientStopped += _ => _listening = false;
        _nm.OnServerStopped += _ => _listening = false;
        _nm.OnClientConnectedCallback += OnConnected;
        _nm.OnClientDisconnectCallback += OnDisconnected;
    }

    // Safe to call more than once; the messaging manager only exists while the network runs.
    public static void Listen()
    {
        if (_nm == null || !_nm.IsListening || _nm.CustomMessagingManager == null || _listening) return;
        _listening = true;
        _playerOfClient.Clear();
        var messages = _nm.CustomMessagingManager;
        messages.RegisterNamedMessageHandler(ToHost, (sender, reader) => Deliver(sender, ToHost, reader));
        messages.RegisterNamedMessageHandler(ToClients, (sender, reader) => Deliver(sender, ToClients, reader));
        messages.RegisterNamedMessageHandler(Hello, OnHello);
        if (!_nm.IsHost) SendHello();
    }

    public static void SendToHost(string text) => Send(ToHost, text, NetworkManager.ServerClientId);

    public static void SendToClient(ulong clientId, string text) => Send(ToClients, text, clientId);

    // Host to every other device.
    public static void SendToClients(string text)
    {
        if (!IsRunning || !_nm.IsServer) return;
        var others = _nm.ConnectedClientsIds.Where(id => id != NetworkManager.ServerClientId).ToList();
        if (others.Count == 0) return;
        using var writer = Write(text);
        _nm.CustomMessagingManager.SendNamedMessage(ToClients, others, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    // Host: this room player's device is connected over Relay and has said hello.
    public static bool HasPlayer(string playerId) =>
        playerId != null && (playerId == OnlineRoom.MyId || _playerOfClient.ContainsValue(playerId));

    public static ulong ClientOfPlayer(string playerId)
    {
        foreach (var kv in _playerOfClient)
            if (kv.Value == playerId) return kv.Key;
        return ulong.MaxValue;
    }

    static void Send(string channel, string text, ulong clientId)
    {
        if (!IsRunning || _nm.CustomMessagingManager == null)
        {
            Debug.LogWarning($"Online: not connected, dropped a {channel} message.");
            return;
        }
        using var writer = Write(text);
        _nm.CustomMessagingManager.SendNamedMessage(channel, clientId, writer, NetworkDelivery.ReliableFragmentedSequenced);
    }

    static FastBufferWriter Write(string text)
    {
        var writer = new FastBufferWriter(1024, Allocator.Temp, MaxMessageBytes);
        writer.WriteValueSafe(text ?? "");
        return writer;
    }

    static void Deliver(ulong sender, string channel, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string text);
        Received?.Invoke(sender, channel, text);
    }

    static void SendHello()
    {
        if (_nm == null || !_nm.IsConnectedClient) return;
        using var writer = Write(OnlineRoom.MyId);
        _nm.CustomMessagingManager.SendNamedMessage(Hello, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
    }

    static void OnHello(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string playerId);
        if (!_nm.IsServer) return;
        _playerOfClient[sender] = playerId;
        PlayerJoined?.Invoke(playerId);
    }

    static void OnConnected(ulong clientId)
    {
        Listen();
        if (!_nm.IsServer && clientId == _nm.LocalClientId)
        {
            SendHello();
            ConnectedToHost?.Invoke();
        }
    }

    static void OnDisconnected(ulong clientId)
    {
        if (_nm == null) return;
        if (_nm.IsServer)
        {
            if (clientId == NetworkManager.ServerClientId) return;
            if (_playerOfClient.TryGetValue(clientId, out var playerId))
            {
                _playerOfClient.Remove(clientId);
                PlayerLeft?.Invoke(playerId);
            }
            return;
        }
        HostLost?.Invoke();
    }
}
