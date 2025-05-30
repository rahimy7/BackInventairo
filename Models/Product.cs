// Product.cs
namespace InventarioAPI.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string DivisionCode { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;
        public string Description1 { get; set; } = string.Empty;
        public string Description2 { get; set; } = string.Empty;
    }
}

// Request.cs
namespace InventarioAPI.Models
{
    public class Request
    {
        public int Id { get; set; }
        public string TicketNumber { get; set; } = string.Empty;
        public string Tienda { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RequestPriority Priority { get; set; }
        public RequestStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public enum RequestPriority
    {
        BAJA,
        NORMAL,
        ALTA,
        URGENTE
    }

    public enum RequestStatus
    {
        PENDIENTE,
        EN_REVISION,
        LISTO,
        AJUSTADO
    }
}

// Conteo.cs
namespace InventarioAPI.Models
{
    public class Conteo
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; }
        public DateTime CountedAt { get; set; }
    }
}

// Assignment.cs
namespace InventarioAPI.Models
{
    public class Assignment
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public int UserId { get; set; }
        public string Code { get; set; } = string.Empty;
        public DateTime AssignedAt { get; set; }
    }
}
