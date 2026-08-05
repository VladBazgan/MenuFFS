namespace MenuFFS.Models;

public sealed class MenuValidationException(string message) : Exception(message)
{
}

public sealed class ProviderConfigurationException(string message) : Exception(message)
{
}

public sealed class AiProviderException(string message, int? upstreamStatusCode = null) : Exception(message)
{
    public int? UpstreamStatusCode { get; } = upstreamStatusCode;
}
