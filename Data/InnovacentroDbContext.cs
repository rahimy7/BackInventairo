using Microsoft.EntityFrameworkCore;
using InventarioAPI.Models;

namespace InventarioAPI.Data
{
    public class InnovacentroDbContext : DbContext
    {
        public InnovacentroDbContext(DbContextOptions<InnovacentroDbContext> options) : base(options) { }

        public DbSet<ProductView> ProductViews { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductView>().HasNoKey().ToView("View_ProductosLI");
        }
    }
}
