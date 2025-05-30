using Microsoft.EntityFrameworkCore;
using InventarioAPI.Models;

namespace InventarioAPI.Data
{
    public class InventarioDbContext : DbContext
    {
        public InventarioDbContext(DbContextOptions<InventarioDbContext> options) : base(options) { }

public DbSet<Product> Products { get; set; }

        // Aún no tienes entidades reales con estos nombres
        // public DbSet<Product> Products { get; set; }
        // public DbSet<Request> Requests { get; set; }
        // public DbSet<Conteo> Conteos { get; set; }
        // public DbSet<Assignment> Assignments { get; set; }

        // Cuando crees las clases entidad, descomenta y ajusta aquí
    }
}
