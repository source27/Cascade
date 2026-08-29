using System;

namespace Cascade.Service
{
    public sealed class NullNetworkService : INetworkService
    {
        public NetworkConnectionState State { get; private set; } = NetworkConnectionState.Disconnected;

        public void Connect(string address)
        {
            State = NetworkConnectionState.Connected;
        }

        public void Disconnect()
        {
            State = NetworkConnectionState.Disconnected;
        }

        public void Send(byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
        }
    }
}
