using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace StockNotificationClient;


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

    private static async Task ConnectAndListenAsync()
    {
        Console.WriteLine($"\n[INFO] Connecting to server at {ServerHost}:{ServerPort}...");

        using var client = new TcpClient();
        await client.ConnectAsync(ServerHost, ServerPort);

        PrintSuccess($"Connected to Stock Notification Server!");
        Console.WriteLine("[INFO] Waiting for stock update notifications...");
        Console.WriteLine("[INFO] Press Ctrl+C to disconnect.\n");
        Console.WriteLine(new string('─', 60));

        using var stream = client.GetStream();
        var buffer = new byte[4096];
        var messageBuilder = new StringBuilder();

        var pingTask = SendPingAsync(stream);

        while (_isRunning && client.Connected)
        {
            try
            {
                if (stream.DataAvailable)
                {
                    var bytesRead = await stream.ReadAsync(buffer);

                    if (bytesRead == 0)
                    {
                        Console.WriteLine("[WARN] Server closed the connection.");
                        break;
                    }

                    var receivedText = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(receivedText);

                    var content = messageBuilder.ToString();
                    var messages = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

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

                    foreach (var message in messages)
                    {
                        ProcessMessage(message.Trim());
                    }
                }
                else
                {
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

  
    private static async Task SendPingAsync(NetworkStream stream)
    {
        //as long is the client is running send ping every 30 seconds to keep the connection alive

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
                break;
            }
        }
    }


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
                    PrintInfo("Received Pong");
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


    private static void PrintStockUpdate(NotificationMessage notification)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Welcome to stock update notification");
        Console.ResetColor();

        Console.WriteLine($"  Time: {notification.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
        Console.WriteLine($"  {notification.Message}");

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
            }
        }

        Console.WriteLine(new string('─', 60));
        Console.WriteLine();
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@" Initiate stock notification client
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


public class NotificationMessage
{
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public object? Data { get; set; }
}


public class StockUpdateData
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int OldQuantity { get; set; }
    public int NewQuantity { get; set; }
    public int Change { get; set; }
}

