using Api.Common;
using Api.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WMS.Api.Controllers;

/// <summary>
/// Endpoints de usuarios: listado paginado con filtros, detalle, alta, baja lógica y actualización.
/// Requiere autenticación Bearer (login en <c>/api/Auth/login</c>).
/// Las respuestas de error (400/404/500) se generan en ErrorHandlingMiddleware como ProblemDetails.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Obtiene el listado paginado de usuarios, o el detalle de un usuario cuando se envía <c>id</c>.
    /// Filtros disponibles: <c>search</c> (nombre o correo), <c>isActive</c>, <c>page</c>, <c>pageSize</c>, <c>orderDesc</c>.
    /// </summary>
    /// <response code="200">Listado paginado o detalle del usuario.</response>
    /// <response code="400">Parámetros de paginación inválidos (ProblemDetails).</response>
    /// <response code="404">No existe ningún usuario con el <c>id</c> indicado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails, sin detalles internos).</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserListViewModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UserViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get([FromQuery] UserQuery query, CancellationToken cancellationToken)
    {
        if (query.Id.HasValue)
        {
            var user = await _userService.GetUserByIdAsync(query.Id.Value, cancellationToken);
            return Ok(user);
        }

        var pagedResult = await _userService.GetAllUsersAsync(query, cancellationToken);

        return Ok(pagedResult);
    }

    /// <summary>
    /// Crea un usuario. La contraseña se almacena como hash (nunca en texto plano) y no se devuelve en la respuesta.
    /// </summary>
    /// <response code="201">Usuario creado.</response>
    /// <response code="400">Datos inválidos o correo ya registrado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPost]
    [ProducesResponseType(typeof(UserViewModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Post(
        [FromBody] UserCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var user = await _userService.CreateUserAsync(dto, cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = user.Id }, user);
    }

    /// <summary>
    /// Actualiza por completo un usuario existente. La contraseña es opcional: hay que omitir el campo para
    /// conservar la actual (la cadena vacía produce un 400 por <c>MinLength(8)</c>).
    /// </summary>
    /// <response code="200">Usuario actualizado.</response>
    /// <response code="400">Datos inválidos o correo ya registrado (ProblemDetails).</response>
    /// <response code="404">Usuario no encontrado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(UserViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Put(
        int id,
        [FromBody] UserUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateUserAsync(id, dto, cancellationToken);

        return Ok(user);
    }

    /// <summary>
    /// Actualización parcial con la misma semántica que PUT (el modelo es idéntico).
    /// </summary>
    /// <response code="200">Usuario actualizado.</response>
    /// <response code="400">Datos inválidos o correo ya registrado (ProblemDetails).</response>
    /// <response code="404">Usuario no encontrado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(typeof(UserViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Patch(
        int id,
        [FromBody] UserUpdateDTO dto,
        CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateUserAsync(id, dto, cancellationToken);

        return Ok(user);
    }

    /// <summary>
    /// Realiza la baja lógica del usuario (SoftDelete); el registro permanece en la base de datos.
    /// </summary>
    /// <response code="204">Usuario eliminado lógicamente.</response>
    /// <response code="404">Usuario no encontrado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _userService.DeleteUserAsync(id, cancellationToken);

        if (!deleted)
        {
            throw new KeyNotFoundException($"User with ID {id} not found.");
        }

        return NoContent();
    }
}
