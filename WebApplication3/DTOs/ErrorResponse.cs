namespace ProductInventoryApi.DTOs;

/// <summary>
/// Represents a standardized error response returned by the API.
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Gets or sets the main error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a collection of detailed error messages, typically from validation failures.
    /// </summary>
    public IEnumerable<string>? Errors { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorResponse"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ErrorResponse(string message)
    {
        Message = message;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorResponse"/> class with a message and detailed errors.
    /// </summary>
    /// <param name="message">The main error message.</param>
    /// <param name="errors">A collection of detailed error messages.</param>
    public ErrorResponse(string message, IEnumerable<string> errors)
    {
        Message = message;
        Errors = errors;
    }
}
