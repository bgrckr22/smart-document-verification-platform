using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using SmartDocumentPlatform.Api.Data;
using SmartDocumentPlatform.Api.Middleware;
using SmartDocumentPlatform.Api.Options;
using SmartDocumentPlatform.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
}
var maximumUploadBytes = builder.Configuration.GetValue<long>(
    $"{UploadOptions.SectionName}:MaxBytes",
    10 * 1024 * 1024);
if (maximumUploadBytes is < 1024 or > 50 * 1024 * 1024)
{
    throw new InvalidOperationException("Uploads:MaxBytes must be between 1 KB and 50 MB.");
}
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.Configure<FormOptions>(options =>
    options.MultipartBodyLengthLimit = maximumUploadBytes + (1024 * 1024));
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = maximumUploadBytes + (1024 * 1024));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Smart Document Verification API",
        Version = "v1",
        Description = "Upload document images, run OpenCV analysis, and manage processing history."
    });
});

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
    connectionString,
    npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(5),
        errorCodesToAdd: null)));
builder.Services.AddOptions<ComputerVisionOptions>()
    .Bind(builder.Configuration.GetSection(ComputerVisionOptions.SectionName))
    .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https", "BaseUrl must be an HTTP or HTTPS URL.")
    .Validate(options => options.TimeoutSeconds is > 0 and <= 300, "TimeoutSeconds must be between 1 and 300.")
    .ValidateOnStart();
builder.Services.AddOptions<FileStorageOptions>()
    .Bind(builder.Configuration.GetSection(FileStorageOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.RootPath), "RootPath is required.")
    .ValidateOnStart();
builder.Services.AddOptions<UploadOptions>()
    .Bind(builder.Configuration.GetSection(UploadOptions.SectionName))
    .Validate(options => options.MaxBytes is >= 1024 and <= 50 * 1024 * 1024, "MaxBytes must be between 1 KB and 50 MB.")
    .ValidateOnStart();

builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<DocumentFileValidator>();
builder.Services.AddSingleton<IDocumentStorage, LocalDocumentStorage>();
builder.Services.AddHttpClient<IComputerVisionClient, ComputerVisionClient>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many document uploads",
                Detail = "Wait for an active upload to finish, then try again.",
                Instance = context.HttpContext.Request.Path,
                Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    };
    options.AddConcurrencyLimiter("Uploads", limiter =>
    {
        limiter.PermitLimit = 2;
        limiter.QueueLimit = 4;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (allowedOrigins.Length > 0)
    {
        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
}));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (dbContext.Database.IsRelational())
    {
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
}

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    await next();
});
app.UseRouting();
app.UseCors("Frontend");
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();

if (builder.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "healthy", service = "backend" }))
    .WithTags("system");
app.MapGet("/health/ready", async (
    AppDbContext dbContext,
    IComputerVisionClient computerVisionClient,
    CancellationToken cancellationToken) =>
{
    try
    {
        var databaseConnected = await dbContext.Database.CanConnectAsync(cancellationToken);
        var computerVisionConnected = await computerVisionClient.IsHealthyAsync(cancellationToken);
        return databaseConnected && computerVisionConnected
            ? Results.Ok(new { status = "ready", database = "connected", computerVision = "connected" })
            : Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "A required service is unavailable",
                extensions: new Dictionary<string, object?>
                {
                    ["database"] = databaseConnected ? "connected" : "unavailable",
                    ["computerVision"] = computerVisionConnected ? "connected" : "unavailable"
                });
    }
    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception exception)
    {
        app.Logger.LogWarning(exception, "Readiness check failed");
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "A required service is unavailable");
    }
}).WithTags("system");
app.MapControllers();

await app.RunAsync();

public partial class Program;
