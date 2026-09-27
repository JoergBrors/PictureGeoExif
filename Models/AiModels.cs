using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;

namespace PictureExifclone.Models;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Set<T>(ref T field, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value; PropertyChanged?.Invoke(this, new(property));
    }
}

/// <summary>Local, pre-norm view of usage preferences. Never derived from image content.</summary>
public enum PreferenceState { Missing, PresentUndecoded, AllowedByIptc, ProhibitedOrRestricted }

public sealed class AiImageRow : Observable
{
    public string FilePath { get; init; } = "";
    public string Name => Path.GetFileName(FilePath);
    private bool selected = true;
    public bool Selected { get => selected; set => Set(ref selected, value); }
    private string status = "Bereit";
    public string Status { get => status; set => Set(ref status, value); }
    /// <summary>SHA-256 of the file at analysis time; writing is refused if the file changed since.</summary>
    public string Revision { get; set; } = "";
    public PreferenceState Preference { get; set; }
    public string PreferenceText { get; set; } = "";
    public string? ExistingSeason { get; set; }
    public string? ExistingTitle { get; set; }
    public string[] ExistingKeywords { get; set; } = [];
    public ObservableCollection<AiFieldChange> Changes { get; } = [];
}

public enum XmpValueKind { Text, LangAlt, Bag, Seq }

public sealed class AiFieldChange : Observable
{
    public required string Field { get; init; }
    /// <summary>Logical target such as "XMP.pge:Season"; only registered targets are writable.</summary>
    public required string Target { get; init; }
    public XmpValueKind Kind { get; init; }
    public string Current { get; init; } = "";
    public required string Proposed { get; init; }
    public required string Source { get; init; }
    public string Evidence { get; init; } = "";
    private bool accept;
    public bool Accept { get => accept; set => Set(ref accept, value); }
}

public sealed record AiProposal(string? Season, string? Title, string[] Keywords, double Confidence, string Evidence);
public sealed record AiUsage(long InputTokens, long CachedTokens, long OutputTokens)
{
    public static readonly AiUsage None = new(0, 0, 0);
}
public sealed record AiReply(string Json, AiUsage Usage);
public sealed record AiPrices(decimal Input, decimal Cached, decimal Output)
{
    /// <summary>USD per million tokens. Output already contains reasoning tokens for all supported providers.</summary>
    public decimal Cost(AiUsage usage) =>
        (Math.Max(0, usage.InputTokens - usage.CachedTokens) * Input + usage.CachedTokens * Cached + usage.OutputTokens * Output) / 1_000_000m;
    public bool IsComplete => Input > 0 && Output > 0 && Cached >= 0;
}

public sealed record AiProviderProfile(string Id, string Provider, string Model, string? CredentialTarget,
    string? EndpointVariable, string? DeploymentVariable, bool Entra, string? ReasoningEffort, bool EvaluationOnly)
{
    public override string ToString() => $"{Id} · {Model}{(EvaluationOnly ? " (nur Evaluation)" : "")}";
}

public sealed class AiTemplate
{
    public JsonObject Root { get; }
    public string Json => Root.ToJsonString(new JsonSerializerOptions { WriteIndented=true, Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    public string Id => Root["id"]?.GetValue<string>() ?? "vorlage";
    public string SystemPrompt => Root["analysis"]!["systemPrompt"]!.GetValue<string>();
    public JsonObject Fields => Root["analysis"]!["fields"]!.AsObject();
    public int MaxOutput => Root["costControl"]?["maxModelOutputTokens"]?.GetValue<int>() ?? 1024;
    public int LongEdge => Root["imageInput"]?["maxLongEdgePixels"]?.GetValue<int>() ?? 1024;
    public int Quality => Root["imageInput"]?["quality"]?.GetValue<int>() ?? 80;
    public decimal Budget => Root["costControl"]?["maxEstimatedRunCost"]?.GetValue<decimal>() ?? 5;
    public decimal ImageBudget => Root["costControl"]?["maxEstimatedImageCost"]?.GetValue<decimal>() ?? 0.03m;
    public double ReviewThreshold => Fields["jahreszeit"]?["confidenceReviewThreshold"]?.GetValue<double>() ?? 0.8;
    public int MaxConcurrency => Math.Clamp(Root["execution"]?["maxConcurrency"]?.GetValue<int>() ?? 2, 1, 4);
    public int MaxAttempts => Math.Clamp(Root["execution"]?["maxAttemptsPerImage"]?.GetValue<int>() ?? 3, 1, 5);
    public string SelectedProvider => Root["execution"]?["selectedProvider"]?.GetValue<string>() ?? "";
    public string Hash => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Root.ToJsonString())));
    public string WritePolicy(string field) => Fields[field]?["writePolicy"]?.GetValue<string>() ?? "fillMissing";

    public IReadOnlyList<AiProviderProfile> Providers =>
        (Root["providers"] as JsonObject ?? []).Select(p =>
        {
            var o = p.Value!.AsObject();
            string? credential = o["credentialReference"]?.GetValue<string>();
            return new AiProviderProfile(p.Key, o["provider"]!.GetValue<string>(),
                o["model"]?.GetValue<string>() ?? o["modelFamily"]?.GetValue<string>() ?? "",
                credential?.StartsWith("credential-manager:", StringComparison.Ordinal) == true ? credential["credential-manager:".Length..] : null,
                o["endpointEnvironmentVariable"]?.GetValue<string>(), o["deploymentEnvironmentVariable"]?.GetValue<string>(),
                o["authentication"]?.GetValue<string>() == "entraId", o["reasoningEffort"]?.GetValue<string>(),
                o["enableAfterQualityAndCostEvaluation"]?.GetValue<bool>() == true);
        }).ToList();

    private AiTemplate(JsonObject root) { Root=root; }

    public static AiTemplate Parse(string json)
    {
        if(json.Length>128_000) throw new InvalidDataException("Vorlage zu groß (maximal 128 KB).");
        var root=JsonNode.Parse(json,new JsonNodeOptions(),new JsonDocumentOptions { MaxDepth=32, CommentHandling=JsonCommentHandling.Skip })?.AsObject() ?? throw new InvalidDataException("JSON-Objekt erwartet.");
        var template=new AiTemplate(root);
        try { Validate(template); }
        catch (Exception ex) when (ex is InvalidOperationException or NullReferenceException or FormatException)
        { throw new InvalidDataException("Vorlage hat einen ungültigen Aufbau: " + ex.Message, ex); }
        return template;
    }

    private static void Validate(AiTemplate template)
    {
        var root=template.Root;
        if(root["templateVersion"]?.GetValue<string>()!="1.0") throw new InvalidDataException("Vorlagenversion 1.0 erforderlich.");
        if(string.IsNullOrWhiteSpace(template.SystemPrompt) || template.SystemPrompt.Length>8000) throw new InvalidDataException("Systemprompt fehlt oder ist zu lang.");
        var allowed=new HashSet<string> { "jahreszeit","titel","stichwoerter" };
        if(template.Fields.Count==0 || template.Fields.Any(p=>!allowed.Contains(p.Key))) throw new InvalidDataException("Unterstützte KI-Felder: jahreszeit, titel, stichwoerter.");
        if(template.LongEdge is <256 or >2048 || template.Quality is <20 or >100 || template.MaxOutput is <256 or >8192 || template.Budget<=0 || template.ImageBudget<=0)
            throw new InvalidDataException("Ungültige Bild-, Token- oder Budgetgrenzen.");
        if(root["learningPreferences"]?["allowModelInference"]?.GetValue<bool>()==true) throw new InvalidDataException("Nutzungsrechte dürfen nicht aus dem Bild erraten werden.");
        if(root["execution"]?["writeMode"]?.GetValue<string>()!="reviewThenApply") throw new InvalidDataException("Schreibmodus muss reviewThenApply sein.");
        if(root["execution"]?["providerFallback"]?.GetValue<string>() is { } fallback && fallback!="disabled") throw new InvalidDataException("Automatischer Anbieterwechsel wird nicht unterstützt.");
        if(root["costControl"]?["webSearch"]?.GetValue<bool>()==true) throw new InvalidDataException("Websuche wird nicht unterstützt.");
        if(root["imageInput"]?["sendOriginal"]?.GetValue<bool>()==true) throw new InvalidDataException("Nur verkleinerte Vorschauen werden übertragen.");
        if(root["metadataInput"]?["sendExactGps"]?.GetValue<bool>()==true || root["metadataInput"]?["sendFilePath"]?.GetValue<bool>()==true)
            throw new InvalidDataException("Exakte GPS-Daten und Dateipfade werden nicht an Modelle gesendet.");
        foreach(var field in template.Fields)
        {
            if(field.Value?["prompt"]?.GetValue<string>() is not {Length:>0 and <=4000}) throw new InvalidDataException("Feldprompt fehlt oder ist zu lang.");
            var policy=field.Value?["writePolicy"]?.GetValue<string>();
            if(policy is not ("fillMissing" or "mergeUnique" or "replace")) throw new InvalidDataException("Unbekannte Schreibregel.");
        }
        var providers=template.Providers;
        if(providers.Count==0) throw new InvalidDataException("Mindestens ein Anbieterprofil erforderlich.");
        foreach(var p in providers)
        {
            if(p.Provider is not ("openAI" or "azureOpenAI" or "googleGemini")) throw new InvalidDataException($"Unbekannter Anbieter: {p.Provider}");
            if(p.Provider=="azureOpenAI" ? p.EndpointVariable==null || p.DeploymentVariable==null : string.IsNullOrWhiteSpace(p.Model))
                throw new InvalidDataException($"Profil {p.Id}: Modell bzw. Azure-Endpoint/Deployment-Variable fehlt.");
        }
    }

    public static AiProposal ParseResponse(string json)
    {
        var node=JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Leere KI-Antwort.");
        if(node.Count!=3 || !node.ContainsKey("jahreszeit") || !node.ContainsKey("titel") || !node.ContainsKey("stichwoerter")) throw new InvalidDataException("KI-Antwort enthält unbekannte oder fehlende Felder.");
        var season=node["jahreszeit"]!.AsObject(); var title=node["titel"]!.AsObject();
        if(season.Count!=3 || !season.ContainsKey("value") || !season.ContainsKey("confidence") || !season.ContainsKey("evidence") || title.Count!=1 || !title.ContainsKey("value")) throw new InvalidDataException("Ungültige Antwortstruktur.");
        string? value=season["value"]?.GetValue<string>();
        if(value is not(null or "Winter" or "Frühling" or "Sommer" or "Herbst")) throw new InvalidDataException("Ungültige Jahreszeit.");
        double confidence=season["confidence"]!.GetValue<double>(); string evidence=season["evidence"]!.GetValue<string>();
        string? caption=title["value"]?.GetValue<string>()?.Trim();
        string[] keywords=node["stichwoerter"]!.AsArray().Select(x=>x!.GetValue<string>().Trim()).ToArray();
        if(!double.IsFinite(confidence)||confidence<0||confidence>1||evidence.Length>180||caption?.Length>80||keywords.Length>8||keywords.Any(x=>x.Length is 0 or >40)) throw new InvalidDataException("KI-Antwort verletzt die Feldgrenzen.");
        return new(value,string.IsNullOrEmpty(caption)?null:caption,keywords.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),confidence,evidence);
    }
}
