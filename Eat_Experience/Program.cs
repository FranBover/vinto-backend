using Vinto.Api.Data;
using Vinto.Api.Repositories.Interfaces;
using Vinto.Api.Repositories.Implementaciones;
using Vinto.Api.Services.Interfaces;
using Vinto.Api.Services.Implementaciones;
using Vinto.Api.Hubs;
using Vinto.Api.Storage;
using Vinto.Api.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.FileProviders;
using System.Text;
using System.Text.Json;
using Vinto.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>();
var key = Encoding.UTF8.GetBytes(jwtSettings.Key);

// Add services to the container.

// Agregar autenticaci�n JWT
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            builder.Configuration["JwtSettings:Key"] ?? throw new InvalidOperationException("JWT key not found")))
    };

    // SignalR no puede enviar el header Authorization en el WebSocket, as� que para las rutas
    // del hub leemos el token del query string "access_token".
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});


builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IOptions<JwtSettings>>().Value);

// conexi�n a sqlserver
builder.Services.AddSingleton<IMenuPublicoCache, MenuPublicoCache>();
builder.Services.AddScoped<MenuCacheInvalidationInterceptor>();
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.CommandTimeout(180) // 3 minutos
    ).AddInterceptors(sp.GetRequiredService<MenuCacheInvalidationInterceptor>()));

// Repositorios

builder.Services.AddScoped<ICategoriaRepository, CategoriaRepository>();
builder.Services.AddScoped<IProductoRepository, ProductoRepository>();
builder.Services.AddScoped<IProductoExtraRepository, ProductoExtraRepository>();
builder.Services.AddScoped<IPedidoRepository, PedidoRepository>();
builder.Services.AddScoped<IDetallePedidoRepository, DetallePedidoRepository>();
builder.Services.AddScoped<IAdministradorRepository, AdministradorRepository>();
builder.Services.AddScoped<IDetallePedidoExtraRepository, DetallePedidoExtraRepository>();
builder.Services.AddScoped<ITipoVarianteRepository, TipoVarianteRepository>();
builder.Services.AddScoped<IOpcionVarianteRepository, OpcionVarianteRepository>();
builder.Services.AddScoped<IVarianteProductoRepository, VarianteProductoRepository>();
builder.Services.AddScoped<IStockRepository, StockRepository>();
builder.Services.AddScoped<IDescuentoRepository, DescuentoRepository>();
builder.Services.AddScoped<ICuponRepository, CuponRepository>();

// Servicios

builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IProductoService, ProductoService>();
builder.Services.AddScoped<IProductoExtraService, ProductoExtraService>();
builder.Services.AddScoped<IPedidoService, PedidoService>();
builder.Services.AddScoped<IDetallePedidoRepository, DetallePedidoRepository>();
builder.Services.AddScoped<IAdministradorService, AdministradorService>();
builder.Services.AddScoped<IDetallePedidoExtraService, DetallePedidoExtraService>();
builder.Services.AddScoped<ITipoVarianteService, TipoVarianteService>();
builder.Services.AddScoped<IOpcionVarianteService, OpcionVarianteService>();
builder.Services.AddScoped<IVarianteProductoService, VarianteProductoService>();
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IDescuentoService, DescuentoService>();
builder.Services.AddScoped<ICuponService, CuponService>();
builder.Services.AddScoped<IDescuentoCalculatorService, DescuentoCalculatorService>();
builder.Services.AddScoped<IMercadoPagoService, MercadoPagoService>();
builder.Services.AddScoped<IReporteService, ReporteService>();



// ForwardedHeaders: detras del front end de Azure App Service, RemoteIpAddress seria la IP interna del
// proxy. Leemos X-Forwarded-For / X-Forwarded-Proto para recuperar la IP real y el esquema original.
// App Service (Linux) ya setea ASPNETCORE_FORWARDEDHEADERS_ENABLED=true, que registra su propio
// middleware; esta Configure se registra despues y pisa sus opciones. KnownNetworks/KnownProxies se
// limpian porque la IP del proxy de App Service es privada y cambia (por defecto solo se confia en loopback).
// ForwardLimit = 1: se toma SOLO la entrada mas a la derecha del XFF (la que agrego el front end de
// Azure); los valores que inyecte el cliente quedan a la izquierda y se ignoran.
// Si se pone Front Door / CDN delante, subir ForwardLimit al numero de proxies de confianza.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

// Encryption
builder.Services.AddSingleton<IEncryptionHelper, EncryptionHelper>();
builder.Services.AddSingleton<IMercadoPagoSignatureValidator, MercadoPagoSignatureValidator>();

// Storage: elegimos el provider seg�n config. "AzureBlob" usa Azure Blob Storage;
// cualquier otro valor (o ausencia) cae en almacenamiento local (desarrollo).
var storageProvider = builder.Configuration["Storage:Provider"];
if (string.Equals(storageProvider, "AzureBlob", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IStorageProvider, AzureBlobStorageProvider>();
}
else
{
    builder.Services.AddScoped<IStorageProvider, LocalStorageProvider>();
}
builder.Services.AddScoped<IImagenService, ImagenService>();

builder.Services.AddSignalR();

// Controllers y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Ingrese el token JWT as�: Bearer {token}"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

    options.AddSecurityDefinition("X-Admin-Key", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "X-Admin-Key",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "ApiKeyScheme",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Clave de registro de admins (solo usar para POST /api/Auth/register)"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "X-Admin-Key"
                }
            },
            new string[] {}
        }
    });
});

// CORS


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(
                      "http://localhost:5173",
                      "https://vinto-frontend-dev-ffbbb4e2fzcfd5h9.centralus-01.azurewebsites.net",
                      "https://purple-dune-08cbe830f.7.azurestaticapps.net",
                      "https://vintoapp.com",
                      "https://www.vintoapp.com")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials(); // requerido por SignalR
        });
});



// RATE LIMITING (Microsoft.AspNetCore.RateLimiting, incluido en el framework).
// ATENCION: el estado de los contadores vive en MEMORIA de cada instancia. Si se escala
// horizontalmente (N instancias del App Service) el limite efectivo es N veces mayor y no se
// comparte entre instancias; habria que moverlo a un store distribuido (ej. Redis).
// No hay limite global: solo se limitan los endpoints con [EnableRateLimiting("nombre")].
// Particionado por IP real del cliente (requiere UseForwardedHeaders antes de UseRateLimiter).
static RateLimitPartition<string> PorIp(HttpContext ctx, int permitLimit, TimeSpan window) =>
    RateLimitPartition.GetSlidingWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "sin-ip",
        _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = window,
            SegmentsPerWindow = 5,
            QueueLimit = 0,
            AutoReplenishment = true
        });

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("login", ctx => PorIp(ctx, 5, TimeSpan.FromMinutes(5)));
    options.AddPolicy("register", ctx => PorIp(ctx, 3, TimeSpan.FromMinutes(15)));
    options.AddPolicy("pedidoPublico", ctx => PorIp(ctx, 10, TimeSpan.FromMinutes(10)));
    options.AddPolicy("consultaPublica", ctx => PorIp(ctx, 30, TimeSpan.FromMinutes(1)));

    options.OnRejected = async (context, cancellationToken) =>
    {
        var http = context.HttpContext;
        var policy = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        http.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("RateLimiter")
            .LogWarning("Rate limit excedido: politica {Policy}, IP {Ip}, {Method} {Path}",
                policy, http.Connection.RemoteIpAddress, http.Request.Method, http.Request.Path);

        // Mensaje generico a proposito: sin Retry-After ni intentos restantes.
        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        http.Response.ContentType = "application/json; charset=utf-8";
        await http.Response.WriteAsync(
            JsonSerializer.Serialize(new { error = "Demasiados intentos, probá de nuevo en unos minutos" }),
            cancellationToken);
    };
});

var app = builder.Build();

// ForwardedHeaders tiene que ir PRIMERO: antes del manejo de excepciones (para que los logs vean la IP real),
// de UseHttpsRedirection (necesita X-Forwarded-Proto o entra en loop de redirects), de CORS y de auth.
// Si el host ya lo registro (ASPNETCORE_FORWARDEDHEADERS_ENABLED=true) NO lo aplicamos de nuevo: una segunda
// pasada consumiria la siguiente entrada del XFF, que es la que puede falsificar el cliente.
var forwardedHeadersEnabledByHost =
    string.Equals(app.Configuration["ASPNETCORE_FORWARDEDHEADERS_ENABLED"], "true", StringComparison.OrdinalIgnoreCase)
    || string.Equals(app.Configuration["FORWARDEDHEADERS_ENABLED"], "true", StringComparison.OrdinalIgnoreCase);
if (!forwardedHeadersEnabledByHost)
{
    app.UseForwardedHeaders();
}

// Middleware GLOBAL de manejo de excepciones.
// Va lo m�s arriba posible del pipeline para envolver TODO lo dem�s en un try/catch
// y as� ninguna excepci�n escape con cuerpo vac�o (lo que adem�s rompe CORS en el navegador).
var globalExceptionLogger = app.Services
    .GetRequiredService<ILoggerFactory>()
    .CreateLogger("GlobalExceptionHandler");

var env = app.Environment;

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        // Logueamos la excepci�n COMPLETA (incluye stack trace e InnerException via ToString()).
        globalExceptionLogger.LogError(ex, "Excepci�n no controlada en {Method} {Path}: {Excepcion}",
            context.Request.Method, context.Request.Path, ex.ToString());

        // Si la respuesta ya empez� a enviarse no podemos reescribir headers/cuerpo: relanzamos.
        if (context.Response.HasStarted)
        {
            throw;
        }

        // IMPORTANTE (CORS): no llamamos a Response.Clear() para no borrar los headers que
        // UseCors ya aplic� aguas abajo (Access-Control-Allow-Origin, etc.). Como este middleware
        // est� por ENCIMA de UseCors en el pipeline, dichos headers ya est�n en la respuesta cuando
        // la excepci�n vuelve hasta ac�, y al no limpiarlos el navegador puede leer este 500.
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json; charset=utf-8";

        // El detalle ("detalle" y "tipo") ya est� gateado a entornos NO productivos: en producci�n
        // solo exponemos un mensaje gen�rico para no filtrar detalles internos; fuera de producci�n
        // incluimos error/detalle/tipo para facilitar el diagn�stico (la excepci�n completa siempre
        // queda en el log del servidor v�a el LogError de arriba).
        var payload = env.IsProduction()
            ? JsonSerializer.Serialize(new
            {
                error = "Error interno del servidor"
            })
            : JsonSerializer.Serialize(new
            {
                error = ex.Message,
                detalle = ex.InnerException?.Message,
                tipo = ex.GetType().Name
            });

        await context.Response.WriteAsync(payload);
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});


app.UseCors("AllowFrontend");

// Despues de UseForwardedHeaders (IP real) y de UseCors (el 429 lleva headers CORS);
// antes de auth y de MapControllers.
app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapHub<PedidosHub>("/hubs/pedidos");

app.MapControllers();


// Seed de administrador de prueba: solo en Development. En prod/staging crear�a un admin
// con credenciales p�blicas conocidas, as� que lo gateamos al entorno de desarrollo.
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!context.Administradores.Any())
        {
            var passwordHasher = new PasswordHasher<Administrador>();
            var admin = new Administrador
            {
                Nombre = "Admin Prueba",
                Email = "admin@ejemplo.com",
                SlugLocal = "admin-prueba",
                Telefono = "3511234567",
                Direccion = "C�rdoba, Argentina"
            };

            admin.PasswordHash = passwordHasher.HashPassword(null, "123456");

            context.Administradores.Add(admin);
            context.SaveChanges();
        }
    }
}






app.Run();
