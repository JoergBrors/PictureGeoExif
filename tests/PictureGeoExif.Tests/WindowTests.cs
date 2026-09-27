using PictureExifclone;
using PictureExifclone.Models;

namespace PictureGeoExif.Tests;

public class WindowTests
{
    private static void OnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start(); thread.Join();
        if (error != null) throw new Xunit.Sdk.XunitException("Fenster konnte nicht erstellt werden: " + error);
    }

    [Fact]
    public void AiMetadataWindow_LoadsXamlAndDefaultTemplate() => OnSta(() =>
    {
        string settingsPath = Path.Combine(Path.GetTempPath(), "pge-test-settings-" + Guid.NewGuid().ToString("N") + ".json");
        var window = new AiMetadataWindow([new ImageItem { FilePath = @"C:\nicht\vorhanden.jpg" }], new AppSettings { FilePath = settingsPath });
        var box = (System.Windows.Controls.TextBox)window.FindName("TemplateBox");
        Assert.Contains("openai-economy", box.Text);
        window.Close();
    });

    /// <summary>
    /// Catches handlers that fire during InitializeComponent (e.g. IsChecked="True") before sibling controls exist.
    /// Not closed on purpose: Closed saves the real user settings.
    /// </summary>
    [Fact]
    public void MainWindow_ConstructsWithoutInitOrderErrors() => OnSta(() => _ = new MainWindow());

    [Fact]
    public void RoadMatchSettingsWindow_LoadsXaml() => OnSta(() =>
        new RoadMatchSettingsWindow(new AppSettings { FilePath = Path.Combine(Path.GetTempPath(), "pge-test-" + Guid.NewGuid().ToString("N") + ".json") }).Close());

    [Fact]
    public void ImageEditorWindow_LoadsXaml() => OnSta(() => new ImageEditorWindow(@"C:\nicht\vorhanden.jpg").Close());
}
