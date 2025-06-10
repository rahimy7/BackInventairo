using InventarioAPI.Models;
using InventarioAPI.Models.Tasks;

namespace InventarioAPI.Services
{
    public interface ITaskService
    {
        Task<ApiResponse<int>> CreateTaskAsync(TaskRequest request);
        Task<ApiResponse<List<TaskResponse>>> GetTasksAsync(int? assignedToId = null, bool? completed = null);
        Task<ApiResponse<TaskStatsResponse>> GetStatsAsync();
        Task<ApiResponse<bool>> CompleteTaskAsync(int taskId);
        Task<ApiResponse<bool>> DeleteTaskAsync(int taskId);
    }
}
