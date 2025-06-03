using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using InventarioAPI.Models;
using InventarioAPI.Services;
using System.Security.Claims;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
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
        /// Endpoint para autenticación de usuarios
        /// </summary>
        /// <param name="request">Datos de login del usuario</param>
        /// <returns>Respuesta con token JWT si es exitoso</returns>
        [HttpPost("login")]
        [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
        {
            try
            {
                // Validar modelo
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Datos inválidos",
                        Errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                // Procesar login
                var result = await _authService.LoginAsync(request);

                if (!result.Success)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = result.Message
                    });
                }

                return Ok(new ApiResponse<LoginResponse>
                {
                    Success = true,
                    Message = "Login exitoso",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Endpoint para verificar si el token es válido
        /// </summary>
        /// <returns>Información del usuario autenticado</returns>
        [HttpGet("verify")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
        public ActionResult<ApiResponse<object>> VerifyToken()
        {
            try
            {
                var userClaims = new
                {
                    Id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                    Usuario = User.FindFirst(ClaimTypes.Name)?.Value,
                    Nombre = User.FindFirst("nombre")?.Value,
                    Perfil = User.FindFirst("perfil")?.Value,
                    Tienda = User.FindFirst("tienda")?.Value,
                    Area = User.FindFirst("area")?.Value
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Token válido",
                    Data = userClaims
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error al verificar token",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Obtener todos los usuarios con filtros
        /// </summary>
        /// <param name="searchTerm">Término de búsqueda</param>
        /// <param name="perfil">Filtrar por perfil</param>
        /// <param name="tienda">Filtrar por tienda</param>
        /// <param name="activo">Filtrar por estado activo</param>
        /// <param name="pageNumber">Número de página</param>
        /// <param name="pageSize">Tamaño de página</param>
        /// <returns>Lista de usuarios</returns>
        [HttpGet("users")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<List<UserResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<UserResponse>>>> GetUsers(
            [FromQuery] string searchTerm = "",
            [FromQuery] UserProfile? perfil = null,
            [FromQuery] string tienda = "",
            [FromQuery] bool? activo = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // Verificar permisos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                var request = new GetUsersRequest
                {
                    SearchTerm = searchTerm,
                    Perfil = perfil,
                    Tienda = tienda,
                    Activo = activo,
                    PageNumber = pageNumber,
                    PageSize = Math.Min(pageSize, 100)
                };

                var result = await _userService.GetUsersAsync(request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Crear nuevo usuario
        /// </summary>
        /// <param name="request">Datos del nuevo usuario</param>
        /// <returns>Usuario creado</returns>
        [HttpPost("users")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<UserResponse>), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserResponse>>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                // Verificar permisos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Datos inválidos",
                        Errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                var currentUserId = GetCurrentUserId();
                var result = await _userService.CreateUserAsync(request, currentUserId);

                if (result.Success)
                {
                    return CreatedAtAction(nameof(GetUsers), result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Actualizar usuario existente
        /// </summary>
        /// <param name="userId">ID del usuario a actualizar</param>
        /// <param name="request">Datos a actualizar</param>
        /// <returns>Usuario actualizado</returns>
        [HttpPut("users/{userId}")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<UserResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<UserResponse>>> UpdateUser(int userId, [FromBody] UpdateUserRequest request)
        {
            try
            {
                // Verificar permisos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Datos inválidos",
                        Errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                var currentUserId = GetCurrentUserId();
                var result = await _userService.UpdateUserAsync(userId, request, currentUserId);

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Eliminar usuario
        /// </summary>
        /// <param name="userId">ID del usuario a eliminar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpDelete("users/{userId}")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUser(int userId)
        {
            try
            {
                // Verificar permisos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                var currentUserId = GetCurrentUserId();
                var result = await _userService.DeleteUserAsync(userId, currentUserId);

                if (result.Success)
                {
                    return Ok(result);
                }

                return BadRequest(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Obtener actividad de un usuario
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <returns>Lista de actividades</returns>
        [HttpGet("users/{userId}/activity")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<List<UserActivity>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<UserActivity>>>> GetUserActivity(int userId)
        {
            try
            {
                // Verificar permisos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                var result = await _userService.GetUserActivityAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Obtener usuarios filtrados por rol
        /// </summary>
        /// <param name="perfil">Filtrar por perfil</param>
        /// <param name="tienda">Filtrar por tienda</param>
        /// <param name="activo">Filtrar por estado activo</param>
        /// <returns>Lista de usuarios filtrados</returns>
        [HttpGet("users-by-role")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<List<UserResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<UserResponse>>>> GetUsersByRole(
            [FromQuery] UserProfile? perfil = null,
            [FromQuery] string tienda = "",
            [FromQuery] bool activo = true)
        {
            try
            {
                // Verificar permisos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                var currentUserTienda = User.FindFirst("tienda")?.Value;

                if (currentUserProfile != "ADMINISTRADOR" && currentUserProfile != "GERENTE_TIENDA")
                {
                    return Forbid();
                }

                // Si es gerente de tienda, solo puede ver usuarios de su tienda
                if (currentUserProfile == "GERENTE_TIENDA")
                {
                    tienda = currentUserTienda ?? "";
                }

                var result = await _userService.GetUsersByRoleAsync(perfil, tienda);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Verificar permisos de usuario para una acción específica
        /// </summary>
        /// <param name="request">Datos de verificación de permisos</param>
        /// <returns>Resultado de la verificación</returns>
        [HttpPost("check-permissions")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<PermissionCheckResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PermissionCheckResponse>>> CheckPermissions([FromBody] CheckPermissionsRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Datos inválidos",
                        Errors = ModelState.Values
                            .SelectMany(v => v.Errors)
                            .Select(e => e.ErrorMessage)
                            .ToList()
                    });
                }

                var currentUserId = GetCurrentUserId();
                var result = await _userService.CheckPermissionsAsync(currentUserId, request);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Obtener estadísticas del sistema
        /// </summary>
        /// <returns>Estadísticas generales del sistema</returns>
        [HttpGet("system-stats")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<SystemStatsResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<SystemStatsResponse>>> GetSystemStats()
        {
            try
            {
                // Verificar permisos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    return Forbid();
                }

                var result = await _userService.GetSystemStatsAsync();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error interno del servidor",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        /// <summary>
        /// Obtener información del usuario actual
        /// </summary>
        /// <returns>Información del usuario autenticado</returns>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public ActionResult<ApiResponse<object>> GetCurrentUser()
        {
            try
            {
                var userInfo = new
                {
                    Id = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0"),
                    Usuario = User.FindFirst(ClaimTypes.Name)?.Value,
                    Nombre = User.FindFirst("nombre")?.Value,
                    Perfil = User.FindFirst("perfil")?.Value,
                    Tienda = User.FindFirst("tienda")?.Value,
                    Area = User.FindFirst("area")?.Value
                };

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Información del usuario obtenida exitosamente",
                    Data = userInfo
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error al obtener información del usuario",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        // Método auxiliar para obtener el ID del usuario actual
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            throw new UnauthorizedAccessException("No se pudo obtener el ID del usuario autenticado");
        }
    }
}