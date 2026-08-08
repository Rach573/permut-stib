using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using PermutStib.Api;
using PermutStib.Business.Services;
using PermutStib.Business.Abstractions;
using PermutStib.Business.Models;
using PermutStib.Data.Entities;
using PermutStib.Data.Persistence;
using PermutStib.Data.Repositories;
using PermutStib.Data.Utils;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("authentication", limiter =>
    {
        limiter.PermitLimit = 20;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, ".data-protection-keys")));

var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");
if (string.IsNullOrWhiteSpace(postgresConnectionString))
    throw new InvalidOperationException("ConnectionStrings:Postgres is required.");

builder.Services.AddDbContext<PermutStibDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

builder.Services
    .AddIdentityCore<AgentUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<PermutStibDbContext>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "permutstib.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var users = context.HttpContext.RequestServices.GetRequiredService<UserManager<AgentUser>>();
            var user = Guid.TryParse(userId, out var parsedId) ? await users.FindByIdAsync(parsedId.ToString()) : null;
            if (user is null || user.Status != AgentStatus.Active)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("app_role", "Admin")));

builder.Services.AddScoped<IAccountGateway, IdentityAccountGateway>();
builder.Services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<IPermutationGateway, PermutationGateway>();
builder.Services.AddScoped<PermutationService>();
builder.Services.AddScoped<ISignatureGateway, SignatureGateway>();
builder.Services.AddScoped<SignatureService>();
builder.Services.AddScoped<INotificationGateway, NotificationGateway>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<IAdminGateway, AdminGateway>();
builder.Services.AddScoped<AdminService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<PermutStibDbContext>();
    await database.Database.EnsureCreatedAsync();
    await DatabaseSchemaInitializer.ApplyAdditiveUpdatesAsync(database);
}

await AdminBootstrapper.SeedAsync(app.Services, app.Configuration);
await DemoDataSeeder.SeedAsync(app.Services, app.Configuration);

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    context.Response.ContentType = "application/json";
    context.Response.StatusCode = exception switch
    {
        ArgumentException => StatusCodes.Status400BadRequest,
        BusinessRuleException => StatusCodes.Status409Conflict,
        KeyNotFoundException => StatusCodes.Status404NotFound,
        UnauthorizedAccessException => StatusCodes.Status403Forbidden,
        DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError
    };
    var message = context.Response.StatusCode == 500 ? "Une erreur interne est survenue." : exception?.Message;
    await context.Response.WriteAsJsonAsync(new { error = message });
}));

app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    var mutatingApiRequest = context.Request.Path.StartsWithSegments("/api") &&
        HttpMethods.IsPost(context.Request.Method);
    if (mutatingApiRequest && context.Request.Headers["X-Permut-STIB"] != "app")
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "Requête applicative invalide." });
        return;
    }
    await next();
});
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/api/status", () => Results.Ok(new { application = "Permut STIB API", status = "running" }));
app.MapGet("/healthz", async (PermutStibDbContext database, CancellationToken token) =>
    await database.Database.CanConnectAsync(token)
        ? Results.Ok(new { status = "healthy" })
        : Results.Problem("Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable));
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
