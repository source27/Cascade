namespace Cascade.Service
{
    public enum NetworkConnectionState
    {
        Disconnected,
        Connecting,
        Connected
    }

    public interface INetworkService
    {
        NetworkConnectionState State { get; }
        void Connect(string address);
        void Disconnect();
        void Send(byte[] payload);
    }
}
