namespace PictureExifclone.Services;

/// <summary>Version and identity derived from the csproj &lt;Version&gt;, so tags, UI and HTTP identification never drift apart.</summary>
public static class AppInfo
{
    public const string RepositoryUrl = "https://github.com/JoergBrors/PictureGeoExif";
    public static string Version => typeof(AppInfo).Assembly.GetName().Version?.ToString(2) ?? "0.0";
    /// <summary>Honest app identification appended to HTTP user agents (OSM tile policy, AI providers).</summary>
    public static string UserAgent => $"PictureGeoExif/{Version} (+{RepositoryUrl})";
}
