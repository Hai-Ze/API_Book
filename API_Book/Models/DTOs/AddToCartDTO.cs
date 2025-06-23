using System.ComponentModel.DataAnnotations;

namespace API_Book.Models.DTOs
{
    // DTO cho Add to Cart request
    public class AddToCartDTO
    {
        [Required(ErrorMessage = "Book ID is required")]
        public int BookId { get; set; }

        [Range(1, 99, ErrorMessage = "Quantity must be between 1 and 99")]
        public int Quantity { get; set; } = 1;
    }

    // DTO cho Update Cart request  
    public class UpdateCartDTO
    {
        [Required(ErrorMessage = "Cart item ID is required")]
        public int CartItemId { get; set; }

        [Range(1, 99, ErrorMessage = "Quantity must be between 1 and 99")]
        public int Quantity { get; set; }
    }

    // DTO cho Cart Item response
    public class CartItemDTO
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public string BookTitle { get; set; } = string.Empty;
        public string BookAuthor { get; set; } = string.Empty;
        public decimal BookPrice { get; set; }
        public string? BookCoverImg { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime AddedAt { get; set; }
    }

    // DTO cho Cart response
    public class CartResponseDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<CartItemDTO> Items { get; set; } = new List<CartItemDTO>();
        public int TotalItems { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    // DTO cho Add to Cart response
    public class AddToCartResponseDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CartItemId { get; set; }
        public int TotalCartItems { get; set; }
    }
}