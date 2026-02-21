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

// SQLite database
var dbPath = Path.Combine(AppContext.BaseDirectory, "CountOrSell.db");
builder.Services.AddDbContext<CountOrSellDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// HttpClient for update checks
builder.Services.AddHttpClient();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<ICardDataService, CardDataService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<ISlabbedCardService, SlabbedCardService>();
builder.Services.AddSingleton<ILabelService, LabelService>();

var app = builder.Build();

// Ensure database is created and schema is up to date
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CountOrSellDbContext>();
    db.Database.EnsureCreated();
    db.EnsureSchemaUpToDate();
}

// Create images directory
Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "images"));

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
