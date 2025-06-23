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
        public DbSet<User> Users { get; set; }
        public DbSet<CartItem> CartItems { get; set; }

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

            // Cấu hình User entity (CẬP NHẬT)
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .ValueGeneratedOnAdd()
                    .UseIdentityColumn();

                // Email phải unique và không null
                entity.HasIndex(e => e.Email)
                    .IsUnique()
                    .HasDatabaseName("uk_users_email");

                entity.Property(e => e.Email)
                    .IsRequired()
                    .HasMaxLength(255)
                    .HasColumnName("email");

                // GoogleId có thể null ban đầu, nhưng nếu có thì phải unique
                entity.HasIndex(e => e.GoogleId)
                    .IsUnique()
                    .HasDatabaseName("uk_users_google_id")
                    .HasFilter("google_id IS NOT NULL AND google_id != ''");

                entity.Property(e => e.GoogleId)
                    .HasMaxLength(100)
                    .HasColumnName("google_id");

                entity.Property(e => e.FullName)
                    .IsRequired()
                    .HasMaxLength(200)
                    .HasColumnName("full_name");

                entity.Property(e => e.AvatarUrl)
                    .HasMaxLength(500)
                    .HasColumnName("avatar_url");

                entity.Property(e => e.Role)
                    .HasMaxLength(20)
                    .HasDefaultValue("Customer")
                    .HasColumnName("role");

                entity.Property(e => e.IsActive)
                    .HasDefaultValue(true)
                    .HasColumnName("is_active");

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .HasColumnName("created_at");

                entity.Property(e => e.LastLogin)
                    .HasDefaultValueSql("CURRENT_TIMESTAMP")
                    .HasColumnName("last_login");
            });

            // Cấu hình CartItem entity (GIỮ NGUYÊN)
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