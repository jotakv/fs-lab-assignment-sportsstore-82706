using Serilog;
using Serilog.Context;
using Serilog.Events;
using Microsoft.EntityFrameworkCore;
using SportsStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// 1) Serilog configuration (code-based, like your example)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    // suppress noisy framework logs
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console()
    .WriteTo.File("./Logs/log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true)
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();

// (Optional) a few startup test logs (remove later if you want)
Log.Information("Application {App} starting", "SportsStore");
Log.Information("Environment {Environment}", builder.Environment.EnvironmentName);

// 2) Services (your existing code)
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<StoreDbContext>(opts =>
{
    opts.UseSqlServer(builder.Configuration["ConnectionStrings:SportsStoreConnection"]);
});

builder.Services.AddScoped<IStoreRepository, EFStoreRepository>();
builder.Services.AddScoped<IOrderRepository, EFOrderRepository>();

builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddScoped<Cart>(sp => SessionCart.GetCart(sp));
builder.Services.AddScoped<SportsStore.Services.Payments.IPaymentService, SportsStore.Services.Payments.StripePaymentService>();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddServerSideBlazor();

builder.Services.AddDbContext<AppIdentityDbContext>(options =>
    options.UseSqlServer(builder.Configuration["ConnectionStrings:IdentityConnection"]));

builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<AppIdentityDbContext>();

var app = builder.Build();

// 3) Startup log (required)
Log.Information("Application startup. Environment={Environment}", app.Environment.EnvironmentName);

// 4) CorrelationId + (optional) UserName context for traceability
app.Use(async (context, next) =>
{
    var correlationId =
        context.Request.Headers.TryGetValue("X-Correlation-ID", out var cid) && !string.IsNullOrWhiteSpace(cid)
            ? cid.ToString()
            : context.TraceIdentifier;

    context.Response.Headers["X-Correlation-ID"] = correlationId;

    var userName = context.User?.Identity?.IsAuthenticated == true
        ? context.User.Identity!.Name
        : "anonymous";

    using (LogContext.PushProperty("CorrelationId", correlationId))
    using (LogContext.PushProperty("UserName", userName))
    {
        await next();
    }
});

// 5) HTTP request logging (observability)
app.UseSerilogRequestLogging();

// 6) Exception logging (keep ONE handler, not two)
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerPathFeature>();
        Log.Error(feature?.Error, "Unhandled exception. Path={Path}", context.Request.Path);
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("An error occurred.");
    });
});

// 7) Your existing pipeline
app.UseRequestLocalization(opts =>
{
    opts.AddSupportedCultures("en-US")
        .AddSupportedUICultures("en-US")
        .SetDefaultCulture("en-US");
});

app.UseStaticFiles();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("catpage",
    "{category}/Page{productPage:int}",
    new { Controller = "Home", action = "Index" });

app.MapControllerRoute("page", "Page{productPage:int}",
    new { Controller = "Home", action = "Index", productPage = 1 });

app.MapControllerRoute("category", "{category}",
    new { Controller = "Home", action = "Index", productPage = 1 });

app.MapControllerRoute("pagination",
    "Products/Page{productPage}",
    new { Controller = "Home", action = "Index", productPage = 1 });

app.MapDefaultControllerRoute();
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/admin/{*catchall}", "/Admin/Index");

// 8) Seeding
SeedData.EnsurePopulated(app);
IdentitySeedData.EnsurePopulated(app);

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}