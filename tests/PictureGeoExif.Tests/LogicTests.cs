using System.Text.Json.Nodes;
using PictureExifclone.Models;
using PictureExifclone.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PictureGeoExif.Tests;

public class GeometryTests
{
    [Fact]
    public void Selection_ClipsNegativeOriginByShorteningWidth() =>
        Assert.Equal(new Rectangle(0, 0, 30, 10), PixelGeometry.Selection(-20, 0, 30, 10, 100, 100));

    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(1, 1, 0, 0)]
    public void Selection_AllDragDirections_OnePixel(double x1, double y1, double x2, double y2) =>
        Assert.Equal(new Rectangle(0, 0, 1, 1), PixelGeometry.Selection(x1, y1, x2, y2, 1, 1));

    [Fact]
    public void Selection_FullImageIncludesRightAndBottomEdges() =>
        Assert.Equal(new Rectangle(0, 0, 20000, 1000), PixelGeometry.Selection(-5, -5, 20010, 1010, 20000, 1000));

    [Theory]
    [InlineData(5, 5, 5, 9)]
    [InlineData(double.NaN, 0, 3, 3)]
    [InlineData(200, 200, 300, 300)]
    public void Selection_EmptyOrInvalid(double x1, double y1, double x2, double y2) =>
        Assert.True(PixelGeometry.Selection(x1, y1, x2, y2, 100, 100).IsEmpty);

    [Fact]
    public void Fit_AllowsBelowTenPercent() => Assert.True(PixelGeometry.Fit(20000, 1000, 832, 600) < 0.1);

    [Fact]
    public void Blur_OnlyChangesSelection()
    {
        using var image = new Image<Rgba32>(40, 40);
        for (int y = 0; y < 40; y++) for (int x = 0; x < 40; x++) image[x, y] = new Rgba32((byte)(x * 6), (byte)(y * 6), 0);
        using var before = image.Clone();
        var region = new Rectangle(10, 10, 5, 5);
        EditorSession.Blur(image, region, 3);
        for (int y = 0; y < 40; y++) for (int x = 0; x < 40; x++)
            if (!region.Contains(x, y)) Assert.Equal(before[x, y], image[x, y]);
    }

    [Fact]
    public void Pixelate_BlockLargerThanRegion_DoesNotThrow()
    {
        using var image = new Image<Rgba32>(10, 10);
        EditorSession.Pixelate(image, new Rectangle(0, 0, 1, 7), 50);
    }

    [Fact]
    public void Stamp_RightBottomAnchor_StaysInsideImage()
    {
        using var image = new Image<Rgba32>(200, 100, Color.White.ToPixel<Rgba32>());
        EditorSession.Stamp(image, "Lat: 1.000000\nLon: 2.000000", 20, Color.Black, 196, 96, true, true);
        Assert.Equal(Color.White.ToPixel<Rgba32>(), image[0, 0]);
    }
}

public class TemplateTests
{
    private static string Template => File.ReadAllText(MetadataTests.TemplatePath);

    [Fact]
    public void DefaultTemplate_IsValid_AndDefaultsToOpenAiEconomy()
    {
        var template = AiTemplate.Parse(Template);
        Assert.Equal("openai-economy", template.SelectedProvider);
        Assert.Contains(template.Providers, p => p.Id == "azure-gpt5-mini" && p.Entra);
        Assert.Equal("PictureGeoExif/OpenAI", template.Providers.Single(p => p.Id == "openai-economy").CredentialTarget);
    }

    [Theory]
    [InlineData("learningPreferences", "allowModelInference", "true")]
    [InlineData("execution", "writeMode", "\"direct\"")]
    [InlineData("imageInput", "sendOriginal", "true")]
    [InlineData("metadataInput", "sendExactGps", "true")]
    [InlineData("execution", "providerFallback", "\"any\"")]
    public void UnsafeTemplateSettings_AreRejected(string section, string key, string json)
    {
        var root = JsonNode.Parse(Template)!.AsObject();
        root[section]![key] = JsonNode.Parse(json);
        Assert.Throws<InvalidDataException>(() => AiTemplate.Parse(root.ToJsonString()));
    }

    [Fact]
    public void MalformedTemplate_GivesInvalidData()
    {
        var root = JsonNode.Parse(Template)!.AsObject();
        root["analysis"]!["systemPrompt"] = 42;
        Assert.Throws<InvalidDataException>(() => AiTemplate.Parse(root.ToJsonString()));
    }

    [Fact]
    public void ExampleResponse_Parses()
    {
        var proposal = AiTemplate.ParseResponse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Templates", "ai-metadata.response.example.json")));
        Assert.Equal("Winter", proposal.Season);
        Assert.Equal(4, proposal.Keywords.Length);
    }

    [Theory]
    [InlineData("""{"jahreszeit":{"value":"Monsun","confidence":0.5,"evidence":""},"titel":{"value":null},"stichwoerter":[]}""")]
    [InlineData("""{"jahreszeit":{"value":null,"confidence":1.5,"evidence":""},"titel":{"value":null},"stichwoerter":[]}""")]
    [InlineData("""{"jahreszeit":{"value":null,"confidence":0.5,"evidence":""},"titel":{"value":null},"stichwoerter":[],"learningOptOut":"optIn"}""")]
    public void InvalidResponses_AreRejected(string json) => Assert.Throws<InvalidDataException>(() => AiTemplate.ParseResponse(json));

    [Fact]
    public void Proposals_RespectFillMissingAndMergeUnique()
    {
        var template = AiTemplate.Parse(Template);
        var row = new AiImageRow { FilePath = "x.jpg", ExistingSeason = "Herbst", ExistingKeywords = ["Schnee"] };
        var changes = AiMetadataService.Proposals(row, new AiProposal("Winter", "Titel", ["schnee", "Weg"], 0.5, "e"), template, "KI").ToList();
        Assert.DoesNotContain(changes, c => c.Target == "XMP.pge:Season");
        Assert.Equal("Weg", changes.Single(c => c.Target == "XMP.dc:subject").Proposed);
        Assert.True(changes.Single(c => c.Target == "XMP.dc:title").Accept);
    }

    [Fact]
    public void LowConfidenceSeason_IsNotPreaccepted()
    {
        var template = AiTemplate.Parse(Template);
        var change = AiMetadataService.Proposals(new AiImageRow { FilePath = "x" }, new AiProposal("Sommer", null, [], 0.5, "e"), template, "KI").Single();
        Assert.False(change.Accept);
    }

    [Fact]
    public void ProviderSchema_StripsLengthKeywords_KeepsStructure()
    {
        var schema = AiProviderFactory.ProviderSchema(JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Templates", "ai-metadata.response.schema.json")))!.AsObject());
        string json = schema.ToJsonString();
        Assert.DoesNotContain("maxLength", json);
        Assert.DoesNotContain("$schema", json);
        Assert.Contains("additionalProperties", json);
    }

    [Fact]
    public void Cost_UsesCachedRate_AndNeverNegative()
    {
        var prices = new AiPrices(1m, 0.1m, 10m);
        Assert.Equal((900m * 1 + 100m * 0.1m + 50m * 10) / 1_000_000m, prices.Cost(new AiUsage(1000, 100, 50)));
        Assert.Equal(0.1m * 200 / 1_000_000m, prices.Cost(new AiUsage(100, 200, 0)));
    }
}
