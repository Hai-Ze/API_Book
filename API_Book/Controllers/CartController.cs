using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using API_Book.Models.DTOs;
using API_Book.Services;
using System.Security.Claims;

namespace API_Book.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Tất cả endpoints cần đăng nhập
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CartController> _logger;

        public CartController(ICartService cartService, ILogger<CartController> logger)
        {
            _cartService = cartService;
            _logger = logger;
        }

        /// <summary>
        /// Thêm sách vào giỏ hàng
        /// </summary>
        [HttpPost("add")]
        public async Task<ActionResult<AddToCartResponseDTO>> AddToCart([FromBody] AddToCartDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    _logger.LogWarning("User ID not found in token");
                    return Unauthorized(new
                    {
                        Success = false,
                        Message = "Vui lòng đăng nhập"
                    });
                }

                _logger.LogInformation($"Adding book {request.BookId} to cart for user {userId}");

                var result = await _cartService.AddToCartAsync(userId, request);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return BadRequest(result);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart");
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Lỗi server: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Lấy giỏ hàng của user hiện tại
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<CartResponseDTO>> GetCart()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new CartResponseDTO
                    {
                        Success = false,
                        Message = "Vui lòng đăng nhập"
                    });
                }

                var result = await _cartService.GetUserCartAsync(userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart");
                return StatusCode(500, new CartResponseDTO
                {
                    Success = false,
                    Message = $"Lỗi server: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Cập nhật số lượng item trong giỏ
        /// </summary>
        [HttpPut("update")]
        public async Task<ActionResult> UpdateCartItem([FromBody] UpdateCartDTO request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new { Success = false, Message = "Vui lòng đăng nhập" });
                }

                var success = await _cartService.UpdateCartItemAsync(userId, request);

                if (success)
                {
                    return Ok(new { Success = true, Message = "Cập nhật giỏ hàng thành công" });
                }
                else
                {
                    return NotFound(new { Success = false, Message = "Không tìm thấy item trong giỏ hàng" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item");
                return StatusCode(500, new { Success = false, Message = $"Lỗi server: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xóa item khỏi giỏ hàng
        /// </summary>
        [HttpDelete("remove/{cartItemId}")]
        public async Task<ActionResult> RemoveFromCart(int cartItemId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new { Success = false, Message = "Vui lòng đăng nhập" });
                }

                var success = await _cartService.RemoveFromCartAsync(userId, cartItemId);

                if (success)
                {
                    return Ok(new { Success = true, Message = "Đã xóa khỏi giỏ hàng" });
                }
                else
                {
                    return NotFound(new { Success = false, Message = "Không tìm thấy item trong giỏ hàng" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart item");
                return StatusCode(500, new { Success = false, Message = $"Lỗi server: {ex.Message}" });
            }
        }

        /// <summary>
        /// Xóa toàn bộ giỏ hàng
        /// </summary>
        [HttpDelete("clear")]
        public async Task<ActionResult> ClearCart()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new { Success = false, Message = "Vui lòng đăng nhập" });
                }

                var success = await _cartService.ClearCartAsync(userId);

                if (success)
                {
                    return Ok(new { Success = true, Message = "Đã xóa toàn bộ giỏ hàng" });
                }
                else
                {
                    return BadRequest(new { Success = false, Message = "Lỗi khi xóa giỏ hàng" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart");
                return StatusCode(500, new { Success = false, Message = $"Lỗi server: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy số lượng items trong giỏ hàng
        /// </summary>
        [HttpGet("count")]
        public async Task<ActionResult> GetCartItemsCount()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Ok(new { Success = true, Count = 0 });
                }

                var count = await _cartService.GetCartItemsCountAsync(userId);
                return Ok(new { Success = true, Count = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart count");
                return StatusCode(500, new { Success = false, Message = $"Lỗi server: {ex.Message}" });
            }
        }

        /// <summary>
        /// Test endpoint để debug user info
        /// </summary>
        [HttpGet("debug-user")]
        public ActionResult DebugUser()
        {
            try
            {
                var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
                var userId = GetCurrentUserId();

                return Ok(new
                {
                    Success = true,
                    UserId = userId,
                    Claims = claims,
                    IsAuthenticated = User.Identity?.IsAuthenticated,
                    AuthenticationType = User.Identity?.AuthenticationType
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Success = false,
                    Error = ex.Message,
                    Claims = new List<object>()
                });
            }
        }

        /// <summary>
        /// Lấy User ID từ JWT token - FIXED VERSION
        /// </summary>
        private int GetCurrentUserId()
        {
            try
            {
                // Thử tất cả các claim types có thể chứa user ID
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User.FindFirst("sub")?.Value
                                ?? User.FindFirst("user_id")?.Value
                                ?? User.FindFirst("id")?.Value;

                _logger.LogInformation($"Raw user ID claim: {userIdClaim}");
                _logger.LogInformation($"Available claims: {string.Join(", ", User.Claims.Select(c => $"{c.Type}:{c.Value}"))}");

                if (int.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogInformation($"Successfully parsed user ID: {userId}");
                    return userId;
                }

                _logger.LogWarning($"Failed to parse user ID from claim: {userIdClaim}");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user ID from token");
                return 0;
            }
        }
    }
}