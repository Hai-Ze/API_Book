using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_Book.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(255)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [StringLength(100)]
        [Column("google_id")]
        public string GoogleId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [StringLength(500)]
        [Column("avatar_url")]
        public string? AvatarUrl { get; set; }

        [StringLength(20)]
        [Column("role")]
        public string Role { get; set; } = "Customer"; // Admin, Customer

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("last_login")]
        public DateTime LastLogin { get; set; } = DateTime.UtcNow;
    }
}