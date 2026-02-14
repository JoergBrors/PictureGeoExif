using System.Windows;

namespace PictureExifclone
{
    public partial class LicenseViewer : Window
    {
        public LicenseViewer()
        {
            InitializeComponent();
            LoadLicenses();
        }

        private void LoadLicenses()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                
                string licensePath = System.IO.Path.Combine(baseDir, "LICENSE");
                if (System.IO.File.Exists(licensePath))
                {
                    LicenseTextBox.Text = System.IO.File.ReadAllText(licensePath);
                }
                else
                {
                    LicenseTextBox.Text = "LICENSE-Datei nicht gefunden.\n\nPfad: " + licensePath;
                }

                string thirdPartyPath = System.IO.Path.Combine(baseDir, "THIRD-PARTY-LICENSES.md");
                if (System.IO.File.Exists(thirdPartyPath))
                {
                    ThirdPartyTextBox.Text = System.IO.File.ReadAllText(thirdPartyPath);
                }
                else
                {
                    ThirdPartyTextBox.Text = "THIRD-PARTY-LICENSES.md nicht gefunden.\n\nPfad: " + thirdPartyPath;
                }

                string licensesDir = System.IO.Path.Combine(baseDir, "licenses");
                if (System.IO.Directory.Exists(licensesDir))
                {
                    var files = System.IO.Directory.GetFiles(licensesDir, "*.*", System.IO.SearchOption.AllDirectories);
                    LicensesInfoTextBlock.Text = $"licenses-Ordner gefunden ({files.Length} Dateien)";
                }
                else
                {
                    LicensesInfoTextBlock.Text = "licenses-Ordner nicht gefunden";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Lizenzen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OpenLicensesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string licensesDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "licenses");
                if (System.IO.Directory.Exists(licensesDir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", licensesDir);
                }
                else
                {
                    MessageBox.Show("licenses-Ordner nicht gefunden.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Oeffnen des Ordners: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
