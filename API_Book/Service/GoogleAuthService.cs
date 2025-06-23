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

        // DANH SÁCH EMAIL ADMIN - THAY ĐỔI THEO NHU CẦU
        private readonly List<string> AdminEmails = new()
        {
            "taodalat123@gmail.com",
            "hoanghaizs73@gmail.com",        // ← THAY BẰNG EMAIL GOOGLE CỦA BẠN
        };

        public GoogleAuthService(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<AuthResponseDTO> AuthenticateGoogleUserAsync(string googleToken)
        {
            try
            {
                // 1. Xác minh Google Token
                var googleUser = await VerifyGoogleTokenAsync(googleToken);
                if (googleUser == null)
                {
                    return new AuthResponseDTO
                    {
                        Success = false,
                        Message = "Invalid Google token"
                    };
                }

                // 2. Tạo hoặc cập nhật user
                var user = await CreateOrUpdateUserAsync(googleUser);

                // 3. Tạo JWT token
                var jwtToken = GenerateJwtToken(user);

                // 4. Trả về response
                return new AuthResponseDTO
                {
                    Success = true,
                    Message = "Login successful",
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
                    IsNewUser = user.CreatedAt == user.LastLogin
                };
            }
            catch (Exception ex)
            {
                return new AuthResponseDTO
                {
                    Success = false,
                    Message = $"Authentication failed: {ex.Message}"
                };
            }
        }

        public async Task<GoogleUserInfo?> VerifyGoogleTokenAsync(string googleToken)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(googleToken);

                return new GoogleUserInfo
                {
                    Id = payload.Subject,
                    Email = payload.Email,
                    Name = payload.Name,
                    Picture = payload.Picture,
                    Email_Verified = payload.EmailVerified
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Google token verification failed: {ex.Message}");
                return null;
            }
        }

        public async Task<User> CreateOrUpdateUserAsync(GoogleUserInfo googleUser)
        {
            // Tìm user hiện tại
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.GoogleId == googleUser.Id);

            if (existingUser == null)
            {
                // Tạo user mới
                var role = DetermineUserRole(googleUser.Email);

                var newUser = new User
                {
                    Email = googleUser.Email,
                    GoogleId = googleUser.Id,
                    FullName = googleUser.Name,
                    AvatarUrl = googleUser.Picture,
                    Role = role,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                Console.WriteLine($"Created new user: {newUser.Email} with role: {role}");
                return newUser;
            }
            else
            {
                // Cập nhật user hiện tại
                existingUser.LastLogin = DateTime.UtcNow;
                existingUser.AvatarUrl = googleUser.Picture; // Cập nhật ảnh mới
                existingUser.FullName = googleUser.Name;     // Cập nhật tên mới

                await _context.SaveChangesAsync();

                Console.WriteLine($"Updated existing user: {existingUser.Email}");
                return existingUser;
            }
        }

        public string GenerateJwtToken(User user)
        {
            var jwtKey = _configuration["Jwt:Key"] ?? "your-super-secret-jwt-key-here-min-32-chars";
            var key = Encoding.ASCII.GetBytes(jwtKey);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("avatar", user.AvatarUrl ?? ""),
                new Claim("google_id", user.GoogleId)
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
            return tokenHandler.WriteToken(token);
        }

        private string DetermineUserRole(string email)
        {
            // Check danh sách Admin
            if (AdminEmails.Contains(email.ToLower()))
            {
                return "Admin";
            }

            // User đầu tiên là Admin (tùy chọn)
            var userCount = _context.Users.Count();
            if (userCount == 0)
            {
                return "Admin";
            }

            return "Customer";
        }
    }
}