using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel;

namespace API_Book.Models
{
    [Table("books")]
    public class Book
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // SỬA LỖI: Thêm Identity
        [Description("Unique identifier for the book")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(500, ErrorMessage = "Title cannot exceed 500 characters")]
        [Column("title")]
        [Description("Title of the book")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Author is required")]
        [StringLength(200, ErrorMessage = "Author name cannot exceed 200 characters")]
        [Column("author")]
        [Description("Author of the book")]
        public string Author { get; set; } = string.Empty;

        [Range(0, 999999.99, ErrorMessage = "Price must be between 0 and 999,999.99")]
        [Column("price")]
        [Description("Price of the book")]
        public float Price { get; set; }

        [Column("description")]
        [Description("Description of the book")]
        public string? Description { get; set; }

        [Column("genres")]
        [Description("Genres of the book (comma-separated)")]
        public string? Genres { get; set; }

        [Column("coverImg")]
        [Description("URL of the book cover image")]
        public string? CoverImg { get; set; }

        [Column("language")]
        [Description("Language of the book")]
        public string? Language { get; set; }

        [Range(0, 5, ErrorMessage = "Rating must be between 0 and 5")]
        [Column("rating")]
        [Description("Average rating of the book")]
        public float? Rating { get; set; }

        [Column("series")]
        [Description("Series name if book is part of a series")]
        public string? Series { get; set; }

        [Range(1, 9999, ErrorMessage = "Pages must be between 1 and 9,999")]
        [Column("pages")]
        [Description("Number of pages in the book")]
        public int? Pages { get; set; }

        [Column("ratingsByStars")]
        [Description("Ratings breakdown by stars (JSON format)")]
        public string? RatingsByStars { get; set; }

        [Column("numRatings")]
        [Description("Total number of ratings")]
        public int? NumRatings { get; set; }

        [Column("genres_jsonb")]
        [Description("Genres in JSON format")]
        public string? GenresJsonb { get; set; }
    }
}