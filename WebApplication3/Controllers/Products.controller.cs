using Microsoft.AspNetCore.Mvc;
using ProductInventoryApi.DTOs;
using WebApplication3.Interfaces;

namespace ProductInventoryApi.Controllers;

/// <summary>
/// Manages product inventory operations including creation, retrieval, updates, and deletion.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductsRepository _productRepository;
    private readonly ILogger<ProductsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProductsController"/> class.
    /// </summary>
    /// <param name="productRepository">The repository for product data operations.</param>
    /// <param name="logger">The logger instance.</param>
    public ProductsController(IProductsRepository productRepository, ILogger<ProductsController> logger)
    {
        _productRepository = productRepository;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a list of all active products.
    /// </summary>
    /// <remarks>
    /// Use this endpoint to get all products that have not been soft-deleted. 
    /// You can optionally filter by category ID.
    /// </remarks>
    /// <param name="categoryId">Optional. The ID of the category to filter products by.</param>
    /// <returns>A list of active products.</returns>
    /// <response code="200">Returns the list of products</response>
    /// <response code="500">If there was an internal server error</response>
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

    /// <summary>
    /// Retrieves details of a specific product by its unique ID.
    /// </summary>
    /// <param name="id">The unique identifier of the product.</param>
    /// <returns>The product details if found.</returns>
    /// <response code="200">Returns the requested product</response>
    /// <response code="404">If the product does not exist</response>
    /// <response code="500">If there was an internal server error</response>
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

    /// <summary>
    /// Adds a new product to the inventory.
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// 
    ///     POST /api/Products
    ///     {
    ///        "name": "Gaming Laptop X1",
    ///        "sku": "GL-X1-2024",
    ///        "price": 4500.00,
    ///        "stockQuantity": 15,
    ///        "categoryId": 1
    ///     }
    /// 
    /// </remarks>
    /// <param name="request">The product creation object containing name, SKU, price, etc.</param>
    /// <returns>The newly created product.</returns>
    /// <response code="201">Returns the newly created product</response>
    /// <response code="400">If the input validation fails (e.g. negative price, missing SKU)</response>
    /// <response code="500">If there was an internal server error</response>
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

    /// <summary>
    /// Updates the stock quantity for an existing product.
    /// </summary>
    /// <param name="id">The unique identifier of the product to update.</param>
    /// <param name="request">The object containing the new stock quantity.</param>
    /// <returns>The updated product details.</returns>
    /// <response code="200">Returns the updated product</response>
    /// <response code="400">If validation fails (e.g. negative quantity)</response>
    /// <response code="404">If the product does not exist</response>
    /// <response code="500">If there was an internal server error</response>
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

    /// <summary>
    /// Performs a Soft Delete on a product.
    /// </summary>
    /// <remarks>
    /// The product is marked as inactive (IsActive = 0) and will no longer appear in the main list.
    /// It is not physically removed from the database.
    /// </remarks>
    /// <param name="id">The unique identifier of the product to delete.</param>
    /// <returns>No content.</returns>
    /// <response code="204">If the product was successfully deleted</response>
    /// <response code="404">If the product does not exist</response>
    /// <response code="500">If there was an internal server error</response>
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

