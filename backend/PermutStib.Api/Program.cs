using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PermutStib.Api;
using PermutStib.Business.Services;
using PermutStib.Data.Entities;
using PermutStib.Data.Persistence;
using PermutStib.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<PermutStibDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services
    .AddIdentityCore<AgentUser>(options =>
    {
        options.Password.RequiredLength = 10;
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
    });

builder.Services.AddAuthorization(options =>
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("app_role", "Admin")));

builder.Services.AddScoped<IAccountGateway, IdentityAccountGateway>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<IPermutationGateway, PermutationGateway>();
builder.Services.AddScoped<PermutationService>();
builder.Services.AddScoped<ISignatureGateway, SignatureGateway>();
builder.Services.AddScoped<SignatureService>();
builder.Services.AddScoped<INotificationGateway, NotificationGateway>();
builder.Services.AddScoped<NotificationService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<PermutStibDbContext>();
    await database.Database.EnsureCreatedAsync();
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

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
