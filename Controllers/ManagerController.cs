using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventarioAPI.Services;
using InventarioAPI.Models;
using System.Security.Claims;
using System.Threading.Tasks;

namespace InventarioAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ManagerController : ControllerBase
    {
        private readonly IRequestService _requestService;

        public ManagerController(IRequestService requestService)
        {
            _requestService = requestService;
        }

        [HttpGet("dashboard/{tienda}")]
        public async Task<IActionResult> GetDashboard(string tienda)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var result = await _requestService.GetDashboardAsync(userId, tienda);
            return Ok(result);
        }

        [HttpGet("team/{tienda}")]
        public async Task<IActionResult> GetTeam(string tienda)
        {
            var result = await _requestService.GetTeamByStoreAsync(tienda);
            return Ok(result);
        }

        [HttpGet("requests")]
        public async Task<IActionResult> GetRequests([FromQuery] string tienda)
        {
            var result = await _requestService.GetRequestsByStoreAsync(tienda);
            return Ok(result);
        }

        [HttpPut("request/status")]
        public async Task<IActionResult> UpdateRequestStatus([FromBody] UpdateCodeStatusRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var result = await _requestService.UpdateCodeStatusAsync(request, userId);
            return Ok(result);
        }

        [HttpPost("assign-codes")]
        public async Task<IActionResult> AssignCodes([FromBody] AssignCodeRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var result = await _requestService.AssignCodeAsync(request, userId);
            return Ok(result);
        }
    }
}