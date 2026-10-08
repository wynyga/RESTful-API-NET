using Microsoft.EntityFrameworkCore;
using Models.Entities;

namespace Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Project> Projects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Konfigurasi tambahan jika diperlukan
            modelBuilder.Entity<User>(entity => 
            {
                entity.HasIndex(e => e.Email).IsUnique();
            });

            modelBuilder.Entity<Project>(entity =>
            {
                entity.HasIndex(e => e.ProjectName).IsUnique();
            });
        }
    }
}

