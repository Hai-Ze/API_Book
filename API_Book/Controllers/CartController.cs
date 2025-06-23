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

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
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
                    return Unauthorized(new AddToCartResponseDTO
                    {
                        Success = false,
                        Message = "Vui lòng đăng nhập"
                    });
                }

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
                return StatusCode(500, new AddToCartResponseDTO
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
                    return Ok(new { Success = true, Message = "Đã xóa sách khỏi giỏ hàng" });
                }
                else
                {
                    return NotFound(new { Success = false, Message = "Không tìm thấy item trong giỏ hàng" });
                }
            }
            catch (Exception ex)
            {
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
                return StatusCode(500, new { Success = false, Message = $"Lỗi server: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy User ID từ JWT token
        /// </summary>
        private int GetCurrentUserId()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(userIdClaim, out var userId) ? userId : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}