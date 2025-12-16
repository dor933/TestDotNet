using System.ComponentModel.DataAnnotations;

namespace ProductInventoryApi.DTOs;


public class UpdateStockRequest
{
    [Required(ErrorMessage = "Quantity is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Quantity must be a non-negative value.")]
    public int Quantity { get; set; }
}

