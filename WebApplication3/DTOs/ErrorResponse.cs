namespace ProductInventoryApi.DTOs;


public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public IEnumerable<string>? Errors { get; set; }

    public ErrorResponse(string message)
    {
        Message = message;
    }

    public ErrorResponse(string message, IEnumerable<string> errors)
    {
        Message = message;
        Errors = errors;
    }
}

