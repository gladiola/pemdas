using pemdas.Middleware;
using pemdas.Services;
using Serilog;
using Serilog.Events;

// Configure Serilog before the host is built so startup errors are also captured.
// Logs rotate hourly and are retained for 30 days (720 files × 1 h = 30 days).
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine("logs", "pemdas-.log"),
        rollingInterval: RollingInterval.Hour,
        retainedFileCountLimit: 720,          // 30 days × 24 h/day
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        shared: false,
        flushToDiskInterval: TimeSpan.FromSeconds(5))
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Replace the default logging with Serilog.
builder.Host.UseSerilog((ctx, services, loggerConfig) =>
    loggerConfig
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: Path.Combine("logs", "pemdas-.log"),
            rollingInterval: RollingInterval.Hour,
            retainedFileCountLimit: 720,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
            shared: false,
            flushToDiskInterval: TimeSpan.FromSeconds(5)));

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<PemdasSolver>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseHttpsRedirection();

// Security headers and nonce/CSP must run before static files and routing
// so that every response — including 4xx short-circuits — carries them.
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<NonceMiddleware>();

// Log each request with its method, path, status code, and duration.
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms | TraceId={TraceId}";
    options.GetLevel = (ctx, elapsed, ex) =>
        ex != null || ctx.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : LogEventLevel.Information;
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
    };
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
