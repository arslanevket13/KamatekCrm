using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace KamatekCrm.Services;

public class ServerInfo
{
    public string ServerName { get; set; } = "";
    public string ApiUrl { get; set; } = "";
    public string WebUrl { get; set; } = "";
    public string DatabaseHost { get; set; } = "";
    public int Version { get; set; }
    public DateTime Timestamp { get; set; }
    public IPAddress? IpAddress { get; set; }
}

public class NetworkDiscoveryService
{
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cts;
    private readonly int _port;
    private readonly Dispatcher _dispatcher;

    public event EventHandler<ServerInfo>? ServerDiscovered;
    public event EventHandler<ServerInfo>? ServerLost;

    private ServerInfo? _lastKnownServer;
    private readonly TimeSpan _serverTimeout = TimeSpan.FromSeconds(15);

    public NetworkDiscoveryService(int port = 5051)
    {
        _port = port;
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ListenAsync(_cts.Token));
        System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Dinleme başlatıldı. Port: {_port}");
    }

    public void Stop()
    {
        _cts?.Cancel();
        _udpClient?.Close();
        _udpClient?.Dispose();
        _udpClient = null;
        System.Diagnostics.Debug.WriteLine("[NetworkDiscovery] Dinleme durduruldu.");
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        try
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            _udpClient.EnableBroadcast = true;

            System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Port {_port}'de dinleniyor...");

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(cancellationToken);
                    var json = Encoding.UTF8.GetString(result.Buffer);
                    var message = JsonSerializer.Deserialize<ServerDiscoveryMessage>(json);

                    if (message != null && message.ServerName == "KamatekCRM")
                    {
                        var serverInfo = new ServerInfo
                        {
                            ServerName = message.ServerName,
                            ApiUrl = message.ApiUrl,
                            WebUrl = message.WebUrl,
                            DatabaseHost = message.DatabaseHost,
                            Version = message.Version,
                            Timestamp = message.Timestamp,
                            IpAddress = result.RemoteEndPoint.Address
                        };

                        var isNew = _lastKnownServer?.ApiUrl != serverInfo.ApiUrl;
                        _lastKnownServer = serverInfo;

                        _dispatcher.BeginInvoke(() =>
                        {
                            if (isNew)
                            {
                                ServerDiscovered?.Invoke(this, serverInfo);
                                System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Sunucu bulundu: {serverInfo.ApiUrl}");
                            }
                        });
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Dinleme hatası: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NetworkDiscovery] Başlatma hatası: {ex.Message}");
        }
    }

    public ServerInfo? GetLastKnownServer()
    {
        return _lastKnownServer;
    }

    public bool IsServerAvailable()
    {
        if (_lastKnownServer == null) return false;
        return DateTime.UtcNow - _lastKnownServer.Timestamp < _serverTimeout;
    }

    private class ServerDiscoveryMessage
    {
        public string ServerName { get; set; } = "";
        public string ApiUrl { get; set; } = "";
        public string WebUrl { get; set; } = "";
        public string DatabaseHost { get; set; } = "";
        public int Version { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
