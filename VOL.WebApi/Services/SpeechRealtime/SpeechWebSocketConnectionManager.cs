using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;

namespace VOL.WebApi.Services.SpeechRealtime
{
    public interface ISpeechWebSocketConnectionManager
    {
        IReadOnlyDictionary<string, WebSocket> Connections { get; }
        string Add(WebSocket socket);
        bool Remove(string connectionId, out WebSocket socket);
    }

    public class SpeechWebSocketConnectionManager : ISpeechWebSocketConnectionManager
    {
        private readonly ConcurrentDictionary<string, WebSocket> _connections = new ConcurrentDictionary<string, WebSocket>();

        public IReadOnlyDictionary<string, WebSocket> Connections => _connections;

        public string Add(WebSocket socket)
        {
            var id = Guid.NewGuid().ToString("N");
            _connections.TryAdd(id, socket);
            return id;
        }

        public bool Remove(string connectionId, out WebSocket socket)
        {
            return _connections.TryRemove(connectionId, out socket);
        }
    }
}
