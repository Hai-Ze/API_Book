using API_Book.Models;
using API_Book.Repositories;
using API_Book.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Cấu hình DbContext với timeout tối ưu
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(15);
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

// Thêm phần này vào Program.cs sau phần builder configuration

// QUAN TRỌNG: Đăng ký HTTP Context Accessor để lấy user info
builder.Services.AddHttpContextAccessor();

// Cấu hình CORS chi tiết và mở rộng
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors", policy =>
    {
        policy.WithOrigins(
                "http://127.0.0.1:5500",
                "http://localhost:5500",
                "http://localhost:3000",
                "http://localhost:8080",
                "http://localhost:5173",
                "http://localhost:4200",
                "https://localhost:7000",
                "https://localhost:7288",
                "file://" // Cho phép local file access
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Content-Range", "X-Content-Range", "Authorization");
    });

    // Policy cho phép tất cả origins (chỉ dùng cho development)
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) // Cho phép mọi origin
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Đăng ký Repositories
builder.Services.AddScoped<IBookRepository, BookRepository>();

// Đăng ký Services
builder.Services.AddScoped<IGoogleAuthService, GoogleAuthService>();
builder.Services.AddScoped<ICartService, CartService>();

// Cấu hình JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-super-secret-jwt-key-here-minimum-32-characters-long-for-security";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // Cho development, đổi thành true cho production
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        RequireExpirationTime = true
    };

    // Log JWT errors
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"JWT Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("JWT Token validated successfully");
            return Task.CompletedTask;
        }
    };
});

// Cấu hình Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole("Customer"));
    options.AddPolicy("RequireAuth", policy => policy.RequireAuthenticatedUser());
});

// Cấu hình Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
    });

// Cấu hình CORS chi tiết và mở rộng
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors", policy =>
    {
        policy.WithOrigins(
                "http://127.0.0.1:5500",
                "http://localhost:5500",
                "http://localhost:3000",
                "http://localhost:8080",
                "http://localhost:5173", // Vite
                "http://localhost:4200", // Angular
                "https://localhost:7000",
                "https://localhost:7288"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Content-Range", "X-Content-Range");
    });

    options.AddPolicy("ProductionCors", policy =>
    {
        policy.WithOrigins("https://yourdomain.com") // Thay bằng domain thực
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });

    // Policy rộng cho testing
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Cấu hình Swagger với JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BookStore API với Google Auth",
        Version = "v1",
        Description = "API quản lý sách với xác thực Google OAuth",
        Contact = new OpenApiContact
        {
            Name = "BookStore Team",
            Email = "support@bookstore.com"
        }
    });

    // Thêm JWT Authentication cho Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header sử dụng Bearer scheme. Ví dụ: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT"
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

// Cấu hình Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// Cấu hình Memory Cache
builder.Services.AddMemoryCache();

// Cấu hình Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BookStore API V1");
        c.RoutePrefix = string.Empty;
        c.DocumentTitle = "BookStore API Documentation";
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.ShowExtensions();
    });

    // Sử dụng CORS rộng cho development
    app.UseCors("DevelopmentCors");
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseCors("ProductionCors");
}

// Middleware Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    
    if (app.Environment.IsDevelopment())
    {
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
    }
    
    await next();
});

// Handle preflight requests
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 200;
        await context.Response.WriteAsync("");
        return;
    }
    await next();
});

app.UseResponseCompression();
app.UseHttpsRedirection();

// Middleware order cực kỳ quan trọng
app.UseRouting();

// CORS phải được đặt trước Authentication
app.UseCors();

// Authentication và Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Health check endpoint
app.MapHealthChecks("/health");

// Endpoint mặc định với thông tin về Authentication
app.MapGet("/", () => new
{
    Message = "BookStore API với Google Authentication đang chạy!",
    Documentation = "/swagger",
    Status = "Healthy",
    Timestamp = DateTime.UtcNow,
    Authentication = new
    {
        GoogleLogin = "/api/auth/google-login",
        VerifyToken = "/api/auth/verify",
        CurrentUser = "/api/auth/me",
        AdminTest = "/api/auth/admin-test",
        Health = "/api/auth/health"
    },
    BookEndpoints = new
    {
        Books = "/api/BookApi/paged",
        Search = "/api/BookApi/search?q=query",
        TopRated = "/api/BookApi/top-rated",
        QuickStats = "/api/BookApi/quick-stats"
    },
    CartEndpoints = new
    {
        AddToCart = "/api/cart/add",
        GetCart = "/api/cart",
        UpdateCart = "/api/cart/update",
        RemoveItem = "/api/cart/remove/{id}",
        ClearCart = "/api/cart/clear",
        GetCount = "/api/cart/count"
    },
    Note = "Sử dụng Google OAuth để xác thực. Email admin được cấu hình trong GoogleAuthService."
});

// Middleware để log slow queries và requests
app.Use(async (context, next) =>
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception in request pipeline");
        
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new
        {
            Success = false,
            Message = "Internal server error",
            RequestId = context.TraceIdentifier
        });
    }
    finally
    {
        stopwatch.Stop();
        
        if (stopwatch.ElapsedMilliseconds > 5000) // Log slow requests
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning($"Slow request: {context.Request.Method} {context.Request.Path} took {stopwatch.ElapsedMilliseconds}ms");
        }
    }
});

// Khởi tạo database nếu cần
try
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    // Đảm bảo database được tạo
    await context.Database.EnsureCreatedAsync();
    
    Console.WriteLine("Database connection successful");
}
catch (Exception ex)
{
    Console.WriteLine($"Database connection failed: {ex.Message}");
}

Console.WriteLine($"🚀 BookStore API đang chạy tại: {builder.Configuration["ApplicationUrl"] ?? "https://localhost:7288"}");
Console.WriteLine($"📖 Swagger UI: {builder.Configuration["ApplicationUrl"] ?? "https://localhost:7288"}/swagger");
Console.WriteLine($"🔐 Google Client ID: 198205931206-445vmgejdn1s12d5lr9kqc3jj8o1el3u.apps.googleusercontent.com");

app.Run();