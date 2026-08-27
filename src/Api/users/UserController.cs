using Microsoft.AspNetCore.Mvc;

namespace Api.Users;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserService userService,
        ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    // GET: api/User
    // GET: api/User/{id}
    [HttpGet]
    public async Task<IActionResult> Get(
        int? id = null)
    {
        try
        {
            if (id.HasValue)
            {
                var user = await _userService.GetUserByIdAsync(id.Value);

                return Ok(user);
            }

            var users = await _userService.GetAllUsersAsync();

            return Ok(users);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "User not found.");

            return NotFound(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while getting users.");

            return StatusCode(500, new
            {
                message = "An internal error occurred."
            });
        }
    }

    // POST: api/User
    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] UserCreateDTO dto)
    {
        try
        {
            var user = await _userService.CreateUserAsync(dto);

            return CreatedAtAction(
                nameof(Get),
                new { id = user.Id },
                user);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid user data.");

            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while creating user.");

            return StatusCode(500, new
            {
                message = "An internal error occurred."
            });
        }
    }

    // PUT: api/User/{id}
    [HttpPut]
    public async Task<IActionResult> Put(
        int id,
        [FromBody] UserUpdateDTO dto)
    {
        try
        {
            var user = await _userService.UpdateUserAsync(id, dto);

            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "User not found.");

            return NotFound(new
            {
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid user data.");

            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while updating user {UserId}.",
                id);

            return StatusCode(500, new
            {
                message = "An internal error occurred."
            });
        }
    }

    // PATCH: api/User/{id}
    [HttpPatch]
    public async Task<IActionResult> Patch(
        int id,
        [FromBody] UserUpdateDTO dto)
    {
        try
        {
            var user = await _userService.UpdateUserAsync(id, dto);

            return Ok(user);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "User not found.");

            return NotFound(new
            {
                message = ex.Message
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid user data.");

            return BadRequest(new
            {
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while patching user {UserId}.",
                id);

            return StatusCode(500, new
            {
                message = "An internal error occurred."
            });
        }
    }

    // DELETE: api/User/{id}
    [HttpDelete]
    public async Task<IActionResult> Delete(
        int id)
    {
        try
        {
            var deleted = await _userService.DeleteUserAsync(id);

            if (!deleted)
            {
                return NotFound(new
                {
                    message = $"User with ID {id} not found."
                });
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while deleting user {UserId}.",
                id);

            return StatusCode(500, new
            {
                message = "An internal error occurred."
            });
        }
    }
}