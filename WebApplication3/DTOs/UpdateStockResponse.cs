using System.ComponentModel.DataAnnotations;

namespace ProductInventoryApi.DTOs;

/// <summary>
/// Represents the request data transfer object for updating product stock quantity.
/// </summary>
public class UpdateStockRequest
{
    /// <summary>
    /// The new quantity in stock
    /// </summary>
    /// <example>100</example>
    [Required(ErrorMessage = "Quantity is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be a non-negative value.")]
    public int Quantity { get; set; }
}
