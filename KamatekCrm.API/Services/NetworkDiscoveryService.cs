using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace KamatekCrm.API.Services;

public class ServerDiscoveryMessage
{
    public string ServerName { get; set; } = "KamatekCRM";
    public string ApiUrl { get; set; } = "";
    public string WebUrl { get; set; } = "";
    public string DatabaseHost { get; set; } = "";
    public int Version { get; set; } = 1;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class NetworkDiscoveryService : BackgroundService
{
    private readonly ILogger<NetworkDiscoveryService> _logger;
    private readonly IConfiguration _configuration;
    private UdpClient? _udpClient;
    private readonly int _port;
    private readonly int _intervalSeconds;

    public NetworkDiscoveryService(ILogger<NetworkDiscoveryService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _port = configuration.GetValue("NetworkDiscovery:Port", 5051);
        _intervalSeconds = configuration.GetValue("NetworkDiscovery:IntervalSeconds", 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _port));
            
            _logger.LogInformation("UDP Discovery Service başlatıldı. Port: {Port}, Interval: {Interval}s", _port, _intervalSeconds);

            var apiUrl = _configuration.GetValue("ApiUrl", "http://localhost:5050");
            var webUrl = _configuration.GetValue("WebUrl", "http://localhost:7000");
            var dbHost = _configuration.GetValue("Database:Host", "localhost");
            var serverName = _configuration.GetValue("Server:Name", Environment.MachineName);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var message = new ServerDiscoveryMessage
                    {
                        ServerName = serverName,
                        ApiUrl = apiUrl,
                        WebUrl = webUrl,
                        DatabaseHost = dbHost,
                        Timestamp = DateTime.UtcNow
                    };

                    var json = JsonSerializer.Serialize(message);
                    var data = Encoding.UTF8.GetBytes(json);

                    // Broadcast to all devices on the network
                    var broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, _port);
                    await _udpClient.SendAsync(data, broadcastEndpoint);

                    _logger.LogDebug("Broadcast gönderildi: {ServerName} - {ApiUrl}", serverName, apiUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Broadcast gönderim hatası");
                }

                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UDP Discovery Service hatası");
        }
    }

    public override void Dispose()
    {
        _udpClient?.Dispose();
        base.Dispose();
    }
}
