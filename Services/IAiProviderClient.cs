using MenuFFS.Models;

namespace MenuFFS.Services;

public interface IAiProviderClient
{
    AiProviderKind Kind { get; }

    Task<string> TranslateAsync(
        AiTranslationRequest request,
        AiProviderOptions provider,
        CancellationToken cancellationToken);
}
