using API_Book.Models;
using API_Book.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình DbContext với timeout tối ưu
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        // Cấu hình timeout cho connection
        npgsqlOptions.CommandTimeout(10); // 10 giây thay vì 30 giây mặc định
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });

    // Tắt tracking để tăng performance
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    // Cấu hình logging chi tiết hơn trong development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});

// Đăng ký Repository
builder.Services.AddScoped<IBookRepository, BookRepository>();

// Cấu hình Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null; // Giữ nguyên tên property
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Cấu hình Swagger chi tiết
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BookStore API",
        Version = "v1",
        Description = "API for managing books in bookstore (Optimized for 30k+ records)",
        Contact = new OpenApiContact
        {
            Name = "BookStore Team",
            Email = "support@bookstore.com"
        }
    });

    // Cấu hình để Swagger hiển thị example values
    c.EnableAnnotations();
});

// Cấu hình CORS chi tiết
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

    // Policy cho development - cho phép tất cả
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

// Cấu hình Memory Cache để cache kết quả
builder.Services.AddMemoryCache();

var app = builder.Build();

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BookStore API V1");
        c.RoutePrefix = string.Empty; // Swagger UI ở root path
        c.DocumentTitle = "BookStore API Documentation";
        c.DisplayRequestDuration();
    });

    // Sử dụng CORS "AllowAll" trong development
    app.UseCors("AllowAll");
}
else
{
    // Production - sử dụng CORS hạn chế
    app.UseCors("AllowSpecificOrigins");
}

app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Endpoint mặc định với thông tin performance
app.MapGet("/", () => new
{
    Message = "BookStore API is running!",
    Documentation = "/swagger",
    Performance = new
    {
        MaxRecords = 1000,
        DefaultPageSize = 20,
        MaxPageSize = 50,
        Note = "Optimized for large datasets (30k+ books)"
    },
    Endpoints = new
    {
        Books = "/api/BookApi/paged",
        Search = "/api/BookApi/search?q=query",
        TopRated = "/api/BookApi/top-rated",
        QuickStats = "/api/BookApi/quick-stats"
    }
});

// Thêm middleware để log slow queries
app.Use(async (context, next) =>
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    await next();
    stopwatch.Stop();

    if (stopwatch.ElapsedMilliseconds > 5000) // Log requests > 5 seconds
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogWarning($"Slow request: {context.Request.Path} took {stopwatch.ElapsedMilliseconds}ms");
    }
});

app.Run();