using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MenuFFS.Models;
using MenuFFS.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<AiOptions>()
    .Bind(builder.Configuration.GetSection(AiOptions.SectionName));

var configuredImageLimit = builder.Configuration.GetValue<int?>("Ai:MaxImageBytes")
    ?? 15 * 1024 * 1024;
var requestBodyLimit = Math.Max(configuredImageLimit + 1024 * 1024, 2 * 1024 * 1024);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = requestBodyLimit;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = requestBodyLimit;
    options.ValueLengthLimit = 60_000;
});

builder.Services.AddHttpClient("AiProvider", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
});
builder.Services.AddSingleton<MenuPromptBuilder>();
builder.Services.AddSingleton<IAiProviderClient, OllamaCloudClient>();
builder.Services.AddSingleton<IAiProviderClient, OpenAiClient>();
builder.Services.AddScoped<MenuTranslationService>();
builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("translations", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 1,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Prea multe cereri",
            Detail = "Ai trimis prea multe traduceri într-un timp scurt. Încearcă din nou peste un minut."
        }, cancellationToken: cancellationToken);
    };
});

var app = builder.Build();

app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' blob: data:; style-src 'self'; script-src 'self'; connect-src 'self'; manifest-src 'self'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
        context.Response.Headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=()";

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.Headers["Cache-Control"] = "no-store";
        }

        return Task.CompletedTask;
    });

    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    application = "MenuFFS",
    utc = DateTimeOffset.UtcNow
}));

app.MapGet("/api/config", (IOptions<AiOptions> options) =>
{
    var settings = options.Value;
    var providers = settings.Providers
        .Where(item => item.Value.Enabled)
        .Select(item => new PublicProvider(
            item.Key,
            string.IsNullOrWhiteSpace(item.Value.DisplayName) ? item.Key : item.Value.DisplayName,
            item.Value.Kind.ToString(),
            !string.IsNullOrWhiteSpace(item.Value.ApiKey),
            item.Value.Models
                .Where(model => !string.IsNullOrWhiteSpace(model.Id))
                .Select(model => new PublicModel(
                    model.Id,
                    string.IsNullOrWhiteSpace(model.DisplayName) ? model.Id : model.DisplayName,
                    model.SupportsVision))
                .ToArray()))
        .ToArray();

    return Results.Ok(new
    {
        appName = "MenuFFS",
        maxImageBytes = settings.MaxImageBytes,
        maxMenuTextCharacters = settings.MaxMenuTextCharacters,
        sourceLanguages = LanguageCatalog.SourceLanguages,
        targetLanguages = LanguageCatalog.TargetLanguages,
        providers
    });
});

app.MapPost("/api/translate", async (
    [FromForm] TranslationForm form,
    MenuTranslationService service,
    HttpContext httpContext) =>
{
    try
    {
        var result = await service.TranslateAsync(form, httpContext.RequestAborted);
        return Results.Ok(result);
    }
    catch (MenuValidationException exception)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Cerere invalidă",
            detail: exception.Message);
    }
    catch (ProviderConfigurationException exception)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Furnizor neconfigurat",
            detail: exception.Message);
    }
    catch (AiProviderException exception)
    {
        return Results.Problem(
            statusCode: StatusCodes.Status502BadGateway,
            title: "Eroare de la furnizorul AI",
            detail: exception.Message,
            extensions: exception.UpstreamStatusCode is null
                ? null
                : new Dictionary<string, object?>
                {
                    ["upstreamStatusCode"] = exception.UpstreamStatusCode
                });
    }
})
    .DisableAntiforgery()
    .RequireRateLimiting("translations");

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program
{
}
