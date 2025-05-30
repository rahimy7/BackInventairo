using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using InventarioAPI.Models;
using InventarioAPI.Services;
using System.Security.Claims;
using InventarioAPI.Data;
using Microsoft.EntityFrameworkCore; // O el namespace real de tu InventarioDbContext


namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class RequestController : ControllerBase
    {
        private readonly IRequestService _requestService;
        private readonly InventarioDbContext _context;
        private readonly InnovacentroDbContext _innovacentroContext;

        public RequestController(IRequestService requestService, InventarioDbContext context, InnovacentroDbContext innovacentroContext)
        {
            _requestService = requestService;
            _context = context;
            _innovacentroContext = innovacentroContext;
        }


        /// <summary>
        /// Crear una nueva solicitud de códigos
        /// </summary>
        /// <param name="request">Datos de la solicitud</param>
        /// <returns>Solicitud creada</returns>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<RequestResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RequestResponse>>> CreateRequest([FromBody] CreateRequestRequest request)
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
                var result = await _requestService.CreateRequestAsync(request, currentUserId);

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
        /// Obtener solicitudes con filtros y paginación
        /// </summary>
        /// <param name="tienda">Filtrar por tienda</param>
        /// <param name="status">Filtrar por estado</param>
        /// <param name="priority">Filtrar por prioridad</param>
        /// <param name="requestorId">Filtrar por solicitante</param>
        /// <param name="fromDate">Fecha desde</param>
        /// <param name="toDate">Fecha hasta</param>
        /// <param name="pageNumber">Número de página</param>
        /// <param name="pageSize">Tamaño de página</param>
        /// <param name="searchTerm">Término de búsqueda</param>
        /// <returns>Lista paginada de solicitudes</returns>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<RequestResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestResponse>>>> GetRequests(
            [FromQuery] string tienda = "",
            [FromQuery] RequestStatus? status = null,
            [FromQuery] RequestPriority? priority = null,
            [FromQuery] int? requestorId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string searchTerm = "")
        {
            try
            {
                var request = new GetRequestsRequest
                {
                    Tienda = tienda,
                    Status = status,
                    Priority = priority,
                    RequestorID = requestorId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    PageNumber = pageNumber,
                    PageSize = Math.Min(pageSize, 100),
                    SearchTerm = searchTerm
                };

                var result = await _requestService.GetRequestsAsync(request);
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
        /// Obtener una solicitud por su ID
        /// </summary>
        /// <param name="id">ID de la solicitud</param>
        /// <returns>Solicitud con códigos asociados</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ApiResponse<RequestResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RequestResponse>>> GetRequestById(int id)
        {
            try
            {
                var result = await _requestService.GetRequestByIdAsync(id);

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
        /// Obtener una solicitud por número de ticket
        /// </summary>
        /// <param name="ticketNumber">Número de ticket (ej: REQ-20231128-0001)</param>
        /// <returns>Solicitud con códigos asociados</returns>
        [HttpGet("ticket/{ticketNumber}")]
        [ProducesResponseType(typeof(ApiResponse<RequestResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RequestResponse>>> GetRequestByTicket(string ticketNumber)
        {
            try
            {
                var result = await _requestService.GetRequestByTicketAsync(ticketNumber);

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
        /// Actualizar el estado de un código específico
        /// </summary>
        /// <param name="request">Datos para actualizar el estado</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPut("code/status")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateCodeStatus([FromBody] UpdateCodeStatusRequest request)
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
                var result = await _requestService.UpdateCodeStatusAsync(request, currentUserId);

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
        /// Asignar un código manualmente a un usuario
        /// </summary>
        /// <param name="request">Datos de asignación</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPut("code/assign")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> AssignCode([FromBody] AssignCodeRequest request)
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
                var result = await _requestService.AssignCodeAsync(request, currentUserId);

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
        /// Agregar un comentario a una solicitud o código específico
        /// </summary>
        /// <param name="request">Datos del comentario</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("comment")]
        [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<bool>>> AddComment([FromBody] AddCommentRequest request)
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
                var result = await _requestService.AddCommentAsync(request, currentUserId);

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
        /// Obtener el historial completo de una solicitud
        /// </summary>
        /// <param name="requestId">ID de la solicitud</param>
        /// <returns>Lista de eventos del historial</returns>
        [HttpGet("{requestId}/history")]
        [ProducesResponseType(typeof(ApiResponse<List<RequestHistoryResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<RequestHistoryResponse>>>> GetRequestHistory(int requestId)
        {
            try
            {
                var result = await _requestService.GetRequestHistoryAsync(requestId);
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
        /// Obtener dashboard con estadísticas de solicitudes
        /// </summary>
        /// <param name="tienda">Filtrar por tienda (opcional)</param>
        /// <returns>Dashboard con estadísticas</returns>
        [HttpGet("dashboard")]
        [ProducesResponseType(typeof(ApiResponse<RequestDashboardResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<RequestDashboardResponse>>> GetDashboard([FromQuery] string tienda = "")
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var result = await _requestService.GetDashboardAsync(currentUserId, tienda);
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
        /// Obtener códigos asignados al usuario actual
        /// </summary>
        /// <returns>Lista de códigos asignados</returns>
        [HttpGet("my-codes")]
        [ProducesResponseType(typeof(ApiResponse<List<RequestCodeResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<List<RequestCodeResponse>>>> GetMyAssignedCodes()
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var result = await _requestService.GetMyAssignedCodesAsync(currentUserId);
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
        /// Obtener solicitudes del usuario actual
        /// </summary>
        /// <param name="status">Filtrar por estado</param>
        /// <param name="pageNumber">Número de página</param>
        /// <param name="pageSize">Tamaño de página</param>
        /// <returns>Solicitudes del usuario</returns>
        [HttpGet("my-requests")]
        [ProducesResponseType(typeof(ApiResponse<PagedResponse<RequestResponse>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<PagedResponse<RequestResponse>>>> GetMyRequests(
            [FromQuery] RequestStatus? status = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var request = new GetRequestsRequest
                {
                    RequestorID = currentUserId,
                    Status = status,
                    PageNumber = pageNumber,
                    PageSize = Math.Min(pageSize, 100)
                };

                var result = await _requestService.GetRequestsAsync(request);
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
        /// Actualizar múltiples códigos a la vez
        /// </summary>
        /// <param name="requests">Lista de actualizaciones</param>
        /// <returns>Resultado de las operaciones</returns>
        [HttpPut("codes/batch-update")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> BatchUpdateCodes([FromBody] List<UpdateCodeStatusRequest> requests)
        {
            try
            {
                if (!ModelState.IsValid || !requests.Any())
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Datos inválidos o lista vacía"
                    });
                }

                var currentUserId = GetCurrentUserId();
                var results = new List<object>();
                var successCount = 0;
                var failCount = 0;

                foreach (var request in requests)
                {
                    var result = await _requestService.UpdateCodeStatusAsync(request, currentUserId);

                    results.Add(new
                    {
                        CodeID = request.CodeID,
                        Success = result.Success,
                        Message = result.Message
                    });

                    if (result.Success)
                        successCount++;
                    else
                        failCount++;
                }

                return Ok(new ApiResponse<object>
                {
                    Success = failCount == 0,
                    Message = $"Procesadas {requests.Count} actualizaciones. Exitosas: {successCount}, Fallidas: {failCount}",
                    Data = new { Results = results, SuccessCount = successCount, FailCount = failCount }
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
        /// Obtener estadísticas por tienda
        /// </summary>
        /// <param name="tienda">Código de tienda</param>
        /// <returns>Estadísticas de la tienda</returns>
        [HttpGet("stats/tienda/{tienda}")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ApiResponse<object>>> GetTiendaStats(string tienda)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var dashboard = await _requestService.GetDashboardAsync(currentUserId, tienda);

                if (dashboard.Success)
                {
                    var stats = new
                    {
                        Tienda = tienda,
                        TotalRequests = dashboard.Data.TotalRequests,
                        PendingRequests = dashboard.Data.PendingRequests,
                        InReviewRequests = dashboard.Data.InReviewRequests,
                        CompletedRequests = dashboard.Data.CompletedRequests,
                        OverdueRequests = dashboard.Data.OverdueRequests,
                        CompletionRate = dashboard.Data.TotalRequests > 0 ?
                            Math.Round((decimal)dashboard.Data.CompletedRequests / dashboard.Data.TotalRequests * 100, 2) : 0
                    };

                    return Ok(new ApiResponse<object>
                    {
                        Success = true,
                        Message = "Estadísticas de tienda obtenidas exitosamente",
                        Data = stats
                    });
                }

                return BadRequest(dashboard);
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

        // En RequestController.cs o en un nuevo ProductController.cs

        [HttpGet("product/{productCode}")]
[Authorize]
public async Task<IActionResult> GetProductByCode(string productCode)
{
    try
    {
        var product = await _innovacentroContext.ProductViews
            .Where(p => p.Code == productCode)
            .FirstOrDefaultAsync();

        if (product == null)
        {
            return Ok(new ApiResponse<object>
            {
                Success = false,
                Message = "Producto no encontrado"
            });
        }

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Data = product
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new ApiResponse<object>
        {
            Success = false,
            Message = "Error al buscar producto",
            Errors = new List<string> { ex.Message }
        });
    }
}

    }
}