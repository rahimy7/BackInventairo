using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InventarioAPI.Services;
using InventarioAPI.Models;
using System.Security.Claims;
using System.Threading.Tasks;


namespace InventarioAPI.Controllers
{

    [ApiController]
    [Route("api/leader")]
    [Authorize]
    public class LeaderController : ControllerBase
    {
        private readonly IInventoryCountService _inventoryCountService;

        public LeaderController(IInventoryCountService inventoryCountService)
        {
            _inventoryCountService = inventoryCountService;
        }

        [HttpPost("dashboard")]
        public async Task<IActionResult> GetDashboard([FromBody] GetInventoryCountsRequest request)
        {
            var result = await _inventoryCountService.GetCountDashboardAsync(request.Tienda);
            return Ok(result);
        }

        [HttpPost("assigned-codes")]
        public async Task<IActionResult> GetAssignedCodes([FromBody] GetInventoryCountsRequest request)
        {
            var result = await _inventoryCountService.GetInventoryCountsAsync(request);
            return Ok(result);
        }

        [HttpPost("pending-counts")]
        public async Task<IActionResult> GetPendingCounts([FromBody] GetInventoryCountsRequest request)
        {
            var result = await _inventoryCountService.GetPendingCountsByRequestAsync(request.RequestID ?? 0);
            return Ok(result);
        }

        [HttpPost("register-count")]
        public async Task<IActionResult> RegisterCount([FromBody] RegisterPhysicalCountRequest request)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
            var result = await _inventoryCountService.RegisterPhysicalCountAsync(request, userId);
            return Ok(result);
        }

        [HttpPost("stats")]
        public async Task<IActionResult> GetStats([FromBody] GetInventoryCountsRequest request)
        {
            var result = await _inventoryCountService.GetCountDashboardAsync(request.Tienda);
            return Ok(result);
        }
    }
}