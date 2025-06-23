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
        public DbSet<User> Users { get; set; } // ← THÊM DÒNG NÀY

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình Book entity (GIỮ NGUYÊN)
            modelBuilder.Entity<Book>(entity =>
            {
                entity.ToTable("books");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd()
                    .UseIdentityColumn();

                entity.Property(e => e.Price).HasColumnType("numeric(10,2)");
                entity.Property(e => e.Rating).HasColumnType("numeric(3,1)");
                entity.Ignore(e => e.GenresJsonb);

                entity.Property(e => e.Title).HasColumnName("title").IsRequired().HasMaxLength(500);
                entity.Property(e => e.Author).HasColumnName("author").IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.Genres).HasColumnName("genres");
                entity.Property(e => e.CoverImg).HasColumnName("coverImg");
                entity.Property(e => e.Language).HasColumnName("language");
                entity.Property(e => e.Series).HasColumnName("series");
                entity.Property(e => e.Pages).HasColumnName("pages");
                entity.Property(e => e.RatingsByStars).HasColumnName("ratingsByStars");
                entity.Property(e => e.NumRatings).HasColumnName("numRatings");
            });

            // Cấu hình User entity (MỚI)
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd()
                    .UseIdentityColumn();

                // Email phải unique
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasIndex(e => e.GoogleId).IsUnique();

                entity.Property(e => e.Role)
                    .HasDefaultValue("Customer");

                entity.Property(e => e.IsActive)
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.LastLogin)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");


            });
            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.ToTable("cart_items");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd()
                    .UseIdentityColumn();

                // Unique constraint cho user_id + book_id
                entity.HasIndex(e => new { e.UserId, e.BookId })
                    .IsUnique()
                    .HasDatabaseName("uk_user_book");

                // Relationships
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Book)
                    .WithMany()
                    .HasForeignKey(e => e.BookId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Default values
                entity.Property(e => e.Quantity)
                    .HasDefaultValue(1);

                entity.Property(e => e.AddedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UpdatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");
            });
        }
    }
}