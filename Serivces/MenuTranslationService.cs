using System.Diagnostics;
using Microsoft.Extensions.Options;
using MenuFFS.Models;

namespace MenuFFS.Services;

public sealed class MenuTranslationService(
    IOptions<AiOptions> options,
    IEnumerable<IAiProviderClient> clients,
    MenuPromptBuilder promptBuilder)
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private readonly AiOptions _options = options.Value;
    private readonly IReadOnlyCollection<IAiProviderClient> _clients = clients.ToArray();

    public async Task<TranslationResult> TranslateAsync(
        TranslationForm form,
        CancellationToken cancellationToken)
    {
        ValidateBasicInput(form);

        if (!_options.Providers.TryGetValue(form.ProviderId, out var provider) || !provider.Enabled)
        {
            throw new MenuValidationException("Furnizorul AI selectat nu este disponibil.");
        }

        if (string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            throw new ProviderConfigurationException(
                $"Cheia API pentru {provider.DisplayName} nu este configurată pe server.");
        }

        var model = provider.Models.FirstOrDefault(item =>
            string.Equals(item.Id, form.Model, StringComparison.OrdinalIgnoreCase));

        if (model is null)
        {
            throw new MenuValidationException(
                "Modelul selectat nu aparține furnizorului ales. Reîncarcă pagina și încearcă din nou.");
        }

        var hasImage = form.Image is { Length: > 0 };
        if (hasImage && !model.SupportsVision)
        {
            throw new MenuValidationException(
                $"Modelul {model.DisplayName} nu este configurat cu suport pentru imagini.");
        }

        var image = hasImage
            ? await ReadImageAsync(form.Image!, cancellationToken)
            : null;

        var sourceLanguage = LanguageCatalog.ResolveSource(form.SourceLanguage);
        var targetLanguage = LanguageCatalog.ResolveTarget(form.TargetLanguage);

        var request = new AiTranslationRequest(
            model.Id,
            promptBuilder.BuildSystemPrompt(sourceLanguage, targetLanguage),
            promptBuilder.BuildUserPrompt(hasImage, form.MenuText),
            image?.Base64,
            image?.MimeType,
            _options.MaxOutputTokens,
            _options.TimeoutSeconds);

        var client = _clients.FirstOrDefault(item => item.Kind == provider.Kind)
            ?? throw new ProviderConfigurationException(
                $"Integrarea pentru {provider.DisplayName} nu este disponibilă.");

        var stopwatch = Stopwatch.StartNew();
        var rawMarkdown = await client.TranslateAsync(request, provider, cancellationToken);
        stopwatch.Stop();

        var markdown = NormalizeMarkdown(rawMarkdown);
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new AiProviderException("Modelul selectat a returnat un rezultat gol.");
        }

        return new TranslationResult(
            markdown,
            form.ProviderId,
            provider.DisplayName,
            model.Id,
            hasImage ? "image" : "text",
            stopwatch.ElapsedMilliseconds);
    }

    private void ValidateBasicInput(TranslationForm form)
    {
        if (string.IsNullOrWhiteSpace(form.ProviderId))
        {
            throw new MenuValidationException("Selectează furnizorul AI.");
        }

        if (string.IsNullOrWhiteSpace(form.Model))
        {
            throw new MenuValidationException("Selectează modelul AI.");
        }

        var hasText = !string.IsNullOrWhiteSpace(form.MenuText);
        var hasImage = form.Image is { Length: > 0 };

        if (!hasText && !hasImage)
        {
            throw new MenuValidationException("Încarcă o imagine sau introdu textul meniului.");
        }

        if (form.MenuText?.Length > _options.MaxMenuTextCharacters)
        {
            throw new MenuValidationException(
                $"Textul depășește limita de {_options.MaxMenuTextCharacters:N0} de caractere.");
        }

        if (form.Image?.Length > _options.MaxImageBytes)
        {
            throw new MenuValidationException(
                $"Imaginea depășește limita de {_options.MaxImageBytes / 1024 / 1024} MB.");
        }
    }

    private async Task<ImagePayload> ReadImageAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var input = file.OpenReadStream();
        await using var buffer = new MemoryStream((int)file.Length);
        await input.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var mimeType = DetectImageType(bytes)
            ?? throw new MenuValidationException(
                "Formatul imaginii nu este acceptat. Folosește JPEG, PNG sau WebP.");

        return new ImagePayload(Convert.ToBase64String(bytes), mimeType);
    }

    private static string? DetectImageType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= PngSignature.Length && bytes[..PngSignature.Length].SequenceEqual(PngSignature))
        {
            return "image/png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }

    private static string NormalizeMarkdown(string value)
    {
        var markdown = value.Trim();

        if (markdown.StartsWith("```markdown", StringComparison.OrdinalIgnoreCase))
        {
            markdown = markdown[11..].TrimStart();
        }
        else if (markdown.StartsWith("```md", StringComparison.OrdinalIgnoreCase))
        {
            markdown = markdown[5..].TrimStart();
        }

        if (markdown.EndsWith("```", StringComparison.Ordinal))
        {
            markdown = markdown[..^3].TrimEnd();
        }

        return markdown;
    }

    private sealed record ImagePayload(string Base64, string MimeType);
}
