using System.Security.Claims;
using System.Text;
using Arabica.Api.Guvenlik;
using Arabica.Api.RealTime;
using Arabica.Application.Denetim;
using Arabica.Application.Kimlik;
using Arabica.Application.Kurulum;
using Arabica.Application.Mesajlasma;
using Arabica.Infrastructure.Kimlik;
using Arabica.Infrastructure.Kurulum;
using Arabica.Infrastructure.Veri;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption;
using Microsoft.AspNetCore.DataProtection.AuthenticatedEncryption.ConfigurationModel;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Arabica Cafe API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "JWT'yi şu şekilde girin: Bearer {token}"
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalHataYakalayici>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IDenetimBaglami, HttpDenetimBaglami>();

// Onion composition root: wire Application (CQRS/MediatR/patterns) + Infrastructure (EF/Kafka/ESB/identity).
builder.Services.ArabicaApplicationEkle();
builder.Services.ArabicaInfrastructureEkle(builder.Configuration);
if (builder.Configuration.GetValue("ArkaPlanServisleri:Etkin", true))
    builder.Services.ArabicaArkaPlanServisleriEkle();

// Real-time push: SignalR ADAPTER overrides the logging notifier fallback registered by Infrastructure.
builder.Services.AddSingleton<IDashboardNotifier, SignalRDashboardNotifier>();

// AES-256 secret protection (Data Protection): keys persisted to a protected volume; secrets come from env.
var anahtarDizini = builder.Configuration["DataProtection:KeyPath"];
if (string.IsNullOrWhiteSpace(anahtarDizini))
    anahtarDizini = Path.Combine(builder.Environment.ContentRootPath, "dp-keys");
Directory.CreateDirectory(anahtarDizini);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(anahtarDizini))
    .UseCryptographicAlgorithms(new AuthenticatedEncryptorConfiguration
    {
        EncryptionAlgorithm = EncryptionAlgorithm.AES_256_CBC,
        ValidationAlgorithm = ValidationAlgorithm.HMACSHA256
    });

var jwt = builder.Configuration.GetSection(JwtSecenekleri.Bolum).Get<JwtSecenekleri>()
          ?? throw new InvalidOperationException("Jwt yapılandırması eksik.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Yayinci,
            ValidateAudience = true,
            ValidAudience = jwt.Kitle,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Imza)),
            ValidateLifetime = true,
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
        // SignalR (WebSocket) can't send Authorization headers — accept the JWT via ?access_token on /hubs.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy(Politikalar.Koordinator, p => p.RequireRole(Roller.BolgeKoordinatoru));
    o.AddPolicy(Politikalar.Yonetici, p => p.RequireRole(Roller.BolgeKoordinatoru, Roller.SubeMuduru));
});

builder.Services.AddHealthChecks().AddDbContextCheck<HistoryDbContext>("postgres");

var app = builder.Build();

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<DolulukHub>("/hubs/doluluk");
app.MapHealthChecks("/health");

app.Run();

// WebApplicationFactory<Program> erişimi için.
public partial class Program;
