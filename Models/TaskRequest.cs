namespace InventarioAPI.Models.Tasks
{
    public class TaskRequest
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public int AssignedTo { get; set; }
    }
}
