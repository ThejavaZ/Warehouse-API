using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Application.Common;
using WMS.Application.Inventory;

namespace WMS.Api.Controllers;

/// <summary>
/// Endpoints de inventario: consulta de existencias y movimientos de entrada/salida.
/// Los movimientos se hacen con <c>restock</c> y <c>consume</c> (nunca editando la cantidad
/// a mano desde un PUT genérico, decisión del Sprint 14).
/// Requiere autenticación Bearer (login en <c>/api/Auth/login</c>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class InventoryItemController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryItemController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Listado paginado de existencias. Filtros: <c>warehouseId</c>, <c>productId</c>,
    /// <c>page</c>, <c>pageSize</c>, <c>orderDesc</c>. Cada ítem incluye el nombre del almacén
    /// y el SKU/nombre del producto.
    /// </summary>
    /// <response code="200">Listado paginado de existencias.</response>
    /// <response code="400">Parámetros de paginación inválidos (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<InventoryItemViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get([FromQuery] InventoryQuery query, CancellationToken cancellationToken)
    {
        var pagedResult = await _inventoryService.GetAllAsync(query, cancellationToken);

        return Ok(pagedResult);
    }

    /// <summary>
    /// Entrada de inventario: suma <c>quantity</c> al par almacén–producto.
    /// Si la ficha no existe, se crea con esa cantidad.
    /// </summary>
    /// <response code="200">Existencias actualizadas.</response>
    /// <response code="400">Cantidad inválida, almacén o producto inactivos (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="404">Almacén o producto inexistentes o dados de baja (ProblemDetails).</response>
    /// <response code="409">Conflicto concurrente: reintenta la operación (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPost("restock")]
    [ProducesResponseType(typeof(InventoryItemViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Restock(
        [FromBody] InventoryMovementDTO dto,
        CancellationToken cancellationToken)
    {
        var item = await _inventoryService.RestockAsync(dto, cancellationToken);

        return Ok(item);
    }

    /// <summary>
    /// Salida de inventario: resta <c>quantity</c> al par almacén–producto.
    /// Falla con <c>409</c> si no hay saldo suficiente (la cantidad nunca queda negativa).
    /// </summary>
    /// <response code="200">Existencias actualizadas.</response>
    /// <response code="400">Cantidad inválida, almacén o producto inactivos (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="404">Almacén/producto inexistentes o sin ficha de existencias (ProblemDetails).</response>
    /// <response code="409">Saldo insuficiente (ProblemDetails, código insufficient_stock).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPost("consume")]
    [ProducesResponseType(typeof(InventoryItemViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Consume(
        [FromBody] InventoryMovementDTO dto,
        CancellationToken cancellationToken)
    {
        var item = await _inventoryService.ConsumeAsync(dto, cancellationToken);

        return Ok(item);
    }
}
