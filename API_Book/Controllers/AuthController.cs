using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using API_Book.Models.DTOs;
using API_Book.Services;

namespace API_Book.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IGoogleAuthService _googleAuthService;

        public AuthController(IGoogleAuthService googleAuthService)
        {
            _googleAuthService = googleAuthService;
        }

        /// <summary>
        /// Google Login - Đăng nhập bằng Google
        /// </summary>
        [HttpPost("google-login")]
        public async Task<ActionResult<AuthResponseDTO>> GoogleLogin([FromBody] GoogleLoginDTO request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.GoogleToken))
                {
                    return BadRequest(new AuthResponseDTO
                    {
                        Success = false,
                        Message = "Google token is required"
                    });
                }

                var result = await _googleAuthService.AuthenticateGoogleUserAsync(request.GoogleToken);

                if (result.Success)
                {
                    return Ok(result);
                }
                else
                {
                    return Unauthorized(result);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new AuthResponseDTO
                {
                    Success = false,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Verify Token - Kiểm tra JWT token có hợp lệ không
        /// </summary>
        [HttpGet("verify")]
        [Authorize]
        public ActionResult<object> VerifyToken()
        {
            try
            {
                var user = new
                {
                    Id = User.FindFirst("sub")?.Value,
                    Email = User.FindFirst("email")?.Value,
                    Name = User.FindFirst("name")?.Value,
                    Role = User.FindFirst("role")?.Value,
                    Avatar = User.FindFirst("avatar")?.Value
                };

                return Ok(new
                {
                    Success = true,
                    Message = "Token is valid",
                    User = user
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Token verification failed: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Logout - Đăng xuất (client-side xóa token)
        /// </summary>
        [HttpPost("logout")]
        public ActionResult Logout()
        {
            return Ok(new
            {
                Success = true,
                Message = "Logged out successfully"
            });
        }

        /// <summary>
        /// Get Current User - Lấy thông tin user hiện tại
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public ActionResult GetCurrentUser()
        {
            try
            {
                var user = new
                {
                    Id = int.Parse(User.FindFirst("sub")?.Value ?? "0"),
                    Email = User.FindFirst("email")?.Value,
                    FullName = User.FindFirst("name")?.Value,
                    Role = User.FindFirst("role")?.Value,
                    AvatarUrl = User.FindFirst("avatar")?.Value,
                    GoogleId = User.FindFirst("google_id")?.Value
                };

                return Ok(new
                {
                    Success = true,
                    User = user
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    Success = false,
                    Message = $"Error getting user info: {ex.Message}"
                });
            }
        }

        /// <summary>
        /// Admin Only - Test endpoint chỉ admin truy cập được
        /// </summary>
        [HttpGet("admin-test")]
        [Authorize(Roles = "Admin")]
        public ActionResult AdminTest()
        {
            return Ok(new
            {
                Success = true,
                Message = "Hello Admin! This endpoint is protected.",
                User = User.FindFirst("name")?.Value
            });
        }


    }
}