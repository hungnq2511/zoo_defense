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
    public sealed class UtpLink : INetworkHandler, IPacketLink
    {
        NetworkDriver _driver;
        NetworkPipeline _pipe;
        NetworkConnection _conn;
        bool _host, _started;
        readonly Queue<byte[]> _inbox = new Queue<byte[]>();
        readonly Queue<byte[]> _outbox = new Queue<byte[]>();

        public bool Started { get { return _started; } }
        public bool Connected { get; private set; }
        public bool Disconnected { get; private set; }

        public Task StartAsync(NetworkConfiguration configuration)
        {
            _host = configuration.Role != NetworkRole.Client;
            var data = _host ? configuration.RelayServerData : configuration.RelayClientData;
            if (!data.Endpoint.IsValid) data = _host ? configuration.RelayClientData : configuration.RelayServerData;

            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref data);
            _driver = NetworkDriver.Create(settings);
            _pipe = _driver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
            if (_driver.Bind(NetworkEndpoint.AnyIpv4) != 0) Debug.LogError("[Online] relay bind failed");
            if (_host) _driver.Listen();
            else _conn = _driver.Connect(data.Endpoint);
            _started = true;
            Debug.Log("[Online] relay network started as " + (_host ? "host" : "guest"));
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_driver.IsCreated)
            {
                if (_conn.IsCreated) _driver.Disconnect(_conn);
                _driver.ScheduleUpdate().Complete();
                _driver.Dispose();
            }
            _started = false;
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
                    if (_conn.IsCreated) { _driver.Disconnect(c); continue; }   // 1v1: one guest only
                    _conn = c;
                    Connected = true;
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
                        Connected = true;
                        break;
                    case NetworkEvent.Type.Data:
                    {
                        var bytes = new byte[reader.Length];
                        for (int i = 0; i < bytes.Length; i++) bytes[i] = reader.ReadByte();
                        _inbox.Enqueue(bytes);
                        break;
                    }
                    case NetworkEvent.Type.Disconnect:
                        Connected = false;
                        Disconnected = true;
                        _conn = default(NetworkConnection);
                        break;
                }
            }
            Flush();
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
