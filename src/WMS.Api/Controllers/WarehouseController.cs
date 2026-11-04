using Api.Common;
using Api.Warehouses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WMS.Api.Controllers;

/// <summary>
/// Endpoints de almacenes: listado paginado con filtros, detalle, alta, baja lógica y actualización.
/// Requiere autenticación Bearer (login en <c>/api/Auth/login</c>).
/// Las respuestas de error (400/404/500) se generan en ErrorHandlingMiddleware como ProblemDetails.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class WarehouseController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehouseController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    /// <summary>
    /// Obtiene el listado paginado de almacenes, o el detalle de un almacén cuando se envía <c>id</c>.
    /// Filtros disponibles: <c>search</c> (nombre o ubicación), <c>isActive</c>, <c>page</c>, <c>pageSize</c>, <c>orderDesc</c>.
    /// </summary>
    /// <response code="200">Listado paginado o detalle del almacén.</response>
    /// <response code="400">Parámetros de paginación inválidos (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="404">No existe ningún almacén con el <c>id</c> indicado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails, sin detalles internos).</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<WarehouseListViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(WarehouseViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get([FromQuery] WarehouseQuery query, CancellationToken cancellationToken)
    {
        if (query.Id.HasValue)
        {
            var warehouse = await _warehouseService.GetWarehouseByIdAsync(query.Id.Value, cancellationToken);
            return Ok(warehouse);
        }

        var pagedResult = await _warehouseService.GetAllWarehousesAsync(query, cancellationToken);

        return Ok(pagedResult);
    }

    /// <summary>
    /// Crea un almacén. El nombre debe ser único entre almacenes no dados de baja.
    /// </summary>
    /// <response code="201">Almacén creado.</response>
    /// <response code="400">Datos inválidos o nombre ya registrado (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPost]
    [ProducesResponseType(typeof(WarehouseViewModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Post(
        [FromBody] WarehouseCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseService.CreateWarehouseAsync(dto, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = warehouse.Id }, warehouse);
    }

    /// <summary>
    /// Actualiza por completo un almacén existente.
    /// </summary>
    /// <response code="200">Almacén actualizado.</response>
    /// <response code="400">Datos inválidos o nombre ya registrado (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="404">Almacén no encontrado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(WarehouseViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Put(
        int id,
        [FromBody] WarehouseUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseService.UpdateWarehouseAsync(id, dto, cancellationToken);

        return Ok(warehouse);
    }

    /// <summary>
    /// Actualización parcial con la misma semántica que PUT (el modelo es idéntico).
    /// </summary>
    /// <response code="200">Almacén actualizado.</response>
    /// <response code="400">Datos inválidos o nombre ya registrado (ProblemDetails).</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="404">Almacén no encontrado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(typeof(WarehouseViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Patch(
        int id,
        [FromBody] WarehouseUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseService.UpdateWarehouseAsync(id, dto, cancellationToken);

        return Ok(warehouse);
    }

    /// <summary>
    /// Realiza la baja lógica del almacén (SoftDelete); el registro permanece en la base de datos.
    /// </summary>
    /// <response code="204">Almacén eliminado lógicamente.</response>
    /// <response code="401">Sin autenticación (ProblemDetails).</response>
    /// <response code="404">Almacén no encontrado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _warehouseService.DeleteWarehouseAsync(id, cancellationToken);

        if (!deleted)
        {
            throw new KeyNotFoundException($"Warehouse with ID {id} not found.");
        }

        return NoContent();
    }
}
