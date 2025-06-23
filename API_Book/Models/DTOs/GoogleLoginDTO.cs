using System.ComponentModel.DataAnnotations;

namespace API_Book.Models.DTOs
{
    // DTO cho Google Login Request
    public class GoogleLoginDTO
    {
        [Required]
        public string GoogleToken { get; set; } = string.Empty;
    }

    // DTO cho Google User Info (từ Google API)
    public class GoogleUserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Picture { get; set; } = string.Empty;
        public bool Email_Verified { get; set; }
    }

    // DTO cho Auth Response
    public class AuthResponseDTO
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Token { get; set; }
        public UserInfoDTO? User { get; set; }
        public bool IsNewUser { get; set; } = false;
    }

    // DTO cho User Info
    public class UserInfoDTO
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string? AvatarUrl { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime LastLogin { get; set; }
    }
}