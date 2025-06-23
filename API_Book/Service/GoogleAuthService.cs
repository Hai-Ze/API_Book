using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API_Book.Models;
using API_Book.Models.DTOs;

namespace API_Book.Services
{
    public interface IGoogleAuthService
    {
        Task<AuthResponseDTO> AuthenticateGoogleUserAsync(string googleToken);
        Task<GoogleUserInfo?> VerifyGoogleTokenAsync(string googleToken);
        Task<User> CreateOrUpdateUserAsync(GoogleUserInfo googleUser);
        string GenerateJwtToken(User user);
    }

    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleAuthService> _logger;

        // DANH SÁCH EMAIL ADMIN - THAY ĐỔI THEO NHU CẦU
        private readonly List<string> AdminEmails = new()
        {
            "taodalat123@gmail.com",
            "hoanghaizs73@gmail.com",
            // Thêm email admin khác ở đây
        };

        public GoogleAuthService(ApplicationDbContext context, IConfiguration configuration, ILogger<GoogleAuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AuthResponseDTO> AuthenticateGoogleUserAsync(string googleToken)
        {
            try
            {
                _logger.LogInformation("Starting Google authentication process");

                // 1. Xác minh Google Token
                var googleUser = await VerifyGoogleTokenAsync(googleToken);
                if (googleUser == null)
                {
                    _logger.LogWarning("Google token verification failed");
                    return new AuthResponseDTO
                    {
                        Success = false,
                        Message = "Google token không hợp lệ hoặc đã hết hạn"
                    };
                }

                _logger.LogInformation($"Google user verified: {googleUser.Email}");

                // 2. Kiểm tra email verified
                if (!googleUser.Email_Verified)
                {
                    _logger.LogWarning($"Email not verified for user: {googleUser.Email}");
                    return new AuthResponseDTO
                    {
                        Success = false,
                        Message = "Email Google chưa được xác minh"
                    };
                }

                // 3. Tạo hoặc cập nhật user
                var user = await CreateOrUpdateUserAsync(googleUser);
                _logger.LogInformation($"User created/updated: {user.Email} with role: {user.Role}");

                // 4. Tạo JWT token
                var jwtToken = GenerateJwtToken(user);

                // 5. Trả về response
                return new AuthResponseDTO
                {
                    Success = true,
                    Message = "Đăng nhập thành công",
                    Token = jwtToken,
                    User = new UserInfoDTO
                    {
                        Id = user.Id,
                        Email = user.Email,
                        FullName = user.FullName,
                        AvatarUrl = user.AvatarUrl,
                        Role = user.Role,
                        LastLogin = user.LastLogin
                    },
                    IsNewUser = (DateTime.UtcNow - user.CreatedAt).TotalMinutes < 1
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication failed for Google token");
                return new AuthResponseDTO
                {
                    Success = false,
                    Message = $"Lỗi xác thực: {ex.Message}"
                };
            }
        }

        public async Task<GoogleUserInfo?> VerifyGoogleTokenAsync(string googleToken)
        {
            try
            {
                _logger.LogInformation("Verifying Google token");

                // Xác minh token với Google
                var payload = await GoogleJsonWebSignature.ValidateAsync(googleToken);

                // Kiểm tra thông tin cơ bản
                if (string.IsNullOrEmpty(payload.Email) || string.IsNullOrEmpty(payload.Subject))
                {
                    _logger.LogWarning("Invalid Google token payload - missing email or subject");
                    return null;
                }

                var googleUser = new GoogleUserInfo
                {
                    Id = payload.Subject,
                    Email = payload.Email,
                    Name = payload.Name ?? payload.Email, // Fallback to email if name is null
                    Picture = payload.Picture ?? "",
                    Email_Verified = payload.EmailVerified
                };

                _logger.LogInformation($"Google token verified successfully for: {googleUser.Email}");
                return googleUser;
            }
            catch (InvalidJwtException ex)
            {
                _logger.LogWarning($"Invalid JWT token: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Google token");
                return null;
            }
        }

        public async Task<User> CreateOrUpdateUserAsync(GoogleUserInfo googleUser)
        {
            try
            {
                // Tìm user hiện tại theo GoogleId HOẶC Email
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.GoogleId == googleUser.Id || u.Email.ToLower() == googleUser.Email.ToLower());

                if (existingUser == null)
                {
                    // Kiểm tra xem có user nào với email này chưa (case-insensitive)
                    var emailExists = await _context.Users
                        .AnyAsync(u => u.Email.ToLower() == googleUser.Email.ToLower());

                    if (emailExists)
                    {
                        // Nếu email đã tồn tại, update user đó với Google ID
                        existingUser = await _context.Users
                            .FirstAsync(u => u.Email.ToLower() == googleUser.Email.ToLower());

                        existingUser.GoogleId = googleUser.Id;
                        existingUser.FullName = googleUser.Name;
                        existingUser.AvatarUrl = googleUser.Picture;
                        existingUser.LastLogin = DateTime.UtcNow;
                        existingUser.IsActive = true;

                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Updated existing email user: {existingUser.Email}");
                        return existingUser;
                    }

                    // Tạo user mới
                    var role = DetermineUserRole(googleUser.Email);

                    var newUser = new User
                    {
                        Email = googleUser.Email.ToLower(), // Lowercase email
                        GoogleId = googleUser.Id,
                        FullName = googleUser.Name ?? googleUser.Email,
                        AvatarUrl = googleUser.Picture ?? "",
                        Role = role,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        LastLogin = DateTime.UtcNow
                    };

                    _context.Users.Add(newUser);

                    try
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Created new user: {newUser.Email} with role: {role}");
                        return newUser;
                    }
                    catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                    {
                        _logger.LogError(dbEx, $"Database error creating user: {googleUser.Email}");

                        // Nếu lỗi unique constraint, thử tìm user đã tồn tại
                        var conflictUser = await _context.Users
                            .FirstOrDefaultAsync(u => u.Email.ToLower() == googleUser.Email.ToLower() || u.GoogleId == googleUser.Id);

                        if (conflictUser != null)
                        {
                            // Update user hiện tại
                            conflictUser.GoogleId = googleUser.Id;
                            conflictUser.FullName = googleUser.Name ?? conflictUser.FullName;
                            conflictUser.AvatarUrl = googleUser.Picture ?? conflictUser.AvatarUrl;
                            conflictUser.LastLogin = DateTime.UtcNow;
                            conflictUser.IsActive = true;

                            await _context.SaveChangesAsync();
                            _logger.LogInformation($"Resolved conflict for user: {conflictUser.Email}");
                            return conflictUser;
                        }

                        throw; // Re-throw nếu không resolve được
                    }
                }
                else
                {
                    // Cập nhật user hiện tại
                    existingUser.LastLogin = DateTime.UtcNow;
                    existingUser.IsActive = true;

                    // Cập nhật thông tin nếu có
                    if (!string.IsNullOrEmpty(googleUser.Picture))
                    {
                        existingUser.AvatarUrl = googleUser.Picture;
                    }

                    if (!string.IsNullOrEmpty(googleUser.Name))
                    {
                        existingUser.FullName = googleUser.Name;
                    }

                    // Cập nhật GoogleId nếu chưa có
                    if (string.IsNullOrEmpty(existingUser.GoogleId))
                    {
                        existingUser.GoogleId = googleUser.Id;
                    }

                    // Đảm bảo email consistency
                    if (existingUser.Email.ToLower() != googleUser.Email.ToLower())
                    {
                        existingUser.Email = googleUser.Email.ToLower();
                    }

                    try
                    {
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"Updated existing user: {existingUser.Email}");
                        return existingUser;
                    }
                    catch (Microsoft.EntityFrameworkCore.DbUpdateException dbEx)
                    {
                        _logger.LogError(dbEx, $"Database error updating user: {existingUser.Email}");

                        // Refresh entity và thử lại
                        await _context.Entry(existingUser).ReloadAsync();
                        existingUser.LastLogin = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        return existingUser;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating/updating user for email: {googleUser.Email}");
                throw new Exception($"Không thể tạo hoặc cập nhật user: {ex.Message}", ex);
            }
        }

        public string GenerateJwtToken(User user)
        {
            try
            {
                var jwtKey = _configuration["Jwt:Key"] ?? "your-super-secret-jwt-key-here-minimum-32-characters-long-for-security";
                var key = Encoding.ASCII.GetBytes(jwtKey);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim("sub", user.Id.ToString()), // Thêm sub claim cho CartController
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("email", user.Email), // Duplicate cho compatibility
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim("name", user.FullName), // Duplicate cho compatibility
                    new Claim(ClaimTypes.Role, user.Role),
                    new Claim("role", user.Role), // Duplicate cho compatibility
                    new Claim("avatar", user.AvatarUrl ?? ""),
                    new Claim("google_id", user.GoogleId),
                    new Claim("user_id", user.Id.ToString()) // Thêm user_id claim
                };

                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddDays(7), // Token hết hạn sau 7 ngày
                    SigningCredentials = new SigningCredentials(
                        new SymmetricSecurityKey(key),
                        SecurityAlgorithms.HmacSha256Signature)
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var tokenString = tokenHandler.WriteToken(token);

                _logger.LogInformation($"JWT token generated for user: {user.Email}");
                return tokenString;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating JWT token for user: {user.Email}");
                throw;
            }
        }

        private string DetermineUserRole(string email)
        {
            // Kiểm tra danh sách Admin
            if (AdminEmails.Any(adminEmail => string.Equals(adminEmail, email, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation($"Email {email} is in admin list");
                return "Admin";
            }

            // Kiểm tra nếu là user đầu tiên trong hệ thống
            var userCount = _context.Users.Count();
            if (userCount == 0)
            {
                _logger.LogInformation($"First user in system: {email} - assigning Admin role");
                return "Admin";
            }

            _logger.LogInformation($"Email {email} assigned Customer role");
            return "Customer";
        }
    }
}