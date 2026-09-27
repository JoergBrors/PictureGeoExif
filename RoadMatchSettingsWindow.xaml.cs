using System.Windows;
using System.Windows.Controls;
using PictureExifclone.Services;

namespace PictureExifclone;

public partial class RoadMatchSettingsWindow : Window
{
    private readonly AppSettings settings;

    public RoadMatchSettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        this.settings = settings;
        Fill(settings.RoadMatchUrl, settings.RoadMatchProfile, settings.RoadMatchMaxDeviationMeters);
    }

    private void Fill(string url, string profile, double deviation)
    {
        UrlBox.Text = url;
        ProfileBox.SelectedItem = ProfileBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == profile) ?? ProfileBox.Items[0];
        DeviationSlider.Value = Math.Clamp(deviation, DeviationSlider.Minimum, DeviationSlider.Maximum);
    }

    private void Default_Click(object sender, RoutedEventArgs e) => Fill(RoadMatcher.DefaultServer, "pedestrian", 25);

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!RoadMatcher.IsAllowedServer(UrlBox.Text, out var uri))
        {
            ErrorText.Text = "Bitte eine HTTPS-Adresse eintragen (HTTP nur für einen Server auf diesem Rechner, z. B. http://localhost:8002).";
            return;
        }
        settings.RoadMatchUrl = uri.AbsoluteUri.TrimEnd('/');
        settings.RoadMatchProfile = (string)((ComboBoxItem)ProfileBox.SelectedItem).Tag;
        settings.RoadMatchMaxDeviationMeters = DeviationSlider.Value;
        settings.Save();
        DialogResult = true;
    }
}
