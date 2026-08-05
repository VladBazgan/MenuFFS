using System.Text.Json;

namespace MenuFFS.Services;

internal static class ProviderResponseHelpers
{
    public static string ExtractError(string body, string fallback)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return Limit(error.GetString() ?? fallback);
                }

                if (error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out var message)
                    && message.ValueKind == JsonValueKind.String)
                {
                    return Limit(message.GetString() ?? fallback);
                }
            }
        }
        catch (JsonException)
        {
            // The provider returned a non-JSON error page. Use a short, safe excerpt.
        }

        return Limit(body);
    }

    private static string Limit(string value)
    {
        const int maxLength = 800;
        var normalized = value.ReplaceLineEndings(" ").Trim();
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}…";
    }
}
