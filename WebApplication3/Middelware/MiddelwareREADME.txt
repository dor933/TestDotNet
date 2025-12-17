Observing the Rate Limiting MiddlewareThis guide explains how to verify that the RateLimitingMiddleware is functioning correctly. Based on the default configuration in the code, the limiter applies only to specific criteria.

1. Default Configuration RulesBefore testing, note the hardcoded defaults in RateLimitingOptions:Target Path: /api/products (and sub-paths).Target Methods: POST, PUT, DELETE (Note: GET requests are not limited).The Limit: 10 requests per 60 seconds.

2. SetupEnsure the middleware is registered in your Program.cs before your controllers:C#var app = builder.Build();

// ... other middleware
app.UseRateLimiting(); // <-- Register here
app.MapControllers();

app.Run();

3. How to TestOption A: Using PowerShell (Recommended)You can fire a burst of requests to trigger the limit immediately. Run this command in PowerShell:PowerShell# Send 12 POST requests (Limit is 10)
1..12 | ForEach-Object {
    $response = Invoke-WebRequest -Uri "http://localhost:5272/api/products" -Method POST -ErrorAction SilentlyContinue
    
    Write-Host "Request $_ : Status $($response.StatusCode)"
    
    # Print the specific Rate Limit headers
    if ($response.Headers["X-RateLimit-Remaining"]) {
        Write-Host "   Remaining: $($response.Headers["X-RateLimit-Remaining"])"
    }
    if ($response.Headers["Retry-After"]) {
        Write-Host "   BLOCKED! Retry After: $($response.Headers["Retry-After"]) seconds"
    }
}
Option B: Using cURL or Postman  -  Make a POST request to http://localhost:your-port/api/products.Requests 1 through 10:Inspect the Response Headers.Look for X-RateLimit-Remaining. It will decrease with every click ($9, 8, 7...$).Request 11:Status Code: 429 Too Many Requests.Response Body:JSON{
    "message": "Rate limit exceeded. Too many requests.",
    "retryAfterSeconds": 60
}
Response Header: Retry-After: 60.

4. What to ObserveThe "Happy Path" (Under Limit)For the first 10 requests, the middleware injects these informational headers:X-RateLimit-Limit: 10 (Total allowed)X-RateLimit-Remaining: 9 (Decrements with usage)X-RateLimit-Window: 60 (Window size in seconds)The "Blocked Path" (Over Limit)Once the 11th request hits within the 60-second window:The request does not reach the Controller.The middleware immediately returns 429.The Retry-After header tells the client exactly how many seconds to wait before the oldest request in the queue expires (freeing up a slot).

5. Troubleshooting 

"I keep getting 200 OK after 10 requests": check that you are sending a POST, PUT, or DELETE. The default configuration ignores GET requests. 
"Headers are missing": Ensure app.UseRateLimiting() is placed before app.MapControllers() in your startup sequence.