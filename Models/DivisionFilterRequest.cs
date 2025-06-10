using System;
using System.Collections.Generic;

namespace InventarioAPI.Models
{
    public class DivisionFilterRequest
    {
        public List<string> DivisionCodes { get; set; } = new();
        public string Tienda { get; set; } = string.Empty;
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
