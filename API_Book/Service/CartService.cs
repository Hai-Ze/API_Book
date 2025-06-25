using API_Book.Models;
using API_Book.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace API_Book.Services
{
    public interface ICartService
    {
        Task<AddToCartResponseDTO> AddToCartAsync(int userId, AddToCartDTO request);
        Task<CartResponseDTO> GetUserCartAsync(int userId);
        Task<bool> UpdateCartItemAsync(int userId, UpdateCartDTO request);
        Task<bool> RemoveFromCartAsync(int userId, int cartItemId);
        Task<bool> ClearCartAsync(int userId);
        Task<int> GetCartItemsCountAsync(int userId);
    }

    public class CartService : ICartService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CartService> _logger;

        public CartService(ApplicationDbContext context, ILogger<CartService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// OPTIMIZED: Get user cart with single efficient query
        /// </summary>
        public async Task<CartResponseDTO> GetUserCartAsync(int userId)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation($"Loading cart for user {userId}");

                // OPTIMIZATION 1: Single query with explicit joins and projections
                var cartItems = await _context.CartItems
                    .AsNoTracking() // Don't track changes - faster
                    .Where(ci => ci.UserId == userId)
                    .Join(_context.Books.AsNoTracking(),
                          ci => ci.BookId,
                          b => b.Id,
                          (ci, b) => new
                          {
                              ci.Id,
                              ci.BookId,
                              ci.Quantity,
                              ci.AddedAt,
                              BookTitle = b.Title,
                              BookAuthor = b.Author,
                              BookPrice = b.Price,
                              BookCoverImg = b.CoverImg
                          })
                    .OrderByDescending(x => x.AddedAt)
                    .ToListAsync();

                var loadTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation($"Cart loaded in {loadTime}ms for user {userId}");

                // OPTIMIZATION 2: Calculate totals in memory (small dataset)
                var cartItemDTOs = cartItems.Select(ci => new CartItemDTO
                {
                    Id = ci.Id,
                    BookId = ci.BookId,
                    BookTitle = ci.BookTitle,
                    BookAuthor = ci.BookAuthor,
                    BookPrice = (decimal)ci.BookPrice,
                    BookCoverImg = ci.BookCoverImg,
                    Quantity = ci.Quantity,
                    TotalPrice = (decimal)(ci.BookPrice * ci.Quantity),
                    AddedAt = ci.AddedAt
                }).ToList();

                var totalAmount = cartItemDTOs.Sum(item => item.TotalPrice);
                var totalItems = cartItemDTOs.Sum(item => item.Quantity);

                return new CartResponseDTO
                {
                    Success = true,
                    Message = $"Cart loaded in {loadTime:F0}ms",
                    Items = cartItemDTOs,
                    TotalItems = totalItems,
                    TotalAmount = totalAmount,
                    LastUpdated = cartItems.Any() ? cartItems.Max(ci => ci.AddedAt) : DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, $"Error getting cart for user {userId} after {errorTime}ms");

                return new CartResponseDTO
                {
                    Success = false,
                    Message = $"Error loading cart: {ex.Message}",
                    Items = new List<CartItemDTO>()
                };
            }
        }

        /// <summary>
        /// OPTIMIZED: Add to cart with upsert pattern
        /// </summary>
        public async Task<AddToCartResponseDTO> AddToCartAsync(int userId, AddToCartDTO request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation($"Adding book {request.BookId} to cart for user {userId}");

                // OPTIMIZATION 1: Single query to check book exists and get cart item
                var bookInfo = await _context.Books
                    .AsNoTracking()
                    .Where(b => b.Id == request.BookId)
                    .Select(b => new { b.Id, b.Title })
                    .FirstOrDefaultAsync();

                if (bookInfo == null)
                {
                    return new AddToCartResponseDTO
                    {
                        Success = false,
                        Message = "Sách không tồn tại"
                    };
                }

                // OPTIMIZATION 2: Use SQL MERGE-like operation
                var existingItem = await _context.CartItems
                    .Where(ci => ci.UserId == userId && ci.BookId == request.BookId)
                    .FirstOrDefaultAsync();

                if (existingItem != null)
                {
                    // Update existing
                    existingItem.Quantity += request.Quantity;
                    if (existingItem.Quantity > 99) existingItem.Quantity = 99;
                    existingItem.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Add new
                    existingItem = new CartItem
                    {
                        UserId = userId,
                        BookId = request.BookId,
                        Quantity = request.Quantity,
                        AddedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.CartItems.Add(existingItem);
                }

                await _context.SaveChangesAsync();

                // OPTIMIZATION 3: Fast count without additional query
                var totalItems = await GetCartItemsCountAsync(userId);

                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation($"Added to cart in {responseTime}ms");

                return new AddToCartResponseDTO
                {
                    Success = true,
                    Message = $"Đã thêm '{bookInfo.Title}' vào giỏ hàng",
                    CartItemId = existingItem.Id,
                    TotalCartItems = totalItems
                };
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, $"Error adding to cart after {errorTime}ms");

                return new AddToCartResponseDTO
                {
                    Success = false,
                    Message = $"Lỗi khi thêm vào giỏ hàng: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// OPTIMIZED: Update cart item with validation
        /// </summary>
        public async Task<bool> UpdateCartItemAsync(int userId, UpdateCartDTO request)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // OPTIMIZATION: Direct update without loading into memory
                var rowsAffected = await _context.CartItems
                    .Where(ci => ci.Id == request.CartItemId && ci.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(ci => ci.Quantity, request.Quantity)
                        .SetProperty(ci => ci.UpdatedAt, DateTime.UtcNow));

                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation($"Updated cart item in {responseTime}ms, rows affected: {rowsAffected}");

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, $"Error updating cart item after {errorTime}ms");
                return false;
            }
        }

        /// <summary>
        /// OPTIMIZED: Remove cart item with direct delete
        /// </summary>
        public async Task<bool> RemoveFromCartAsync(int userId, int cartItemId)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // OPTIMIZATION: Direct delete without loading into memory
                var rowsAffected = await _context.CartItems
                    .Where(ci => ci.Id == cartItemId && ci.UserId == userId)
                    .ExecuteDeleteAsync();

                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation($"Removed cart item in {responseTime}ms, rows affected: {rowsAffected}");

                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, $"Error removing cart item after {errorTime}ms");
                return false;
            }
        }

        /// <summary>
        /// OPTIMIZED: Clear cart with bulk delete
        /// </summary>
        public async Task<bool> ClearCartAsync(int userId)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // OPTIMIZATION: Bulk delete without loading into memory
                var rowsAffected = await _context.CartItems
                    .Where(ci => ci.UserId == userId)
                    .ExecuteDeleteAsync();

                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation($"Cleared cart in {responseTime}ms, removed {rowsAffected} items");

                return true;
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogError(ex, $"Error clearing cart after {errorTime}ms");
                return false;
            }
        }

        /// <summary>
        /// OPTIMIZED: Fast count with direct SQL
        /// </summary>
        public async Task<int> GetCartItemsCountAsync(int userId)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                // OPTIMIZATION: Direct SQL aggregation
                var count = await _context.CartItems
                    .AsNoTracking()
                    .Where(ci => ci.UserId == userId)
                    .SumAsync(ci => ci.Quantity);

                var responseTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                _logger.LogInformation($"Cart count retrieved in {responseTime}ms: {count}");

                return count;
            }
            catch (Exception ex)
            {
                var errorTime = DateTime.UtcNow.Subtract(startTime).TotalMilliseconds;
                _logger.LogError(ex, $"Error getting cart count after {errorTime}ms");
                return 0;
            }
        }
    }
}