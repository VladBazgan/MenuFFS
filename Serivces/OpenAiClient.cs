using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MenuFFS.Models;

namespace MenuFFS.Services;

public sealed class OpenAiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<OpenAiClient> logger) : IAiProviderClient
{
    public AiProviderKind Kind => AiProviderKind.OpenAI;

    public async Task<string> TranslateAsync(
        AiTranslationRequest request,
        AiProviderOptions provider,
        CancellationToken cancellationToken)
    {
        var content = new List<object>
        {
            new { type = "input_text", text = request.UserPrompt }
        };

        if (!string.IsNullOrWhiteSpace(request.ImageBase64)
            && !string.IsNullOrWhiteSpace(request.ImageMimeType))
        {
            content.Add(new
            {
                type = "input_image",
                image_url = $"data:{request.ImageMimeType};base64,{request.ImageBase64}",
                detail = "high"
            });
        }

        var payload = new
        {
            model = request.Model,
            instructions = request.SystemPrompt,
            input = new[]
            {
                new
                {
                    role = "user",
                    content
                }
            },
            max_output_tokens = request.MaxOutputTokens,
            store = false
        };

        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var endpoint = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/responses"
            : $"{baseUrl}/v1/responses";

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

        try
        {
            var client = httpClientFactory.CreateClient("AiProvider");
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            if (!response.IsSuccessStatusCode)
            {
                var detail = ProviderResponseHelpers.ExtractError(body, "OpenAI a respins cererea.");
                throw new AiProviderException($"OpenAI: {detail}", (int)response.StatusCode);
            }

            using var document = JsonDocument.Parse(body);
            var translatedText = ExtractOutputText(document.RootElement);

            if (string.IsNullOrWhiteSpace(translatedText))
            {
                throw new AiProviderException("OpenAI a răspuns fără conținut tradus.");
            }

            return translatedText;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException($"OpenAI nu a răspuns în {request.TimeoutSeconds} de secunde.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "OpenAI request failed for model {Model}", request.Model);
            throw new AiProviderException("Conexiunea cu OpenAI nu a putut fi realizată.");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "OpenAI returned invalid JSON for model {Model}", request.Model);
            throw new AiProviderException("OpenAI a returnat un răspuns care nu a putut fi interpretat.");
        }
    }

    private static string ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var directOutput)
            && directOutput.ValueKind == JsonValueKind.String)
        {
            return directOutput.GetString() ?? string.Empty;
        }

        if (!root.TryGetProperty("output", out var output)
            || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var parts = new List<string>();

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var itemContent)
                || itemContent.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var part in itemContent.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var type)
                    && type.GetString() == "output_text"
                    && part.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    parts.Add(text.GetString() ?? string.Empty);
                }
            }
        }

        return string.Join("\n", parts);
    }
}
