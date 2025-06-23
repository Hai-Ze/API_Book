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

        public CartService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AddToCartResponseDTO> AddToCartAsync(int userId, AddToCartDTO request)
        {
            try
            {
                // Kiểm tra book có tồn tại không
                var book = await _context.Books.FindAsync(request.BookId);
                if (book == null)
                {
                    return new AddToCartResponseDTO
                    {
                        Success = false,
                        Message = "Sách không tồn tại"
                    };
                }

                // Kiểm tra item đã có trong cart chưa
                var existingItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.UserId == userId && ci.BookId == request.BookId);

                if (existingItem != null)
                {
                    // Cập nhật quantity
                    existingItem.Quantity += request.Quantity;
                    if (existingItem.Quantity > 99) existingItem.Quantity = 99;
                    existingItem.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    var totalItems = await GetCartItemsCountAsync(userId);
                    return new AddToCartResponseDTO
                    {
                        Success = true,
                        Message = $"Đã cập nhật số lượng sách '{book.Title}'",
                        CartItemId = existingItem.Id,
                        TotalCartItems = totalItems
                    };
                }
                else
                {
                    // Thêm item mới
                    var newItem = new CartItem
                    {
                        UserId = userId,
                        BookId = request.BookId,
                        Quantity = request.Quantity,
                        AddedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.CartItems.Add(newItem);
                    await _context.SaveChangesAsync();

                    var totalItems = await GetCartItemsCountAsync(userId);
                    return new AddToCartResponseDTO
                    {
                        Success = true,
                        Message = $"Đã thêm '{book.Title}' vào giỏ hàng",
                        CartItemId = newItem.Id,
                        TotalCartItems = totalItems
                    };
                }
            }
            catch (Exception ex)
            {
                return new AddToCartResponseDTO
                {
                    Success = false,
                    Message = $"Lỗi khi thêm vào giỏ hàng: {ex.Message}"
                };
            }
        }

        public async Task<CartResponseDTO> GetUserCartAsync(int userId)
        {
            try
            {
                var cartItems = await _context.CartItems
                    .Include(ci => ci.Book)
                    .Where(ci => ci.UserId == userId)
                    .OrderByDescending(ci => ci.AddedAt)
                    .ToListAsync();

                var cartItemDTOs = cartItems.Select(ci => new CartItemDTO
                {
                    Id = ci.Id,
                    BookId = ci.BookId,
                    BookTitle = ci.Book.Title,
                    BookAuthor = ci.Book.Author,
                    BookPrice = (decimal)ci.Book.Price,
                    BookCoverImg = ci.Book.CoverImg,
                    Quantity = ci.Quantity,
                    TotalPrice = (decimal)(ci.Book.Price * ci.Quantity),
                    AddedAt = ci.AddedAt
                }).ToList();

                var totalAmount = cartItemDTOs.Sum(item => item.TotalPrice);
                var totalItems = cartItemDTOs.Sum(item => item.Quantity);

                return new CartResponseDTO
                {
                    Success = true,
                    Message = "Lấy giỏ hàng thành công",
                    Items = cartItemDTOs,
                    TotalItems = totalItems,
                    TotalAmount = totalAmount,
                    LastUpdated = cartItems.Any() ? cartItems.Max(ci => ci.UpdatedAt) : DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                return new CartResponseDTO
                {
                    Success = false,
                    Message = $"Lỗi khi lấy giỏ hàng: {ex.Message}",
                    Items = new List<CartItemDTO>()
                };
            }
        }

        public async Task<bool> UpdateCartItemAsync(int userId, UpdateCartDTO request)
        {
            try
            {
                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.Id == request.CartItemId && ci.UserId == userId);

                if (cartItem == null) return false;

                cartItem.Quantity = request.Quantity;
                cartItem.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> RemoveFromCartAsync(int userId, int cartItemId)
        {
            try
            {
                var cartItem = await _context.CartItems
                    .FirstOrDefaultAsync(ci => ci.Id == cartItemId && ci.UserId == userId);

                if (cartItem == null) return false;

                _context.CartItems.Remove(cartItem);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ClearCartAsync(int userId)
        {
            try
            {
                var cartItems = await _context.CartItems
                    .Where(ci => ci.UserId == userId)
                    .ToListAsync();

                _context.CartItems.RemoveRange(cartItems);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<int> GetCartItemsCountAsync(int userId)
        {
            try
            {
                return await _context.CartItems
                    .Where(ci => ci.UserId == userId)
                    .SumAsync(ci => ci.Quantity);
            }
            catch (Exception)
            {
                return 0;
            }
        }
    }
}