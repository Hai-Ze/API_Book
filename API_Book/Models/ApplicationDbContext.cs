using Microsoft.EntityFrameworkCore;

namespace API_Book.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Book> Books { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Book>(entity =>
            {
                entity.ToTable("books");
                entity.HasKey(e => e.Id);

                // Cấu hình kiểu dữ liệu cho PostgreSQL
                entity.Property(e => e.Price).HasColumnType("real");
                entity.Property(e => e.Rating).HasColumnType("real");
            });
        }
    }
}