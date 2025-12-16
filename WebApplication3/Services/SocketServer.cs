using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ProductInventoryApi.Services;

public class StockNotificationServer : IHostedService, IDisposable
{
    private readonly ILogger<StockNotificationServer> _logger;
    private readonly int _port;
    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _acceptTask;

    private readonly ConcurrentDictionary<string, TcpClient> _connectedClients = new();

    private readonly object _broadcastLock = new();

    public StockNotificationServer(ILogger<StockNotificationServer> logger, IConfiguration configuration)
    {
        _logger = logger;
        _port = configuration.GetValue("SocketServer:Port", 5050);
    }


    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _logger.LogInformation("Stock notification server started on port {Port}", _port);

            // Start accepting clients in background
            _acceptTask = AcceptClientsAsync(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start stock notification server on port {Port}", _port);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping stock notification server...");

        _cancellationTokenSource?.Cancel();

        _listener?.Stop();

        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Accept task did not complete within timeout");
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        DisconnectAllClients();

        _logger.LogInformation("Stock notification server stopped. Total clients disconnected: {Count}",
            _connectedClients.Count);
    }

  
    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                var clientId = GenerateClientId(client);

                if (_connectedClients.TryAdd(clientId, client))
                {
                    _logger.LogInformation("Client connected: {ClientId}. Total clients: {Count}",
                        clientId, _connectedClients.Count);

                    _ = HandleClientAsync(clientId, client, cancellationToken);

                    await SendToClientAsync(client, new NotificationMessage
                    {
                        Type = "Connected",
                        Message = $"Welcome! You are connected to the Stock Notification Server. ClientId: {clientId}",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting client connection");
            }
        }
    }


    private async Task HandleClientAsync(string clientId, TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();
            var buffer = new byte[1024];

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                try
                {
                    if (stream.DataAvailable)
                    {
                        var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        var message = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                        _logger.LogDebug("Received from {ClientId}: {Message}", clientId, message);

                        if (message.Equals("PING", StringComparison.OrdinalIgnoreCase))
                        {
                            await SendToClientAsync(client, new NotificationMessage
                            {
                                Type = "Pong",
                                Message = "PONG",
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }
                    else
                    {
                        await Task.Delay(100, cancellationToken);
                    }
                }
                catch (IOException)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client {ClientId}", clientId);
        }
        finally
        {
            RemoveClient(clientId);
        }
    }

    public async Task BroadcastStockUpdateAsync(int productId, string productName, int oldQuantity, int newQuantity)
    {
        var notification = new NotificationMessage
        {
            Type = "StockUpdate",
            Message = $"Stock updated for '{productName}' (ID: {productId}): {oldQuantity} → {newQuantity}",
            Timestamp = DateTime.UtcNow,
            Data = new
            {
                ProductId = productId,
                ProductName = productName,
                OldQuantity = oldQuantity,
                NewQuantity = newQuantity,
                Change = newQuantity - oldQuantity
            }
        };

        await BroadcastAsync(notification);
    }

    public async Task BroadcastMaintananceStockUpdate()
    {
        var notification = new NotificationMessage
        {
            Type = "StockUpdate",
            Message = $"added 2 quantities for all products",
            Timestamp = DateTime.UtcNow,
       
        };

        await BroadcastAsync(notification);
    }

    private async Task BroadcastAsync(NotificationMessage notification)
    {
        var clientsToRemove = new List<string>();
        var json = JsonSerializer.Serialize(notification) + "\n";
        var data = Encoding.UTF8.GetBytes(json);

        // Create a snapshot of clients to iterate safely
        var clientSnapshot = _connectedClients.ToArray();

        foreach (var kvp in clientSnapshot)
        {
            try
            {
                if (kvp.Value.Connected)
                {
                    var stream = kvp.Value.GetStream();
                    await stream.WriteAsync(data);
                    await stream.FlushAsync();
                }
                else
                {
                    clientsToRemove.Add(kvp.Key);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send to client {ClientId}", kvp.Key);
                clientsToRemove.Add(kvp.Key);
            }
        }

        foreach (var clientId in clientsToRemove)
        {
            RemoveClient(clientId);
        }

        if (clientSnapshot.Length > 0)
        {
            _logger.LogInformation("Broadcast sent to {Count} clients: {Type}",
                clientSnapshot.Length - clientsToRemove.Count, notification.Type);
        }
    }

  
    private async Task SendToClientAsync(TcpClient client, NotificationMessage notification)
    {
        try
        {
            if (client.Connected)
            {
                var json = JsonSerializer.Serialize(notification) + "\n";
                var data = Encoding.UTF8.GetBytes(json);
                var stream = client.GetStream();
                await stream.WriteAsync(data);
                await stream.FlushAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send message to client");
        }
    }

    private void RemoveClient(string clientId)
    {
        if (_connectedClients.TryRemove(clientId, out var client))
        {
            try
            {
                client.Close();
                client.Dispose();
            }
            catch
            {
            }
            _logger.LogInformation("Client disconnected: {ClientId}. Remaining clients: {Count}",
                clientId, _connectedClients.Count);
        }
    }

    private void DisconnectAllClients()
    {
        foreach (var kvp in _connectedClients)
        {
            try
            {
                kvp.Value.Close();
                kvp.Value.Dispose();
            }
            catch
            {
            }
        }
        _connectedClients.Clear();
    }


    private static string GenerateClientId(TcpClient client)
    {
        var endpoint = client.Client.RemoteEndPoint as IPEndPoint;
        return $"{endpoint?.Address}:{endpoint?.Port}-{Guid.NewGuid():N}";
    }


    public int ConnectedClientCount => _connectedClients.Count;

    public void Dispose()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _listener?.Stop();
        DisconnectAllClients();
    }
}

public class NotificationMessage
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Data { get; set; }
}

