using Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Products;

/// <summary>
/// Endpoints de productos: listado paginado con filtros, detalle, alta, baja lógica y actualización.
/// Requiere autenticación Bearer (login en <c>/api/Auth/login</c>).
/// Las respuestas de error (400/404/500) se generan en ErrorHandlingMiddleware como ProblemDetails.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProductController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductController(IProductService productService)
    {
        _productService = productService;
    }

    /// <summary>
    /// Obtiene el listado paginado de productos, o el detalle de un producto cuando se envía <c>id</c>.
    /// Filtros disponibles: <c>search</c> (nombre o SKU), <c>isActive</c>, <c>page</c>, <c>pageSize</c>, <c>orderDesc</c>.
    /// </summary>
    /// <response code="200">Listado paginado o detalle del producto.</response>
    /// <response code="400">Parámetros de paginación inválidos (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="404">No existe ningún producto con el <c>id</c> indicado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails, sin detalles internos).</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductListViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProductViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get([FromQuery] ProductQuery query, CancellationToken cancellationToken)
    {
        if (query.Id.HasValue)
        {
            var product = await _productService.GetProductByIdAsync(query.Id.Value, cancellationToken);
            return Ok(product);
        }

        var pagedResult = await _productService.GetAllProductsAsync(query, cancellationToken);

        return Ok(pagedResult);
    }

    /// <summary>
    /// Crea un producto. El SKU debe ser único entre productos no dados de baja.
    /// </summary>
    /// <response code="201">Producto creado.</response>
    /// <response code="400">Datos inválidos o SKU ya registrado (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPost]
    [ProducesResponseType(typeof(ProductViewModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Post(
        [FromBody] ProductCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var product = await _productService.CreateProductAsync(dto, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = product.Id }, product);
    }

    /// <summary>
    /// Actualiza por completo un producto existente.
    /// </summary>
    /// <response code="200">Producto actualizado.</response>
    /// <response code="400">Datos inválidos o SKU ya registrado (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="404">Producto no encontrado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProductViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Put(
        int id,
        [FromBody] ProductUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        var product = await _productService.UpdateProductAsync(id, dto, cancellationToken);

        return Ok(product);
    }

    /// <summary>
    /// Actualización parcial con la misma semántica que PUT (el modelo es idéntico).
    /// </summary>
    /// <response code="200">Producto actualizado.</response>
    /// <response code="400">Datos inválidos o SKU ya registrado (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="404">Producto no encontrado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(typeof(ProductViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Patch(
        int id,
        [FromBody] ProductUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        var product = await _productService.UpdateProductAsync(id, dto, cancellationToken);

        return Ok(product);
    }

    /// <summary>
    /// Realiza la baja lógica del producto (SoftDelete); el registro permanece en la base de datos.
    /// Su SKU vuelve a quedar disponible para otros productos.
    /// </summary>
    /// <response code="204">Producto eliminado lógicamente.</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="404">Producto no encontrado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _productService.DeleteProductAsync(id, cancellationToken);

        if (!deleted)
        {
            throw new KeyNotFoundException($"Product with ID {id} not found.");
        }

        return NoContent();
    }
}
