using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CountOrSell.Core.Data;
using CountOrSell.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

// Data paths: environment variables take priority (Docker / Azure / production),
// then fall back to the repo-root heuristic used in local development.
static string? TryFindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            return dir.FullName;
        dir = dir.Parent;
    }
    return null;
}

var repoRoot   = TryFindRepoRoot();
var dbPath     = Environment.GetEnvironmentVariable("COS_DATABASE_PATH")
    ?? Path.Combine(repoRoot ?? AppContext.BaseDirectory, "src", "CountOrSell.Api", "database", "CountOrSell.db");
var imagesRoot = Environment.GetEnvironmentVariable("COS_IMAGES_PATH")
    ?? Path.Combine(repoRoot ?? AppContext.BaseDirectory, "src", "CountOrSell.Api", "images");
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
Directory.CreateDirectory(imagesRoot);

// SQLite database
builder.Services.AddDbContext<CountOrSellDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// HttpClient for update checks
builder.Services.AddHttpClient();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<ICardDataService>(sp =>
    new CardDataService(sp.GetRequiredService<CountOrSellDbContext>(), imagesRoot));
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<ISlabbedCardService, SlabbedCardService>();
builder.Services.AddSingleton<ILabelService, LabelService>();

var app = builder.Build();

// Ensure database is created and schema is up to date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CountOrSellDbContext>();
    db.Database.EnsureCreated();
    db.EnsureSchemaUpToDate();

    // Seed default admin user if it doesn't exist
    if (!db.Users.Any(u => u.Username == "cosadm"))
    {
        db.Users.Add(new CountOrSell.Core.Entities.User
        {
            Username = "cosadm",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("wholeftjaceinchargeofdesign"),
            DisplayName = "Admin",
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }
}


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Serve the React SPA from wwwroot in production (Docker / Azure builds).
// In development the Vite dev server handles the frontend separately.
if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// SPA fallback: unmatched routes return index.html so React Router works.
if (!app.Environment.IsDevelopment())
    app.MapFallbackToFile("index.html");

app.Run();
