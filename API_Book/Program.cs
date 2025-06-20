using API_Book.Models;
using API_Book.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
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
        Description = "API for managing books in bookstore",
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
            .AllowCredentials(); // Cho phép credentials nếu cần
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

// Endpoint mặc định
app.MapGet("/", () => "BookStore API is running! Go to /swagger for documentation.");

app.Run();