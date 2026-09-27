using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Azure.Core;
using Azure.Identity;
using PictureExifclone.Models;

namespace PictureExifclone.Services;

public interface IAiMetadataProvider
{
    string Name { get; }
    /// <summary>One structured request. Providers have no file system access; they only return validated JSON text.</summary>
    Task<AiReply> CompleteAsync(string system, string user, byte[]? jpeg, string schemaName, JsonObject schema, int maxOutput, CancellationToken token);
}

public sealed class AiProviderException(string message, AiUsage usage, bool ambiguous = false, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>Tokens the provider may already have billed.</summary>
    public AiUsage Usage { get; } = usage;
    /// <summary>The request may have been processed; retrying could be billed twice.</summary>
    public bool Ambiguous { get; } = ambiguous;
}

public static class AiProviderFactory
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };
    private static readonly TokenCredential AzureCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions { ExcludeInteractiveBrowserCredential = false });

    static AiProviderFactory() => Http.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);

    public static IAiMetadataProvider Create(AiProviderProfile profile, int maxAttempts)
    {
        switch (profile.Provider)
        {
            case "openAI":
            {
                string key = Secret(profile, "OPENAI_API_KEY");
                return new OpenAiResponsesProvider(profile.Id, Http, new Uri("https://api.openai.com/v1/responses"), profile.Model,
                    _ => ValueTask.FromResult(new AuthenticationHeaderValue("Bearer", key)), null, profile.ReasoningEffort, maxAttempts);
            }
            case "azureOpenAI":
            {
                string endpoint = Environment.GetEnvironmentVariable(profile.EndpointVariable!) ?? throw new InvalidOperationException($"Umgebungsvariable {profile.EndpointVariable} (Azure-Endpoint) fehlt.");
                string deployment = Environment.GetEnvironmentVariable(profile.DeploymentVariable!) ?? throw new InvalidOperationException($"Umgebungsvariable {profile.DeploymentVariable} (Azure-Deployment) fehlt.");
                if (!Uri.TryCreate(endpoint.TrimEnd('/') + "/openai/v1/responses", UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                    throw new InvalidOperationException("Azure-Endpoint muss eine HTTPS-URL sein.");
                if (profile.Entra)
                    return new OpenAiResponsesProvider(profile.Id, Http, uri, deployment, async token =>
                    {
                        var access = await AzureCredential.GetTokenAsync(new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]), token);
                        return new AuthenticationHeaderValue("Bearer", access.Token);
                    }, null, profile.ReasoningEffort, maxAttempts);
                string key = Secret(profile, "AZURE_OPENAI_API_KEY");
                return new OpenAiResponsesProvider(profile.Id, Http, uri, deployment, null, key, profile.ReasoningEffort, maxAttempts);
            }
            case "googleGemini":
                return new GeminiProvider(profile.Id, Http, profile.Model, Secret(profile, "GEMINI_API_KEY"), maxAttempts);
            default:
                throw new NotSupportedException($"Anbieter {profile.Provider} wird nicht unterstützt.");
        }
    }

    private static string Secret(AiProviderProfile profile, string environmentFallback)
    {
        string? secret = profile.CredentialTarget != null ? CredentialStore.Read(profile.CredentialTarget) : null;
        secret ??= Environment.GetEnvironmentVariable(environmentFallback);
        return string.IsNullOrWhiteSpace(secret)
            ? throw new InvalidOperationException($"Kein API-Schlüssel für {profile.Id}. Im Fenster speichern (Windows-Anmeldeinformationen) oder {environmentFallback} setzen.")
            : secret;
    }

    /// <summary>Providers accept different JSON-Schema subsets; string length limits are enforced locally instead.</summary>
    public static JsonObject ProviderSchema(JsonObject schema)
    {
        var copy = schema.DeepClone().AsObject();
        Strip(copy);
        return copy;
        static void Strip(JsonNode? node)
        {
            if (node is JsonObject o)
            {
                foreach (var key in new[] { "$schema", "title", "minLength", "maxLength" }) o.Remove(key);
                foreach (var child in o.ToList()) Strip(child.Value);
            }
            else if (node is JsonArray a) foreach (var child in a) Strip(child);
        }
    }

    internal static async Task<JsonObject> PostAsync(HttpClient http, Func<HttpRequestMessage> create, int maxAttempts, CancellationToken token)
    {
        for (int attempt = 1; ; attempt++)
        {
            HttpResponseMessage response;
            try { response = await http.SendAsync(create(), token); }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !token.IsCancellationRequested)
            {
                throw new AiProviderException("Verbindung unterbrochen oder Zeitüberschreitung nach Versand. Nicht automatisch wiederholt, um Doppelabrechnung zu vermeiden.", AiUsage.None, true, ex);
            }
            using (response)
            {
                string body = await response.Content.ReadAsStringAsync(token);
                if (response.IsSuccessStatusCode)
                    return JsonNode.Parse(body)?.AsObject() ?? throw new AiProviderException("Leere Anbieterantwort.", AiUsage.None);
                bool transient = response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.InternalServerError
                    or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;
                if (!transient || attempt >= maxAttempts)
                    throw new AiProviderException($"HTTP {(int)response.StatusCode}: {ErrorText(body)}", AiUsage.None);
                var wait = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)
                    ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
                wait = TimeSpan.FromMilliseconds(Math.Clamp(wait.TotalMilliseconds, 500, 60_000) + Random.Shared.Next(0, 750));
                await Task.Delay(wait, token);
            }
        }
    }

    private static string ErrorText(string body)
    {
        try { return JsonNode.Parse(body)?["error"]?["message"]?.GetValue<string>() ?? body[..Math.Min(300, body.Length)]; }
        catch (System.Text.Json.JsonException) { return body[..Math.Min(300, body.Length)]; }
    }

    internal static long Long(JsonNode? node) => node?.GetValue<long>() ?? 0;
}

/// <summary>OpenAI Responses API and Azure OpenAI v1 Responses API (same wire format; Azure uses the deployment name as model).</summary>
internal sealed class OpenAiResponsesProvider(string name, HttpClient http, Uri uri, string model,
    Func<CancellationToken, ValueTask<AuthenticationHeaderValue>>? authorization, string? apiKey, string? reasoningEffort, int maxAttempts) : IAiMetadataProvider
{
    public string Name => name;

    public async Task<AiReply> CompleteAsync(string system, string user, byte[]? jpeg, string schemaName, JsonObject schema, int maxOutput, CancellationToken token)
    {
        var content = new JsonArray { new JsonObject { ["type"] = "input_text", ["text"] = user } };
        if (jpeg != null) content.Add(new JsonObject { ["type"] = "input_image", ["image_url"] = "data:image/jpeg;base64," + Convert.ToBase64String(jpeg) });
        var body = new JsonObject
        {
            ["model"] = model,
            ["store"] = false,
            ["max_output_tokens"] = maxOutput,
            ["input"] = new JsonArray
            {
                new JsonObject { ["role"] = "developer", ["content"] = new JsonArray { new JsonObject { ["type"] = "input_text", ["text"] = system } } },
                new JsonObject { ["role"] = "user", ["content"] = content }
            },
            ["text"] = new JsonObject { ["format"] = new JsonObject { ["type"] = "json_schema", ["name"] = schemaName, ["strict"] = true, ["schema"] = AiProviderFactory.ProviderSchema(schema) } }
        };
        if (!string.IsNullOrEmpty(reasoningEffort)) body["reasoning"] = new JsonObject { ["effort"] = reasoningEffort };
        string json = body.ToJsonString();
        var auth = authorization == null ? null : await authorization(token);

        var result = await AiProviderFactory.PostAsync(http, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            if (auth != null) request.Headers.Authorization = auth;
            if (apiKey != null) request.Headers.Add("api-key", apiKey);
            return request;
        }, maxAttempts, token);

        var usage = new AiUsage(AiProviderFactory.Long(result["usage"]?["input_tokens"]),
            AiProviderFactory.Long(result["usage"]?["input_tokens_details"]?["cached_tokens"]),
            AiProviderFactory.Long(result["usage"]?["output_tokens"]));
        if (result["status"]?.GetValue<string>() is { } status && status != "completed")
            throw new AiProviderException($"Antwort unvollständig ({result["incomplete_details"]?["reason"]?.GetValue<string>() ?? status}). Kein automatischer Neuversuch.", usage);
        var text = new StringBuilder();
        foreach (var item in result["output"]?.AsArray() ?? [])
        {
            if (item?["type"]?.GetValue<string>() != "message") continue;
            foreach (var part in item["content"]?.AsArray() ?? [])
            {
                switch (part?["type"]?.GetValue<string>())
                {
                    case "refusal": throw new AiProviderException("Modell hat die Anfrage abgelehnt: " + part["refusal"]?.GetValue<string>(), usage);
                    case "output_text": text.Append(part["text"]?.GetValue<string>()); break;
                }
            }
        }
        return text.Length == 0 ? throw new AiProviderException("Antwort enthält keinen Text.", usage) : new AiReply(text.ToString(), usage);
    }
}

/// <summary>Native Gemini generateContent API with JSON-Schema structured output.</summary>
internal sealed class GeminiProvider(string name, HttpClient http, string model, string apiKey, int maxAttempts) : IAiMetadataProvider
{
    public string Name => name;

    public async Task<AiReply> CompleteAsync(string system, string user, byte[]? jpeg, string schemaName, JsonObject schema, int maxOutput, CancellationToken token)
    {
        var parts = new JsonArray { new JsonObject { ["text"] = user } };
        if (jpeg != null) parts.Add(new JsonObject { ["inlineData"] = new JsonObject { ["mimeType"] = "image/jpeg", ["data"] = Convert.ToBase64String(jpeg) } });
        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = system } } },
            ["contents"] = new JsonArray { new JsonObject { ["role"] = "user", ["parts"] = parts } },
            ["generationConfig"] = new JsonObject
            {
                ["responseMimeType"] = "application/json",
                ["responseJsonSchema"] = AiProviderFactory.ProviderSchema(schema),
                ["maxOutputTokens"] = maxOutput
            }
        };
        string json = body.ToJsonString();
        var uri = new Uri($"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        var result = await AiProviderFactory.PostAsync(http, () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            request.Headers.Add("x-goog-api-key", apiKey);
            return request;
        }, maxAttempts, token);

        var meta = result["usageMetadata"];
        var usage = new AiUsage(AiProviderFactory.Long(meta?["promptTokenCount"]), AiProviderFactory.Long(meta?["cachedContentTokenCount"]),
            AiProviderFactory.Long(meta?["candidatesTokenCount"]) + AiProviderFactory.Long(meta?["thoughtsTokenCount"]));
        if (result["promptFeedback"]?["blockReason"]?.GetValue<string>() is { } blocked)
            throw new AiProviderException("Anfrage vom Anbieter blockiert: " + blocked, usage);
        var candidate = result["candidates"]?.AsArray().FirstOrDefault() ?? throw new AiProviderException("Keine Antwort erhalten.", usage);
        string reason = candidate["finishReason"]?.GetValue<string>() ?? "";
        if (reason != "STOP") throw new AiProviderException($"Antwort unvollständig oder blockiert ({reason}). Kein automatischer Neuversuch.", usage);
        var text = string.Concat((candidate["content"]?["parts"]?.AsArray() ?? [])
            .Where(p => p?["thought"]?.GetValue<bool>() != true).Select(p => p?["text"]?.GetValue<string>()));
        return string.IsNullOrEmpty(text) ? throw new AiProviderException("Antwort enthält keinen Text.", usage) : new AiReply(text, usage);
    }
}
