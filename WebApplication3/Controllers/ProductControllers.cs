using Microsoft.AspNetCore.Mvc;
using ProductInventoryApi.DTOs;
using ProductInventoryApi.Repositories;

namespace ProductInventoryApi.Controllers;

/// <summary>
/// API Controller for managing products in the inventory system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(IProductRepository productRepository, ILogger<ProductsController> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }


    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetAllProducts([FromQuery] int? categoryId = null)
    {
        try
        {
            _logger.LogInformation("Getting all active products. CategoryId filter: {CategoryId}", categoryId);

            var products = await _productRepository.GetAllActiveProductsAsync(categoryId);
            var response = products.Select(ProductResponse.FromProduct);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("An error occurred while retrieving products."));
        }
    }

  
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetProductById(int id)
    {
        try
        {
            _logger.LogInformation("Getting product by ID: {ProductId}", id);

            var product = await _productRepository.GetProductByIdAsync(id);

            if (product == null)
            {
                _logger.LogWarning("Product not found. ID: {ProductId}", id);
                return NotFound(new ErrorResponse($"Product with ID {id} not found."));
            }

            return Ok(ProductResponse.FromProduct(product));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving product ID: {ProductId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("An error occurred while retrieving the product."));
        }
    }

 
    [HttpPost]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductResponse>> CreateProduct([FromBody] CreateProductRequest request)
    {
        try
        {
            _logger.LogInformation("Creating new product. SKU: {SKU}", request.SKU);

            // Model validation is automatic with [ApiController] attribute
            // Additional business validation
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new ErrorResponse("Validation failed.", errors));
            }

            var (product, errorMessage) = await _productRepository.CreateProductAsync(
                request.Name,
                request.SKU,
                request.Price,
                request.StockQuantity,
                request.CategoryId);

            if (product == null)
            {
                _logger.LogWarning("Failed to create product. Error: {Error}", errorMessage);
                return BadRequest(new ErrorResponse(errorMessage ?? "Failed to create product."));
            }

            _logger.LogInformation("Product created successfully. ProductId: {ProductId}", product.ProductId);

            var response = ProductResponse.FromProduct(product);
            return CreatedAtAction(
                nameof(GetProductById),
                new { id = product.ProductId },
                response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("An error occurred while creating the product."));
        }
    }


    [HttpPut("{id:int}/stock")]
    [ProducesResponseType(typeof(ProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> UpdateStock(int id, [FromBody] UpdateStockRequest request)
    {
        try
        {
            _logger.LogInformation("Updating stock for product ID: {ProductId}. New quantity: {Quantity}",
                id, request.Quantity);

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new ErrorResponse("Validation failed.", errors));
            }

            // Check if product exists first
            var existingProduct = await _productRepository.GetProductByIdAsync(id);
            if (existingProduct == null)
            {
                _logger.LogWarning("Product not found for stock update. ID: {ProductId}", id);
                return NotFound(new ErrorResponse($"Product with ID {id} not found."));
            }

            var updatedProduct = await _productRepository.UpdateStockAsync(id, request.Quantity);

            if (updatedProduct == null)
            {
                return NotFound(new ErrorResponse($"Product with ID {id} not found."));
            }

            _logger.LogInformation("Stock updated successfully for product ID: {ProductId}", id);
            return Ok(ProductResponse.FromProduct(updatedProduct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating stock for product ID: {ProductId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("An error occurred while updating stock."));
        }
    }


    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        try
        {
            _logger.LogInformation("Soft deleting product ID: {ProductId}", id);

            var deleted = await _productRepository.SoftDeleteAsync(id);

            if (!deleted)
            {
                _logger.LogWarning("Product not found for deletion. ID: {ProductId}", id);
                return NotFound(new ErrorResponse($"Product with ID {id} not found."));
            }

            _logger.LogInformation("Product soft deleted successfully. ID: {ProductId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product ID: {ProductId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ErrorResponse("An error occurred while deleting the product."));
        }
    }
}

