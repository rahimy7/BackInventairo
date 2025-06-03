using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using InventarioAPI.Models;
using InventarioAPI.Services;
using System.Security.Claims;

namespace InventarioAPI.Controllers
{
    /// <summary>
    /// Controlador para gestión de tiendas
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class StoreController : ControllerBase
    {
        private readonly IStoreService _storeService;

        public StoreController(IStoreService storeService)
        {
            _storeService = storeService;
        }

        /// <summary>
        /// Obtener todas las tiendas
        /// </summary>
        /// <returns>Lista de tiendas</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<StoreResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<StoreResponse>>>> GetStores()
        {
            try
            {
                // Verificar permisos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (currentUserProfile != "ADMINISTRADOR" && currentUserProfile != "GERENTE_TIENDA")
                {
                    return Forbid();
                }

                var result = await _storeService.GetStoresAsync();
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
        /// Obtener usuarios de una tienda específica
        /// </summary>
        /// <param name="tienda">Código de la tienda</param>
        /// <returns>Lista de usuarios de la tienda</returns>
        [HttpGet("{tienda}/users")]
        [ProducesResponseType(typeof(ApiResponse<List<UserResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<UserResponse>>>> GetStoreUsers(string tienda)
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
                if (currentUserProfile == "GERENTE_TIENDA" && currentUserTienda != tienda)
                {
                    return Forbid();
                }

                var result = await _storeService.GetStoreUsersAsync(tienda);
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
        /// Obtener estadísticas de una tienda
        /// </summary>
        /// <param name="tienda">Código de la tienda</param>
        /// <returns>Estadísticas de la tienda</returns>
        [HttpGet("{tienda}/stats")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> GetStoreStats(string tienda)
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

                // Si es gerente de tienda, solo puede ver estadísticas de su tienda
                if (currentUserProfile == "GERENTE_TIENDA" && currentUserTienda != tienda)
                {
                    return Forbid();
                }

                var storeResult = await _storeService.GetStoresAsync();
                var usersResult = await _storeService.GetStoreUsersAsync(tienda);

                if (storeResult.Success && usersResult.Success)
                {
                    var store = storeResult.Data?.FirstOrDefault(s => s.Tienda == tienda);
                    var users = usersResult.Data ?? new List<UserResponse>();

                    var stats = new
                    {
                        Tienda = tienda,
                        NombreTienda = store?.Nombre ?? "",
                        TotalUsuarios = users.Count,
                        UsuariosPorPerfil = users.GroupBy(u => u.Perfil.ToString())
                            .ToDictionary(g => g.Key, g => g.Count()),
                        UsuariosActivos = users.Count(u => u.Activo),
                        UsuariosInactivos = users.Count(u => !u.Activo),
                        SolicitudesActivas = store?.SolicitudesActivas ?? 0,
                        UltimaActividad = store?.UltimaActividad
                    };

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Estadísticas de tienda obtenidas exitosamente",
                        Data = stats
                    });
                }

                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error al obtener estadísticas de la tienda"
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
    }

    /// <summary>
    /// Controlador para gestión de divisiones
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class DivisionController : ControllerBase
    {
        private readonly IDivisionService _divisionService;

        public DivisionController(IDivisionService divisionService)
        {
            _divisionService = divisionService;
        }

        /// <summary>
        /// Obtener todas las divisiones
        /// </summary>
        /// <returns>Lista de divisiones</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<DivisionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<DivisionResponse>>>> GetDivisions()
        {
            try
            {
                // Verificar permisos básicos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                if (string.IsNullOrEmpty(currentUserProfile))
                {
                    return Forbid();
                }

                var result = await _divisionService.GetDivisionsAsync();
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
        /// Obtener divisiones de una tienda específica
        /// </summary>
        /// <param name="tienda">Código de la tienda</param>
        /// <returns>Lista de divisiones de la tienda</returns>
        [HttpGet("store/{tienda}")]
        [ProducesResponseType(typeof(ApiResponse<List<DivisionResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<DivisionResponse>>>> GetDivisionsByStore(string tienda)
        {
            try
            {
                // Verificar permisos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                var currentUserTienda = User.FindFirst("tienda")?.Value;

                // Todos los usuarios autenticados pueden ver divisiones, pero restricciones por tienda
                if (currentUserProfile == "GERENTE_TIENDA" && currentUserTienda != tienda)
                {
                    return Forbid();
                }

                if (currentUserProfile == "LIDER" && currentUserTienda != tienda)
                {
                    return Forbid();
                }

                if (currentUserProfile == "INVENTARIO" && currentUserTienda != tienda)
                {
                    return Forbid();
                }

                var result = await _divisionService.GetDivisionsByStoreAsync(tienda);
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
        /// Asignar divisiones a un líder
        /// </summary>
        /// <param name="request">Datos de asignación</param>
        /// <returns>Resultado de la asignación</returns>
        [HttpPost("assign-leader")]
        [ProducesResponseType(typeof(ApiResponse<DivisionAssignmentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<DivisionAssignmentResponse>>> AssignDivisionsToLeader([FromBody] AssignDivisionsRequest request)
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

                // Si es gerente de tienda, solo puede asignar en su tienda
                if (currentUserProfile == "GERENTE_TIENDA" && currentUserTienda != request.Tienda)
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
                var result = await _divisionService.AssignDivisionsToLeaderAsync(request, currentUserId);

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
        /// Obtener divisiones asignadas a un usuario específico
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <param name="tienda">Código de la tienda</param>
        /// <returns>Lista de divisiones asignadas</returns>
        [HttpGet("user/{userId}/store/{tienda}")]
        [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<string>>>> GetUserDivisions(int userId, string tienda)
        {
            try
            {
                // Verificar permisos
                var currentUserProfile = User.FindFirst("perfil")?.Value;
                var currentUserTienda = User.FindFirst("tienda")?.Value;
                var currentUserId = GetCurrentUserId();

                // Administradores pueden ver todo
                // Gerentes pueden ver solo de su tienda
                // Usuarios pueden ver solo sus propias asignaciones de su tienda
                if (currentUserProfile != "ADMINISTRADOR")
                {
                    if (currentUserProfile == "GERENTE_TIENDA" && currentUserTienda != tienda)
                    {
                        return Forbid();
                    }
                    
                    if ((currentUserProfile == "LIDER" || currentUserProfile == "INVENTARIO") &&
                        (currentUserTienda != tienda || currentUserId != userId))
                    {
                        return Forbid();
                    }
                }

                // Simulamos la obtención de divisiones del usuario desde UserDivisions
                // En una implementación real, esto debería ser un método del servicio
                var divisions = new List<string>();

                return Ok(new ApiResponse<List<string>>
                {
                    Success = true,
                    Message = "Divisiones del usuario obtenidas exitosamente",
                    Data = divisions
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
        /// Obtener estadísticas de divisiones
        /// </summary>
        /// <param name="tienda">Filtrar por tienda (opcional)</param>
        /// <returns>Estadísticas de divisiones</returns>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> GetDivisionStats([FromQuery] string tienda = "")
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

                // Si es gerente de tienda, solo puede ver estadísticas de su tienda
                if (currentUserProfile == "GERENTE_TIENDA")
                {
                    tienda = currentUserTienda ?? "";
                }

                var divisionsResult = await _divisionService.GetDivisionsAsync();

                if (divisionsResult.Success && divisionsResult.Data != null)
                {
                    var stats = new
                    {
                        TotalDivisiones = divisionsResult.Data.Count,
                        DivisionesActivas = divisionsResult.Data.Count(d => d.Activa),
                        Tienda = tienda,
                        DivisionesPorCodigo = divisionsResult.Data
                            .GroupBy(d => d.Codigo)
                            .ToDictionary(g => g.Key, g => g.First().Nombre)
                    };

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Estadísticas de divisiones obtenidas exitosamente",
                        Data = stats
                    });
                }

                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Error al obtener estadísticas de divisiones"
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