using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MenuFFS.Models;

namespace MenuFFS.Services;

public sealed class OllamaCloudClient(
    IHttpClientFactory httpClientFactory,
    ILogger<OllamaCloudClient> logger) : IAiProviderClient
{
    public AiProviderKind Kind => AiProviderKind.Ollama;

    public async Task<string> TranslateAsync(
        AiTranslationRequest request,
        AiProviderOptions provider,
        CancellationToken cancellationToken)
    {
        var userMessage = new Dictionary<string, object?>
        {
            ["role"] = "user",
            ["content"] = request.UserPrompt
        };

        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            userMessage["images"] = new[] { request.ImageBase64 };
        }

        var payload = new
        {
            model = request.Model,
            messages = new object[]
            {
                new { role = "system", content = request.SystemPrompt },
                userMessage
            },
            stream = false,
            options = new
            {
                temperature = 0.1,
                num_predict = request.MaxOutputTokens
            }
        };

        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var endpoint = baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/chat"
            : $"{baseUrl}/api/chat";

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
                var detail = ProviderResponseHelpers.ExtractError(body, "Ollama Cloud a respins cererea.");
                throw new AiProviderException(
                    $"Ollama Cloud: {detail}",
                    (int)response.StatusCode);
            }

            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("message", out var responseMessage)
                && responseMessage.TryGetProperty("content", out var content)
                && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString() ?? string.Empty;
            }

            throw new AiProviderException("Ollama Cloud a răspuns fără conținut tradus.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException($"Ollama Cloud nu a răspuns în {request.TimeoutSeconds} de secunde.");
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Ollama Cloud request failed for model {Model}", request.Model);
            throw new AiProviderException("Conexiunea cu Ollama Cloud nu a putut fi realizată.");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Ollama Cloud returned invalid JSON for model {Model}", request.Model);
            throw new AiProviderException("Ollama Cloud a returnat un răspuns care nu a putut fi interpretat.");
        }
    }
}
