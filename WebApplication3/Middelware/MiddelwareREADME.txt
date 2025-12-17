Observing the Rate Limiting MiddlewareThis guide explains how to verify that the RateLimitingMiddleware is functioning correctly.
Based on the default configuration in the code, the limiter applies only to specific criteria.

1. Default Configuration RulesBefore testing, note the hardcoded defaults in RateLimitingOptions:Target Path: /api/products (and sub-paths).Target Methods: POST, PUT, DELETE (Note: GET requests are not limited).The Limit: 10 requests per 60 seconds.

2. Setup

Ensure the middleware is registered in your Program.cs before your controllers:
var app = builder.Build();
// ... other middleware
app.UseRateLimiting(); // <-- Register here
app.MapControllers();

app.Run();

3. How to Test

Option A: Using PowerShell - You can fire a burst of requests to trigger the limit immediately. For Example, Run this command in PowerShell Which Send 12 POST requests (Limit is 10):

1..12 | ForEach-Object {
    $url = "http://localhost:5272/api/products/1/stock"
    
    $jsonBody = '{ "quantity": 3 }'

    try {
        $response = Invoke-WebRequest -Uri $url -Method PUT -Body $jsonBody -ContentType "application/json"
        
        Write-Host "Request $_ : Status $($response.StatusCode) - Success" -ForegroundColor Green
    }
    catch {
        $response = $_.Exception.Response

        if ($null -ne $response) {
            $status = [int]$response.StatusCode
            
            if ($status -eq 429) {
                Write-Host "Request $_ : Status $status - BLOCKED (Rate Limit)" -ForegroundColor Red
                
                if ($response.Headers["Retry-After"]) {
                    Write-Host "    -> Wait for $($response.Headers["Retry-After"]) seconds" -ForegroundColor Yellow
                }
            }
            else {
                Write-Host "Request $_ : Status $status - Error: $($_.Exception.Message)" -ForegroundColor DarkRed
            }
        }
        else {
            Write-Host "Request $_ : Connection Failed ($($_.Exception.Message))" -ForegroundColor Red
        }
    }
}


Option B: Using Swagger UI or Postman  -  Send 10 requests with one of the limited methods (PUT/POST/DELETE , you can use the example from the powershell script). Inspect the Response Headers.Look for X-RateLimit-Remaining. It will decrease with every click ($9, 8, 7...$).Request 11:Status Code: 429 Too Many Requests.Response Body:JSON{
    "message": "Rate limit exceeded. Too many requests.",
    "retryAfterSeconds": 60
}
Response Header: Retry-After: 60.

4. What to Observe:
The middleware injects these informational headers:
X-RateLimit-Limit: 10 (Total allowed)
X-RateLimit-Remaining: 9 (Decrements with usage)
X-RateLimit-Window: 60 (Window size in seconds)

5. Troubleshooting 

"I keep getting 200 OK after 10 requests": check that you are sending a POST, PUT, or DELETE. The default configuration ignores GET requests. 
"Headers are missing": Ensure app.UseRateLimiting() is placed before app.MapControllers() in your startup sequence.