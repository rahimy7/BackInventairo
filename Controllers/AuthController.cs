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

        public AuthController(IAuthService authService)
        {
            _authService = authService;
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
    }
}