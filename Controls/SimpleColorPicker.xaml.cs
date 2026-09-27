using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.ComponentModel;

namespace PictureExifclone.Controls
{
    public partial class SimpleColorPicker : UserControl, INotifyPropertyChanged
    {
        private Color _selectedColor = Colors.White;

        public static readonly DependencyProperty SelectedColorProperty =
            DependencyProperty.Register(
                nameof(SelectedColor), 
                typeof(Color), 
                typeof(SimpleColorPicker),
                new FrameworkPropertyMetadata(
                    Colors.White, 
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedColorChanged));

        public Color SelectedColor
        {
            get 
            { 
                var value = GetValue(SelectedColorProperty);
                return value != null ? (Color)value : Colors.White;
            }
            set => SetValue(SelectedColorProperty, value);
        }

        private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SimpleColorPicker picker && e.NewValue != null)
            {
                picker._selectedColor = (Color)e.NewValue;
                picker.OnPropertyChanged(nameof(SelectedColor));
            }
        }

        public SimpleColorPicker()
        {
            _selectedColor = Colors.White;
            
            InitializeComponent();
            
            // Explizit Default-Wert setzen
            SelectedColor = Colors.White;
            
            DataContext = this;
        }

        private void SelectColor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new ColorPickerDialog(SelectedColor)
                {
                    Owner = Window.GetWindow(this),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                if (dialog.ShowDialog() == true)
                {
                    SelectedColor = dialog.SelectedColor;
                    _selectedColor = dialog.SelectedColor;
                    OnPropertyChanged(nameof(SelectedColor));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim ColorPicker: {ex.Message}");
                MessageBox.Show($"Fehler beim Auswählen der Farbe: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ColorPickerDialog : Window
    {
        public Color SelectedColor { get; private set; }

        private static readonly Color[] PresetColors = new[]
        {
            Colors.White, Colors.Black, Colors.Red, Colors.Green, Colors.Blue,
            Colors.Yellow, Colors.Orange, Colors.Purple, Colors.Pink, Colors.Brown,
            Colors.Gray, Colors.LightGray, Colors.DarkGray, Colors.Cyan, Colors.Magenta,
            Colors.Lime, Colors.Navy, Colors.Teal, Colors.Maroon, Colors.Olive
        };

        public ColorPickerDialog(Color initialColor)
        {
            SelectedColor = initialColor;
            Title = "Farbe auswählen";
            Width = 350;
            Height = 250;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));

            var grid = new Grid { Margin = new Thickness(10) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var uniformGrid = new UniformGrid { Columns = 5, Rows = 4 };

            foreach (var color in PresetColors)
            {
                var localColor = color;
                var border = new Border
                {
                    Width = 50,
                    Height = 40,
                    Background = new SolidColorBrush(localColor),
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(5),
                    Cursor = System.Windows.Input.Cursors.Hand
                };

                border.MouseLeftButtonDown += (s, e) =>
                {
                    SelectedColor = localColor;
                    DialogResult = true;
                    Close();
                };

                uniformGrid.Children.Add(border);
            }

            grid.Children.Add(uniformGrid);
            Grid.SetRow(uniformGrid, 0);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var cancelButton = new Button
            {
                Content = "Abbrechen",
                Width = 80,
                Height = 30,
                Margin = new Thickness(5, 0, 0, 0)
            };
            cancelButton.Click += (s, e) => 
            { 
                DialogResult = false; 
                Close(); 
            };

            buttonPanel.Children.Add(cancelButton);
            grid.Children.Add(buttonPanel);
            Grid.SetRow(buttonPanel, 1);

            Content = grid;
        }
    }
}
