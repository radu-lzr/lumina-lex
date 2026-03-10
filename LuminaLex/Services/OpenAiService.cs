using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LuminaLex.Services;

public sealed class OpenAiService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private const string Endpoint = "https://api.openai.com/v1/responses";

    public async Task<string> CorrectAsync(string text, string apiKey, CancellationToken ct = default)
    {
        const string systemPrompt =
            "Correcteur orthographique et grammatical. Corrige uniquement les erreurs sans modifier le style ni le sens. Retourne uniquement le texte corrigé, sans aucun commentaire.";

        var payload = new
        {
            model = "gpt-5-nano",
            instructions = systemPrompt,
            input = text,
            reasoning = new { effort = "minimal" },
            store = false,
            max_output_tokens = 2048
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {apiKey}");

        using var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = ExtractErrorMessage(body) ?? $"HTTP {(int)response.StatusCode}";
            throw new ApiException($"OpenAI: {errorMsg}");
        }

        return ExtractResponsesText(body)
            ?? throw new ApiException("OpenAI: réponse vide ou format inattendu.");
    }

    private static string? ExtractResponsesText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("output", out var output))
            return null;

        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content))
                continue;

            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) &&
                    type.GetString() == "output_text" &&
                    block.TryGetProperty("text", out var text))
                {
                    return text.GetString();
                }
            }
        }

        return null;
    }

    private static string? ExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var msg))
                return msg.GetString();
            if (doc.RootElement.TryGetProperty("message", out var topMsg))
                return topMsg.GetString();
        }
        catch { }
        return null;
    }
}

public sealed class ApiException : Exception
{
    public ApiException(string message) : base(message) { }
}
