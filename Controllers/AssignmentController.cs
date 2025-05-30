using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using InventarioAPI.Models;
using InventarioAPI.Services;
using System.Security.Claims;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class AssignmentController : ControllerBase
    {
        private readonly IAssignmentService _assignmentService;

        public AssignmentController(IAssignmentService assignmentService)
        {
            _assignmentService = assignmentService;
        }

        /// <summary>
        /// Crear una nueva asignación de división/categoría/grupo a un usuario
        /// </summary>
        /// <param name="request">Datos de la asignación</param>
        /// <returns>Asignación creada</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<AssignmentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AssignmentResponse>>> CreateAssignment([FromBody] CreateAssignmentRequest request)
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

                // Validar que se haya proporcionado información según el tipo de asignación
                var validationError = ValidateAssignmentRequest(request);
                if (!string.IsNullOrEmpty(validationError))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = validationError
                    });
                }

                var currentUserId = GetCurrentUserId();
                var result = await _assignmentService.CreateAssignmentAsync(request, currentUserId);

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
        /// Obtener asignaciones con filtros opcionales
        /// </summary>
        /// <param name="userId">ID del usuario (opcional)</param>
        /// <param name="tienda">Tienda (opcional)</param>
        /// <param name="assignmentType">Tipo de asignación (opcional)</param>
        /// <param name="isActive">Estado activo (opcional)</param>
        /// <returns>Lista de asignaciones</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<AssignmentResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<AssignmentResponse>>>> GetAssignments(
            [FromQuery] int? userId = null,
            [FromQuery] string tienda = null,
            [FromQuery] AssignmentType? assignmentType = null,
            [FromQuery] bool? isActive = true)
        {
            try
            {
                var request = new GetAssignmentsRequest
                {
                    UserID = userId,
                    Tienda = tienda,
                    AssignmentType = assignmentType,
                    IsActive = isActive
                };

                var result = await _assignmentService.GetAssignmentsAsync(request);
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
        /// Obtener asignaciones de usuarios por tienda
        /// </summary>
        /// <param name="tienda">Código de la tienda</param>
        /// <returns>Lista de asignaciones activas de la tienda</returns>
        [HttpGet("tienda/{tienda}")]
        [ProducesResponseType(typeof(ApiResponse<List<AssignmentResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<AssignmentResponse>>>> GetAssignmentsByTienda(string tienda)
        {
            try
            {
                var result = await _assignmentService.GetUserAssignmentsByTiendaAsync(tienda);
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
        /// Obtener la jerarquía completa de productos (divisiones, categorías, grupos, subgrupos)
        /// </summary>
        /// <returns>Lista de elementos de la jerarquía de productos</returns>
        [HttpGet("hierarchy")]
        [ProducesResponseType(typeof(ApiResponse<List<ProductHierarchyItem>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<ProductHierarchyItem>>>> GetProductHierarchy()
        {
            try
            {
                var result = await _assignmentService.GetProductHierarchyAsync();
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
        /// Eliminar (desactivar) una asignación
        /// </summary>
        /// <param name="assignmentId">ID de la asignación a eliminar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpDelete("{assignmentId}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteAssignment(int assignmentId)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var result = await _assignmentService.DeleteAssignmentAsync(assignmentId, currentUserId);

                if (result.Success)
                {
                    return Ok(result);
                }

                return NotFound(result);
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
        /// Obtener las asignaciones del usuario actual
        /// </summary>
        /// <returns>Lista de asignaciones del usuario autenticado</returns>
        [HttpGet("my-assignments")]
        [ProducesResponseType(typeof(ApiResponse<List<AssignmentResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<AssignmentResponse>>>> GetMyAssignments()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var request = new GetAssignmentsRequest
                {
                    UserID = currentUserId,
                    IsActive = true
                };

                var result = await _assignmentService.GetAssignmentsAsync(request);
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
        /// Actualizar una asignación existente
        /// </summary>
        /// <param name="assignmentId">ID de la asignación a actualizar</param>
        /// <param name="request">Nuevos datos de la asignación</param>
        /// <returns>Asignación actualizada</returns>
        [HttpPut("{assignmentId}")]
        [ProducesResponseType(typeof(ApiResponse<AssignmentResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<AssignmentResponse>>> UpdateAssignment(
            int assignmentId, 
            [FromBody] CreateAssignmentRequest request)
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

                var validationError = ValidateAssignmentRequest(request);
                if (!string.IsNullOrEmpty(validationError))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = validationError
                    });
                }

                var currentUserId = GetCurrentUserId();
                
                // Primero eliminar la asignación anterior
                var deleteResult = await _assignmentService.DeleteAssignmentAsync(assignmentId, currentUserId);
                if (!deleteResult.Success)
                {
                    return NotFound(deleteResult);
                }

                // Crear la nueva asignación
                var createResult = await _assignmentService.CreateAssignmentAsync(request, currentUserId);
                
                if (createResult.Success)
                {
                    return Ok(createResult);
                }

                return BadRequest(createResult);
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
        /// Obtener lista de usuarios disponibles para asignación
        /// </summary>
        /// <param name="tienda">Filtrar por tienda (opcional)</param>
        /// <returns>Lista de usuarios</returns>
        [HttpGet("users")]
        [ProducesResponseType(typeof(ApiResponse<List<UserInfo>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public ActionResult<ApiResponse<List<UserInfo>>> GetAvailableUsers([FromQuery] string tienda = null)
        {
            try
            {
                // Este endpoint podría ser implementado en el AuthService o crear un UserService
                // Por ahora retornamos una respuesta básica
                return Ok(new ApiResponse<List<UserInfo>>
                {
                    Success = true,
                    Message = "Endpoint para obtener usuarios disponibles - Por implementar",
                    Data = new List<UserInfo>()
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
        /// Verificar si un usuario tiene asignaciones en una tienda específica
        /// </summary>
        /// <param name="userId">ID del usuario</param>
        /// <param name="tienda">Código de la tienda</param>
        /// <returns>Información sobre las asignaciones del usuario</returns>
        [HttpGet("check-user/{userId}/tienda/{tienda}")]
        [ProducesResponseType(typeof(ApiResponse<List<AssignmentResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<AssignmentResponse>>>> CheckUserAssignments(int userId, string tienda)
        {
            try
            {
                var request = new GetAssignmentsRequest
                {
                    UserID = userId,
                    Tienda = tienda,
                    IsActive = true
                };

                var result = await _assignmentService.GetAssignmentsAsync(request);
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
        /// Obtener estadísticas de asignaciones
        /// </summary>
        /// <returns>Estadísticas generales de asignaciones</returns>
        [HttpGet("stats")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> GetAssignmentStats()
        {
            try
            {
                var allAssignments = await _assignmentService.GetAssignmentsAsync(new GetAssignmentsRequest { IsActive = true });
                
                if (allAssignments.Success && allAssignments.Data != null)
                {
                    var stats = new
                    {
                        TotalAssignments = allAssignments.Data.Count,
                        AssignmentsByType = allAssignments.Data
                            .GroupBy(a => a.AssignmentType)
                            .ToDictionary(g => g.Key.ToString(), g => g.Count()),
                        AssignmentsByTienda = allAssignments.Data
                            .GroupBy(a => a.Tienda)
                            .ToDictionary(g => g.Key, g => g.Count()),
                        UniqueUsers = allAssignments.Data.Select(a => a.UserID).Distinct().Count()
                    };

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Estadísticas obtenidas exitosamente",
                        Data = stats
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "No hay asignaciones disponibles",
                    Data = new { TotalAssignments = 0 }
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

        // Métodos privados auxiliares
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            throw new UnauthorizedAccessException("No se pudo obtener el ID del usuario autenticado");
        }

        private string ValidateAssignmentRequest(CreateAssignmentRequest request)
        {
            switch (request.AssignmentType)
            {
                case AssignmentType.DIVISION:
                    if (string.IsNullOrEmpty(request.DivisionCode) || string.IsNullOrEmpty(request.Division))
                    {
                        return "Para asignación de DIVISION se requiere DivisionCode y Division";
                    }
                    break;

                case AssignmentType.CATEGORIA:
                    if (string.IsNullOrEmpty(request.DivisionCode) || string.IsNullOrEmpty(request.Division) ||
                        string.IsNullOrEmpty(request.CategoryCode) || string.IsNullOrEmpty(request.Categoria))
                    {
                        return "Para asignación de CATEGORIA se requiere DivisionCode, Division, CategoryCode y Categoria";
                    }
                    break;

                case AssignmentType.GRUPO:
                    if (string.IsNullOrEmpty(request.DivisionCode) || string.IsNullOrEmpty(request.Division) ||
                        string.IsNullOrEmpty(request.CategoryCode) || string.IsNullOrEmpty(request.Categoria) ||
                        string.IsNullOrEmpty(request.GroupCode) || string.IsNullOrEmpty(request.Grupo))
                    {
                        return "Para asignación de GRUPO se requiere DivisionCode, Division, CategoryCode, Categoria, GroupCode y Grupo";
                    }
                    break;

                case AssignmentType.SUBGRUPO:
                    if (string.IsNullOrEmpty(request.DivisionCode) || string.IsNullOrEmpty(request.Division) ||
                        string.IsNullOrEmpty(request.CategoryCode) || string.IsNullOrEmpty(request.Categoria) ||
                        string.IsNullOrEmpty(request.GroupCode) || string.IsNullOrEmpty(request.Grupo) ||
                        string.IsNullOrEmpty(request.SubGroupCode) || string.IsNullOrEmpty(request.SubGrupo))
                    {
                        return "Para asignación de SUBGRUPO se requieren todos los campos de la jerarquía";
                    }
                    break;

                default:
                    return "Tipo de asignación no válido";
            }

            return string.Empty;
        }
    }
}