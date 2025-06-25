using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using API_Book.Models.DTOs;
using API_Book.Services;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;

namespace API_Book.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;
        private readonly ILogger<CartController> _logger;
        private readonly IMemoryCache _cache;

        public CartController(ICartService cartService, ILogger<CartController> logger, IMemoryCache cache)
        {
            _cartService = cartService;
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// OPTIMIZED: Get cart with caching
        /// </summary>
        [HttpGet]
        [ResponseCache(Duration = 10, VaryByHeader = "Authorization")] // HTTP cache
        public async Task<ActionResult<CartResponseDTO>> GetCart()
        {
            var requestStart = DateTime.UtcNow;
            var requestId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogInformation($"[{requestId}] GetCart request started");

            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    _logger.LogWarning($"[{requestId}] No valid user ID found");
                    return Ok(new CartResponseDTO
                    {
                        Success = true,
                        Message = "Giỏ hàng trống - chưa đăng nhập",
                        Items = new List<CartItemDTO>(),
                        TotalItems = 0,
                        TotalAmount = 0
                    });
                }

                // OPTIMIZATION 1: Memory cache with user-specific key
                var cacheKey = $"cart_{userId}";

                if (_cache.TryGetValue(cacheKey, out CartResponseDTO? cachedResult) && cachedResult != null)
                {
                    var cacheTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                    _logger.LogInformation($"[{requestId}] Cart served from cache in {cacheTime}ms");

                    // Add cache indicator
                    cachedResult.Message = $"Cached cart ({cacheTime:F0}ms)";
                    return Ok(cachedResult);
                }

                // OPTIMIZATION 2: Fast service call
                var result = await _cartService.GetUserCartAsync(userId);

                // OPTIMIZATION 3: Cache successful results for 30 seconds
                if (result.Success)
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30),
                        SlidingExpiration = TimeSpan.FromSeconds(10),
                        Priority = CacheItemPriority.Normal
                    };

                    _cache.Set(cacheKey, result, cacheOptions);
                }

                var totalTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogInformation($"[{requestId}] GetCart completed in {totalTime}ms");

                return Ok(result);
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogError(ex, $"[{requestId}] GetCart failed after {errorTime}ms");

                return StatusCode(500, new CartResponseDTO
                {
                    Success = false,
                    Message = $"Lỗi server: {ex.Message}",
                    Items = new List<CartItemDTO>()
                });
            }
        }

        /// <summary>
        /// OPTIMIZED: Add to cart with cache invalidation
        /// </summary>
        [HttpPost("add")]
        public async Task<ActionResult<AddToCartResponseDTO>> AddToCart([FromBody] AddToCartDTO request)
        {
            var requestStart = DateTime.UtcNow;
            var requestId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogInformation($"[{requestId}] AddToCart: BookId={request.BookId}, Quantity={request.Quantity}");

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

                // OPTIMIZATION: Invalidate cache after successful add
                if (result.Success)
                {
                    var cacheKey = $"cart_{userId}";
                    _cache.Remove(cacheKey);
                    _logger.LogInformation($"[{requestId}] Cache invalidated for user {userId}");
                }

                var totalTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogInformation($"[{requestId}] AddToCart completed in {totalTime}ms");

                return result.Success ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogError(ex, $"[{requestId}] AddToCart failed after {errorTime}ms");

                return StatusCode(500, new AddToCartResponseDTO
                {
                    Success = false,
                    Message = $"Lỗi server: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// OPTIMIZED: Update cart item with cache invalidation
        /// </summary>
        [HttpPut("update")]
        public async Task<ActionResult> UpdateCartItem([FromBody] UpdateCartDTO request)
        {
            var requestStart = DateTime.UtcNow;
            var requestId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new { Success = false, Message = "Vui lòng đăng nhập" });
                }

                var success = await _cartService.UpdateCartItemAsync(userId, request);

                // OPTIMIZATION: Invalidate cache after successful update
                if (success)
                {
                    var cacheKey = $"cart_{userId}";
                    _cache.Remove(cacheKey);
                }

                var totalTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogInformation($"[{requestId}] UpdateCartItem completed in {totalTime}ms");

                return success
                    ? Ok(new { Success = true, Message = "Cập nhật thành công" })
                    : NotFound(new { Success = false, Message = "Không tìm thấy item" });
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogError(ex, $"[{requestId}] UpdateCartItem failed after {errorTime}ms");

                return StatusCode(500, new { Success = false, Message = $"Lỗi server: {ex.Message}" });
            }
        }

        /// <summary>
        /// OPTIMIZED: Remove cart item with cache invalidation
        /// </summary>
        [HttpDelete("remove/{cartItemId}")]
        public async Task<ActionResult> RemoveFromCart(int cartItemId)
        {
            var requestStart = DateTime.UtcNow;
            var requestId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new { Success = false, Message = "Vui lòng đăng nhập" });
                }

                var success = await _cartService.RemoveFromCartAsync(userId, cartItemId);

                // OPTIMIZATION: Invalidate cache after successful removal
                if (success)
                {
                    var cacheKey = $"cart_{userId}";
                    _cache.Remove(cacheKey);
                }

                var totalTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogInformation($"[{requestId}] RemoveFromCart completed in {totalTime}ms");

                return success
                    ? Ok(new { Success = true, Message = "Đã xóa khỏi giỏ hàng" })
                    : NotFound(new { Success = false, Message = "Không tìm thấy item" });
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogError(ex, $"[{requestId}] RemoveFromCart failed after {errorTime}ms");

                return StatusCode(500, new { Success = false, Message = $"Lỗi server: {ex.Message}" });
            }
        }

        /// <summary>
        /// OPTIMIZED: Clear cart with cache invalidation
        /// </summary>
        [HttpDelete("clear")]
        public async Task<ActionResult> ClearCart()
        {
            var requestStart = DateTime.UtcNow;
            var requestId = Guid.NewGuid().ToString("N")[..8];

            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Unauthorized(new { Success = false, Message = "Vui lòng đăng nhập" });
                }

                var success = await _cartService.ClearCartAsync(userId);

                // OPTIMIZATION: Always clear cache for this user
                var cacheKey = $"cart_{userId}";
                _cache.Remove(cacheKey);

                var totalTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogInformation($"[{requestId}] ClearCart completed in {totalTime}ms");

                return success
                    ? Ok(new { Success = true, Message = "Đã xóa toàn bộ giỏ hàng" })
                    : BadRequest(new { Success = false, Message = "Lỗi khi xóa giỏ hàng" });
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogError(ex, $"[{requestId}] ClearCart failed after {errorTime}ms");

                return StatusCode(500, new { Success = false, Message = $"Lỗi server: {ex.Message}" });
            }
        }

        /// <summary>
        /// OPTIMIZED: Get cart count with caching
        /// </summary>
        [HttpGet("count")]
        [ResponseCache(Duration = 5, VaryByHeader = "Authorization")]
        public async Task<ActionResult> GetCartItemsCount()
        {
            var requestStart = DateTime.UtcNow;

            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Ok(new { Success = true, Count = 0 });
                }

                // OPTIMIZATION: Cache count separately (lighter than full cart)
                var countCacheKey = $"cart_count_{userId}";

                if (_cache.TryGetValue(countCacheKey, out int cachedCount))
                {
                    var cacheTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                    _logger.LogInformation($"Cart count served from cache in {cacheTime}ms");
                    return Ok(new { Success = true, Count = cachedCount });
                }

                var count = await _cartService.GetCartItemsCountAsync(userId);

                // Cache for 15 seconds
                _cache.Set(countCacheKey, count, TimeSpan.FromSeconds(15));

                var totalTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogInformation($"GetCartCount completed in {totalTime}ms");

                return Ok(new { Success = true, Count = count });
            }
            catch (Exception ex)
            {
                var errorTime = (DateTime.UtcNow - requestStart).TotalMilliseconds;
                _logger.LogError(ex, $"GetCartCount failed after {errorTime}ms");

                return StatusCode(500, new { Success = false, Message = $"Lỗi server: {ex.Message}" });
            }
        }

        /// <summary>
        /// Fast health check endpoint
        /// </summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public ActionResult HealthCheck()
        {
            return Ok(new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = "Optimized",
                CacheStats = new
                {
                    // Basic cache info (if available)
                    HasCache = _cache != null
                }
            });
        }

        /// <summary>
        /// Debug endpoint for performance monitoring
        /// </summary>
        [HttpGet("debug")]
        [Authorize(Roles = "Admin")]
        public ActionResult Debug()
        {
            var userId = GetCurrentUserId();
            var cacheKey = $"cart_{userId}";
            var countCacheKey = $"cart_count_{userId}";

            return Ok(new
            {
                UserId = userId,
                Cache = new
                {
                    HasCartCache = _cache.TryGetValue(cacheKey, out _),
                    HasCountCache = _cache.TryGetValue(countCacheKey, out _)
                },
                Performance = new
                {
                    Timestamp = DateTime.UtcNow,
                    ServerTime = DateTime.UtcNow.ToString("HH:mm:ss.fff")
                }
            });
        }

        /// <summary>
        /// OPTIMIZED: Get User ID from JWT token
        /// </summary>
        private int GetCurrentUserId()
        {
            try
            {
                // Try multiple claim types efficiently
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                ?? User.FindFirst("sub")?.Value
                                ?? User.FindFirst("user_id")?.Value;

                return int.TryParse(userIdClaim, out var userId) ? userId : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user ID from token");
                return 0;
            }
        }
    }
}