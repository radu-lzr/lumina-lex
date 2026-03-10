using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LuminaLex.Services;

public sealed class DeepLService
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public async Task<string> TranslateAsync(string text, string apiKey, CancellationToken ct = default)
    {
        var endpoint = apiKey.Contains(":fx", StringComparison.Ordinal)
            ? "https://api-free.deepl.com/v2/translate"
            : "https://api.deepl.com/v2/translate";

        var payload = new
        {
            text = new[] { text },
            target_lang = "EN-US",
            model_type = "latency_optimized"
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"DeepL-Auth-Key {apiKey}");

        using var response = await Http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = ExtractErrorMessage(body) ?? $"HTTP {(int)response.StatusCode}";
            throw new ApiException($"DeepL: {errorMsg}");
        }

        return ExtractTranslation(body)
            ?? throw new ApiException("DeepL: réponse vide ou format inattendu.");
    }

    private static string? ExtractTranslation(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("translations", out var translations))
            {
                foreach (var item in translations.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var text))
                        return text.GetString();
                }
            }
        }
        catch { }
        return null;
    }

    private static string? ExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString();
        }
        catch { }
        return null;
    }
}
