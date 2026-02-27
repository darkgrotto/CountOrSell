using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using CountOrSell.Api.Services;
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

// HttpClient for update checks and SPH proxying
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("sph");

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICollectionService, CollectionService>();
builder.Services.AddScoped<ICardDataService>(sp =>
    new CardDataService(sp.GetRequiredService<CountOrSellDbContext>(), imagesRoot));
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IImportService, ImportService>();
builder.Services.AddScoped<ISlabbedCardService, SlabbedCardService>();
builder.Services.AddSingleton<ILabelService, LabelService>();

// Demo mode: register the background reset service
if (string.Equals(Environment.GetEnvironmentVariable("COS_DEMO_MODE"), "true", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddHostedService<DemoResetService>();

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

// ── SPH reverse proxy ─────────────────────────────────────────────────────
// When SPH_API_URL is configured, all requests to /sph/* are forwarded to
// the SealedProdHelper service. This lets the frontend iframe SPH at /sph/
// without CORS issues, sharing the same origin and JWT cookies.
var sphApiUrl = Environment.GetEnvironmentVariable("SPH_API_URL");
if (!string.IsNullOrEmpty(sphApiUrl))
{
    var sphBase = sphApiUrl.TrimEnd('/');
    app.Map("/sph", sphApp =>
    {
        sphApp.Run(async ctx =>
        {
            var httpClientFactory = ctx.RequestServices.GetRequiredService<IHttpClientFactory>();
            using var client = httpClientFactory.CreateClient("sph");
            client.Timeout = TimeSpan.FromSeconds(60);

            // Build the target URL: strip the /sph prefix from the incoming path
            var remainingPath = ctx.Request.Path.Value ?? "/";
            var query = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : string.Empty;
            var targetUrl = $"{sphBase}{remainingPath}{query}";

            // Build the outgoing request
            using var reqMsg = new HttpRequestMessage
            {
                Method = new HttpMethod(ctx.Request.Method),
                RequestUri = new Uri(targetUrl),
            };

            // Forward body (for POST/PUT/PATCH)
            if (ctx.Request.ContentLength > 0 || ctx.Request.Headers.ContainsKey("Transfer-Encoding"))
            {
                reqMsg.Content = new StreamContent(ctx.Request.Body);
                if (ctx.Request.ContentType != null)
                    reqMsg.Content.Headers.ContentType =
                        System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ctx.Request.ContentType);
            }

            // Forward select request headers (including Authorization so the SPH JWT is validated)
            foreach (var header in ctx.Request.Headers)
            {
                if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)) continue;
                if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
                if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
                try { reqMsg.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value); }
                catch { /* ignore malformed headers */ }
            }

            HttpResponseMessage respMsg;
            try
            {
                respMsg = await client.SendAsync(reqMsg, HttpCompletionOption.ResponseHeadersRead);
            }
            catch (Exception ex)
            {
                ctx.Response.StatusCode = 502;
                await ctx.Response.WriteAsync($"SPH proxy error: {ex.Message}");
                return;
            }

            ctx.Response.StatusCode = (int)respMsg.StatusCode;
            foreach (var header in respMsg.Headers)
                ctx.Response.Headers.TryAppend(header.Key, header.Value.ToArray());
            foreach (var header in respMsg.Content.Headers)
                ctx.Response.Headers.TryAppend(header.Key, header.Value.ToArray());

            // Remove transfer-encoding chunked — ASP.NET handles chunking itself
            ctx.Response.Headers.Remove("transfer-encoding");

            await respMsg.Content.CopyToAsync(ctx.Response.Body);
        });
    });
}

// SPA fallback: unmatched routes return index.html so React Router works.
if (!app.Environment.IsDevelopment())
    app.MapFallbackToFile("index.html");

app.Run();
