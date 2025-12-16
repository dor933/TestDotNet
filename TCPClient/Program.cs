using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace StockNotificationClient;

/// <summary>
/// Console Application that acts as a TCP Socket Client.
/// Connects to the Stock Notification Server and receives real-time stock updates.
/// 
/// Usage:
/// 1. Start the Web API server (which includes the TcpListener on port 5050)
/// 2. Run this console app
/// 3. When stock is updated via PUT /api/products/{id}/stock, this app receives the notification
/// </summary>
class Program
{
    private const string ServerHost = "127.0.0.1";
    private const int ServerPort = 5050;
    private static bool _isRunning = true;

    static async Task Main(string[] args)
    {
        Console.Title = "Stock Notification Client";
        PrintHeader();

        // Handle Ctrl+C for graceful shutdown
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            _isRunning = false;
            Console.WriteLine("\n[INFO] Shutdown signal received...");
        };

        while (_isRunning)
        {
            try
            {
                await ConnectAndListenAsync();
            }
            catch (Exception ex)
            {
                PrintError($"Connection error: {ex.Message}");

                if (_isRunning)
                {
                    Console.WriteLine("[INFO] Reconnecting in 5 seconds...");
                    await Task.Delay(5000);
                }
            }
        }

        Console.WriteLine("[INFO] Client shutdown complete.");
    }

    /// <summary>
    /// Connects to the TCP server and listens for stock update notifications.
    /// Uses TcpClient from System.Net.Sockets namespace.
    /// </summary>
    private static async Task ConnectAndListenAsync()
    {
        Console.WriteLine($"\n[INFO] Connecting to server at {ServerHost}:{ServerPort}...");

        // Create TcpClient and connect to the server
        using var client = new TcpClient();
        await client.ConnectAsync(ServerHost, ServerPort);

        PrintSuccess($"Connected to Stock Notification Server!");
        Console.WriteLine("[INFO] Waiting for stock update notifications...");
        Console.WriteLine("[INFO] Press Ctrl+C to disconnect.\n");
        Console.WriteLine(new string('─', 60));

        // Get the network stream for reading data
        using var stream = client.GetStream();
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        // Keep-alive ping task
        var pingTask = SendPingAsync(stream);

        while (_isRunning && client.Connected)
        {
            try
            {
                // Check if data is available
                if (stream.DataAvailable)
                {
                    var bytesRead = await stream.ReadAsync(buffer);

                    if (bytesRead == 0)
                    {
                        Console.WriteLine("[WARN] Server closed the connection.");
                        break;
                    }

                    // Append received data to message builder
                    var receivedText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(receivedText);

                    // Process complete messages (separated by newlines)
                    var content = messageBuilder.ToString();
                    var messages = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    // Keep incomplete message in buffer
                    if (!content.EndsWith('\n') && messages.Length > 0)
                    {
                        messageBuilder.Clear();
                        messageBuilder.Append(messages[^1]);
                        messages = messages[..^1];
                    }
                    else
                    {
                        messageBuilder.Clear();
                    }

                    // Process each complete message
                    foreach (var message in messages)
                    {
                        ProcessMessage(message.Trim());
                    }
                }
                else
                {
                    // Small delay to prevent busy-waiting
                    await Task.Delay(100);
                }
            }
            catch (IOException ex)
            {
                PrintError($"Connection lost: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>
    /// Sends periodic PING messages to keep the connection alive.
    /// </summary>
    private static async Task SendPingAsync(NetworkStream stream)
    {
        while (_isRunning)
        {
            try
            {
                await Task.Delay(30000); // Ping every 30 seconds

                if (stream.CanWrite)
                {
                    var pingData = Encoding.UTF8.GetBytes("PING\n");
                    await stream.WriteAsync(pingData);
                    await stream.FlushAsync();
                }
            }
            catch
            {
                // Ignore ping errors
                break;
            }
        }
    }

    /// <summary>
    /// Processes a received JSON message from the server.
    /// </summary>
    private static void ProcessMessage(string json)
    {
        try
        {
            var notification = JsonSerializer.Deserialize<NotificationMessage>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (notification == null) return;

            switch (notification.Type)
            {
                case "Connected":
                    PrintInfo($"Server: {notification.Message}");
                    break;

                case "StockUpdate":
                    PrintStockUpdate(notification);
                    break;

                case "Pong":
                    // Keep-alive response, no need to display
                    break;

                default:
                    Console.WriteLine($"[MSG] {notification.Type}: {notification.Message}");
                    break;
            }
        }
        catch (JsonException)
        {
            Console.WriteLine($"[RAW] {json}");
        }
    }

    /// <summary>
    /// Displays a formatted stock update notification.
    /// </summary>
    private static void PrintStockUpdate(NotificationMessage notification)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              📦 STOCK UPDATE NOTIFICATION                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.ResetColor();

        Console.WriteLine($"  Time: {notification.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
        Console.WriteLine($"  {notification.Message}");

        // Parse the Data object if available
        if (notification.Data != null)
        {
            try
            {
                var dataJson = notification.Data.ToString();
                if (!string.IsNullOrEmpty(dataJson))
                {
                    var data = JsonSerializer.Deserialize<StockUpdateData>(dataJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (data != null)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"  Product ID:   {data.ProductId}");
                        Console.WriteLine($"  Product Name: {data.ProductName}");
                        Console.Write($"  Stock Change: {data.OldQuantity} → {data.NewQuantity} ");

                        if (data.Change > 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"(+{data.Change})");
                        }
                        else if (data.Change < 0)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"({data.Change})");
                        }
                        else
                        {
                            Console.WriteLine("(no change)");
                        }
                        Console.ResetColor();
                    }
                }
            }
            catch
            {
                // Ignore parsing errors for data
            }
        }

        Console.WriteLine(new string('─', 60));
        Console.WriteLine();
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════╗
║                                                               ║
║     📡 STOCK NOTIFICATION CLIENT (TCP Socket)                 ║
║                                                               ║
║     Listens for real-time stock updates from the API server   ║
║     Using: System.Net.Sockets.TcpClient                       ║
║                                                               ║
╚═══════════════════════════════════════════════════════════════╝
");
        Console.ResetColor();
    }

    private static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] {message}");
        Console.ResetColor();
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] {message}");
        Console.ResetColor();
    }

    private static void PrintInfo(string message)
    {
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine($"[INFO] {message}");
        Console.ResetColor();
    }
}

/// <summary>
/// Notification message structure matching the server's format.
/// </summary>
public class NotificationMessage
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Data { get; set; }
}

/// <summary>
/// Stock update data structure.
/// </summary>
public class StockUpdateData
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int OldQuantity { get; set; }
    public int NewQuantity { get; set; }
    public int Change { get; set; }
}

