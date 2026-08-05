namespace MenuFFS.Models;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public int MaxImageBytes { get; set; } = 15 * 1024 * 1024;

    public int MaxMenuTextCharacters { get; set; } = 50_000;

    public int MaxOutputTokens { get; set; } = 8_000;

    public int TimeoutSeconds { get; set; } = 180;

    public Dictionary<string, AiProviderOptions> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AiProviderOptions
{
    public string DisplayName { get; set; } = string.Empty;

    public AiProviderKind Kind { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public List<AiModelOptions> Models { get; set; } = [];
}

public sealed class AiModelOptions
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool SupportsVision { get; set; }
}

public enum AiProviderKind
{
    Ollama,
    OpenAI
}
