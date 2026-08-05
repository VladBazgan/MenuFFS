using Microsoft.AspNetCore.Http;

namespace MenuFFS.Models;

public sealed class TranslationForm
{
    public string ProviderId { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string SourceLanguage { get; set; } = "auto";

    public string TargetLanguage { get; set; } = "ro";

    public string? MenuText { get; set; }

    public IFormFile? Image { get; set; }
}

public sealed record TranslationResult(
    string Markdown,
    string ProviderId,
    string ProviderName,
    string Model,
    string InputMode,
    long DurationMilliseconds);

public sealed record AiTranslationRequest(
    string Model,
    string SystemPrompt,
    string UserPrompt,
    string? ImageBase64,
    string? ImageMimeType,
    int MaxOutputTokens,
    int TimeoutSeconds);

public sealed record LanguageOption(string Code, string Name);

public sealed record PublicModel(string Id, string Name, bool SupportsVision);

public sealed record PublicProvider(
    string Id,
    string Name,
    string Kind,
    bool Configured,
    IReadOnlyCollection<PublicModel> Models);
