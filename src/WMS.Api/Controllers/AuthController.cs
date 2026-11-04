using System.Security.Claims;
using Api.Auth;
using Api.Common;
using Api.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WMS.Api.Controllers;

/// <summary>
/// Endpoints de autenticación: registro (opcional), login, renovación y revocación de tokens.
/// <c>/register</c>, <c>/login</c>, <c>/refresh</c> y <c>/revoke</c> son públicos;
/// <c>/me</c> requiere un access token Bearer válido.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;

    public AuthController(IAuthService authService, IUserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    /// <summary>
    /// Registra un usuario con las mismas reglas que <c>POST /api/User</c> (no devuelve tokens:
    /// para obtenerlos hay que llamar a <c>/login</c>). Se puede desactivar con <c>Auth:RegistrationEnabled</c>.
    /// </summary>
    /// <response code="201">Usuario creado.</response>
    /// <response code="400">Datos inválidos o correo ya registrado (ProblemDetails).</response>
    /// <response code="403">Registro abierto desactivado en la configuración (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserViewModel), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register(
        [FromBody] UserCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var user = await _authService.RegisterAsync(dto, cancellationToken);

        return Created($"/api/User/{user.Id}", user);
    }

    /// <summary>
    /// Autentica un usuario y devuelve un access token corto más un refresh token rotatorio.
    /// El correo debe pertenecer a un usuario activo y no dado de baja.
    /// </summary>
    /// <response code="200">Credenciales válidas: se devuelven los tokens.</response>
    /// <response code="400">Cuerpo inválido (ProblemDetails).</response>
    /// <response code="401">Correo o contraseña incorrectos (ProblemDetails).</response>
    /// <response code="429">Demasiadas solicitudes desde el mismo origen (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Login(
        [FromBody] LoginDTO dto,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.LoginAsync(dto, cancellationToken);

        return Ok(auth);
    }

    /// <summary>
    /// Canjea un refresh token vigente por uno nuevo (rotación) junto con un access token recién firmado.
    /// El refresh token usado queda revocado; reutilizarlo devuelve 401.
    /// </summary>
    /// <response code="200">Tokens renovados.</response>
    /// <response code="400">Cuerpo inválido (ProblemDetails).</response>
    /// <response code="401">Refresh token inválido, caducado o ya usado (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenDTO dto,
        CancellationToken cancellationToken)
    {
        var auth = await _authService.RefreshAsync(dto, cancellationToken);

        return Ok(auth);
    }

    /// <summary>
    /// Revoca el refresh token indicado (logout). Siempre responde 204, tanto si existía como si no,
    /// para no permitir adivinar tokens válidos.
    /// </summary>
    /// <response code="204">Token revocado (o inexistente; la respuesta es idéntica a propósito).</response>
    /// <response code="400">Cuerpo inválido (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [AllowAnonymous]
    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Revoke(
        [FromBody] RefreshTokenDTO dto,
        CancellationToken cancellationToken)
    {
        await _authService.RevokeAsync(dto, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Devuelve el perfil del usuario autenticado (a partir del claim del access token).
    /// </summary>
    /// <response code="200">Perfil del usuario.</response>
    /// <response code="401">Sin token o token inválido (ProblemDetails).</response>
    /// <response code="404">El usuario del token ya no existe o está dado de baja (ProblemDetails).</response>
    /// <response code="500">Error inesperado (ProblemDetails).</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserViewModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!int.TryParse(userIdClaim, out var userId))
        {
            throw new DomainException("invalid_token", "The access token is invalid.", 401);
        }

        var user = await _userService.GetUserByIdAsync(userId, cancellationToken);

        return Ok(user);
    }
}
