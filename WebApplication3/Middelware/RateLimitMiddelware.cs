using System.Collections.Concurrent;
using System.Net;

namespace ProductInventoryApi.Middleware;

/// <summary>
/// Configuration options for the rate limiting middleware.
/// </summary>
public class RateLimitingOptions
{
    /// <summary>
    /// The configuration section name for rate limiting options.
    /// </summary>
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Gets or sets the maximum number of requests allowed within the time window. Default is 10.
    /// </summary>
    public int MaxRequests { get; set; } = 10;

    /// <summary>
    /// Gets or sets the time window size in seconds for rate limiting. Default is 60 seconds.
    /// </summary>
    public int WindowSizeInSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the HTTP methods that are subject to rate limiting.
    /// </summary>
    public string[] LimitedMethods { get; set; } = { "POST", "PUT", "DELETE" };

    /// <summary>
    /// Gets or sets the URL paths that are subject to rate limiting.
    /// </summary>
    public string[] LimitedPaths { get; set; } = { "/api/products" };
}

internal class ClientRequestTracker
{
    private readonly object _lock = new();
    private readonly Queue<DateTime> _requestTimestamps = new();

    public int RequestCount
    {
        get
        {
            //prevent race condition
            lock (_lock)
            {
                return _requestTimestamps.Count;
            }
        }
    }


    public DateTime GetLastRequestTime()
    {
        lock (_lock)
        {
            if (_requestTimestamps.Count == 0)
            {
         
                return DateTime.MinValue;
            }

   
            return _requestTimestamps.Last();
        }
    }
    public bool TryRecordRequest(int maxRequests, TimeSpan windowSize)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var windowStart = now - windowSize;

            // Remove request that not relevant to the window if any
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
            {
                _requestTimestamps.Dequeue();
            }

            // Check if we're at the limit
            if (_requestTimestamps.Count >= maxRequests)
            {
                return false;
            }

            // Record the new request
            _requestTimestamps.Enqueue(now);

            return true;
        }
    }


    public TimeSpan GetRetryAfter(TimeSpan windowSize)
    {
        lock (_lock)
        {
            if (_requestTimestamps.Count == 0)
            {
                return TimeSpan.Zero;
            }

            var oldestRequest = _requestTimestamps.Peek();
            var retryAfter = (oldestRequest + windowSize) - DateTime.UtcNow;
            return retryAfter > TimeSpan.Zero ? retryAfter : TimeSpan.Zero;
        }
    }
}

/// <summary>
/// Middleware that implements rate limiting based on client IP address.
/// Limits the number of requests a client can make within a configurable time window.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly RateLimitingOptions _options;
    private readonly ConcurrentDictionary<string, ClientRequestTracker> _clientTrackers;
    private readonly Timer _cleanupTimer;

    //will run each 5 minutes to delete from the _clientTrackers clients that doenst sent request in the last 2 mintues , to prevent memoery leak
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitingMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="configuration">The application configuration to retrieve rate limiting settings.</param>
    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _options = new RateLimitingOptions();
        configuration.GetSection(RateLimitingOptions.SectionName).Bind(_options);
        _clientTrackers = new ConcurrentDictionary<string, ClientRequestTracker>();

        _cleanupTimer = new Timer(CleanupStaleEntries, null, CleanupInterval, CleanupInterval);
    }

    /// <summary>
    /// Processes the HTTP request and enforces rate limiting rules.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldRateLimit(context))
        {
            await _next(context);
            return;
        }

        var clientId = GetClientIdentifier(context);
        var tracker = _clientTrackers.GetOrAdd(clientId, _ => new ClientRequestTracker());
        var windowSize = TimeSpan.FromSeconds(_options.WindowSizeInSeconds);

        if (!tracker.TryRecordRequest(_options.MaxRequests, windowSize))
        {
            var retryAfter = tracker.GetRetryAfter(windowSize);
            _logger.LogWarning(
                "Rate limit exceeded for client {ClientId}. Retry after {RetryAfter} seconds.",
                clientId,
                retryAfter.TotalSeconds);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = Math.Ceiling(retryAfter.TotalSeconds).ToString();
            context.Response.ContentType = "application/json";

            var errorResponse = new
            {
                message = "Rate limit exceeded. Too many requests.",
                retryAfterSeconds = Math.Ceiling(retryAfter.TotalSeconds)
            };

            await context.Response.WriteAsJsonAsync(errorResponse);
            return;
        }

        //creating the response to the client with information about the current state- what the limit, and how much he can send in exact period of time (the window size in seconds).
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-RateLimit-Limit"] = _options.MaxRequests.ToString();
            context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, _options.MaxRequests - tracker.RequestCount).ToString();
            context.Response.Headers["X-RateLimit-Window"] = _options.WindowSizeInSeconds.ToString();
            return Task.CompletedTask;
        });

        await _next(context);
    }


    private bool ShouldRateLimit(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value ?? string.Empty;

        var isLimitedMethod = _options.LimitedMethods
            .Any(m => m.Equals(method, StringComparison.OrdinalIgnoreCase));

        if (!isLimitedMethod)
        {
            return false;
        }

        var isLimitedPath = _options.LimitedPaths
            .Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        return isLimitedPath;
    }

  
    private static string GetClientIdentifier(HttpContext context)
    {
        // find the original IP address that send the request if proxy or LB sit between
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ip = forwardedFor.Split(',').First().Trim();
            return ip;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private void CleanupStaleEntries(object? state)
    {
        var windowSize = TimeSpan.FromSeconds(_options.WindowSizeInSeconds);
        var staleThreshold = DateTime.UtcNow - windowSize - TimeSpan.FromMinutes(1);

        foreach (var kvp in _clientTrackers)
        {
            var lastRequest = kvp.Value.GetLastRequestTime();

            // If the last request was before the cutoff (now-2 minutes), OR the queue is empty (MinValue), remove it.
            if (lastRequest < staleThreshold)
            {
                _clientTrackers.TryRemove(kvp.Key, out _);
            }
        }
    }
}

/// <summary>
/// Extension methods for adding rate limiting middleware to the application pipeline.
/// </summary>
public static class RateLimitingMiddlewareExtensions
{
    /// <summary>
    /// Adds the rate limiting middleware to the application pipeline.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
