using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace API_Book.Models
{
    [Table("cart_items")]
    public class CartItem
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("user_id")]
        public int UserId { get; set; }

        [Required]
        [Column("book_id")]
        public int BookId { get; set; }

        [Required]
        [Range(1, 99, ErrorMessage = "Quantity must be between 1 and 99")]
        [Column("quantity")]
        public int Quantity { get; set; }

        [Column("added_at")]
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;

        [ForeignKey("BookId")]
        public virtual Book Book { get; set; } = null!;
    }
}