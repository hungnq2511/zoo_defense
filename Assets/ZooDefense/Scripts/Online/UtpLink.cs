using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Services.Multiplayer;
using UnityEngine;
using ZooDefense.Core;

namespace ZooDefense.Online
{
    /// <summary>
    /// The session system's network hook: when a session's relay connection is ready it calls StartAsync with the
    /// relay data, and we open a plain Unity Transport driver on it (no Netcode: the game only exchanges small
    /// lockstep packets). Reliable + ordered + fragmentation, so every message arrives once, in order.
    /// </summary>
    public sealed class UtpLink : INetworkHandler, IPacketLink, ILinkHealth
    {
        NetworkDriver _driver;
        NetworkPipeline _pipe;
        NetworkConnection _conn;
        bool _host, _started, _everConnected;
        RelayServerData _relay;
        float _downSince = -1f, _nextRetry;
        /// <summary>Times the relay connection was rebuilt during the game.</summary>
        public int Rebuilds { get; private set; }
        readonly Queue<byte[]> _inbox = new Queue<byte[]>();
        readonly Queue<byte[]> _outbox = new Queue<byte[]>();

        public bool Started { get { return _started; } }
        public bool Connected { get; private set; }
        public bool Disconnected { get; private set; }
        /// <summary>This phone still has a network (decides who lost a dropped match).</summary>
        public bool SelfOnline { get { return Application.internetReachability != NetworkReachability.NotReachable; } }

        public Task StartAsync(NetworkConfiguration configuration)
        {
            if (_driver.IsCreated) DisposeDriver();   // the session system may start the network again (reconnect)
            _host = configuration.Role != NetworkRole.Client;
            var data = _host ? configuration.RelayServerData : configuration.RelayClientData;
            if (!data.Endpoint.IsValid) data = _host ? configuration.RelayClientData : configuration.RelayServerData;
            _relay = data;
            Open();
            Debug.Log("[Online] relay network started as " + (_host ? "host" : "guest"));
            return Task.CompletedTask;
        }

        void Open()
        {
            var data = _relay;
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref data);
            // Notice a dead link within a few seconds (default is 30 s), so the reconnect starts early.
            settings.WithNetworkConfigParameters(connectTimeoutMS: 1000, maxConnectAttempts: 8, disconnectTimeoutMS: 6000, heartbeatTimeoutMS: 1000);
            _driver = NetworkDriver.Create(settings);
            _pipe = _driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            if (_driver.Bind(NetworkEndpoint.AnyIpv4) != 0) Debug.LogError("[Online] relay bind failed");
            if (_host) _driver.Listen();
            else _conn = _driver.Connect(data.Endpoint);
            _started = true;
        }

        /// <summary>
        /// The link dropped during a game (Wi-Fi to mobile data, a tunnel): rebuild the driver on the same relay
        /// allocation and connect again. The guest reconnects to the host; a host whose own network went away binds
        /// to the relay again and waits for the guest. Lockstep then resends the missed frames (Resume).
        /// </summary>
        void TryRestore()
        {
            if (!_everConnected || Connected) { _downSince = -1f; return; }
            float now = Time.realtimeSinceStartup;
            if (_downSince < 0f) { _downSince = now; _nextRetry = now + (_host ? 2.5f : 0.5f); return; }
            if (now < _nextRetry || !SelfOnline) return;
            // The host only rebuilds when its relay binding is gone; otherwise the guest is the one coming back.
            if (_host && _driver.IsCreated && _driver.GetRelayConnectionStatus() == RelayConnectionStatus.Established) { _nextRetry = now + 1f; return; }
            _nextRetry = now + 2f;
            Rebuilds++;
            Debug.Log("[Online] restoring the relay link (" + (_host ? "host" : "guest") + ", try " + Rebuilds + ")");
            DisposeDriver();
            _conn = default(NetworkConnection);
            Open();
        }

        public Task StopAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposeDriver();
            _started = false;
            _everConnected = false;
            Connected = false;
        }

        void DisposeDriver()
        {
            if (_driver.IsCreated)
            {
                if (_conn.IsCreated) _driver.Disconnect(_conn);
                _driver.ScheduleUpdate().Complete();
                _driver.Dispose();
            }
            _driver = default(NetworkDriver);
            Connected = false;
        }

        /// <summary>Pumps the driver: accepts the guest, reads packets, flushes queued sends. Call every frame.</summary>
        public void Update()
        {
            if (!_started || !_driver.IsCreated) return;
            _driver.ScheduleUpdate().Complete();

            if (_host)
            {
                NetworkConnection c;
                while ((c = _driver.Accept()) != default(NetworkConnection))
                {
                    // 1v1 in a locked session: a new connection is the guest coming back; drop the stale one.
                    if (_conn.IsCreated && _conn != c) _driver.Disconnect(_conn);
                    _conn = c;
                    MarkConnected();
                }
            }

            NetworkEvent.Type type;
            NetworkConnection from;
            Unity.Collections.DataStreamReader reader;
            while ((type = _driver.PopEvent(out from, out reader)) != NetworkEvent.Type.Empty)
            {
                switch (type)
                {
                    case NetworkEvent.Type.Connect:
                        MarkConnected();
                        break;
                    case NetworkEvent.Type.Data:
                    {
                        var bytes = new byte[reader.Length];
                        for (int i = 0; i < bytes.Length; i++) bytes[i] = reader.ReadByte();
                        _inbox.Enqueue(bytes);
                        break;
                    }
                    case NetworkEvent.Type.Disconnect:
                        if (from != _conn) break;   // an old connection replaced by a newer one
                        Connected = false;
                        Disconnected = true;
                        _conn = default(NetworkConnection);
                        break;
                }
            }
            Flush();
            if (!Connected && _everConnected) TryRestore();
        }

        void MarkConnected()
        {
            if (_everConnected && !Connected) Debug.Log("[Online] relay link restored");
            Connected = true;
            Disconnected = false;
            _everConnected = true;
            _downSince = -1f;
        }

        void Flush()
        {
            while (_outbox.Count > 0 && Connected && _conn.IsCreated)
            {
                var data = _outbox.Peek();
                Unity.Collections.DataStreamWriter w;
                if (_driver.BeginSend(_pipe, _conn, out w, data.Length) != 0) return;   // send queue full: retry next frame
                for (int i = 0; i < data.Length; i++) w.WriteByte(data[i]);
                if (_driver.EndSend(w) < 0) return;
                _outbox.Dequeue();
            }
        }

        public void Send(byte[] data)
        {
            _outbox.Enqueue(data);
            if (_started && _driver.IsCreated) Flush();
        }

        public bool TryReceive(out byte[] data)
        {
            if (_inbox.Count > 0) { data = _inbox.Dequeue(); return true; }
            data = null;
            return false;
        }
    }
}
