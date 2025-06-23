using API_Book.Models;
using API_Book.Repositories;
using API_Book.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình DbContext với timeout tối ưu
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(10);
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });

    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});

// Đăng ký Repositories
builder.Services.AddScoped<IBookRepository, BookRepository>();

// Đăng ký Services (MỚI)
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<ICartService, CartService>(); 



// Cấu hình JWT Authentication (MỚI)
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-super-secret-jwt-key-here-minimum-32-characters-long";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Cho development
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Cấu hình Authorization (MỚI)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer"));
});

// Cấu hình Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Cấu hình Swagger với JWT support (CẬP NHẬT)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BookStore API with Google Auth",
        Version = "v1",
        Description = "API for managing books with Google OAuth authentication",
        Contact = new OpenApiContact
        {
            Name = "BookStore Team",
            Email = "support@bookstore.com"
        }
    });

    // Thêm JWT Authentication cho Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    c.EnableAnnotations();
});

// Cấu hình CORS chi tiết (CẬP NHẬT)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(
                "http://127.0.0.1:5500",
                "http://localhost:5500",
                "http://localhost:3000",
                "http://localhost:8080",
                "https://localhost:7000"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Cấu hình Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Cấu hình Memory Cache
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BookStore API V1");
        c.RoutePrefix = string.Empty;
        c.DocumentTitle = "BookStore API Documentation";
        c.DisplayRequestDuration();
    });

    app.UseCors("AllowAll");
}
else
{
    app.UseCors("AllowSpecificOrigins");
}

app.UseResponseCompression();
app.UseHttpsRedirection();

// Thêm Authentication & Authorization middleware (MỚI)
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Endpoint mặc định với thông tin về Authentication (CẬP NHẬT)
app.MapGet("/", () => new
{
    Message = "BookStore API with Google Authentication is running!",
    Documentation = "/swagger",
    Authentication = new
    {
        GoogleLogin = "/api/auth/google-login",
        VerifyToken = "/api/auth/verify",
        CurrentUser = "/api/auth/me",
        AdminTest = "/api/auth/admin-test"
    },
    BookEndpoints = new
    {
        Books = "/api/BookApi/paged",
        Search = "/api/BookApi/search?q=query",
        TopRated = "/api/BookApi/top-rated",
        QuickStats = "/api/BookApi/quick-stats"
    },
    Note = "Use Google OAuth for authentication. Admin emails are configured in GoogleAuthService."
});

// Middleware để log slow queries
app.Use(async (context, next) =>
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await next();
    stopwatch.Stop();

    if (stopwatch.ElapsedMilliseconds > 5000)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning($"Slow request: {context.Request.Path} took {stopwatch.ElapsedMilliseconds}ms");
    }
});

app.Run();