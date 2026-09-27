using SixLabors.ImageSharp;
using Point = System.Windows.Point;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using PictureExifclone.Services;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Image = SixLabors.ImageSharp.Image;
using PixelImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;
using PixelRect = SixLabors.ImageSharp.Rectangle;

namespace PictureExifclone;

public partial class ImageEditorWindow : Window
{
    private readonly string originalPath;
    private readonly double? latitude, longitude;
    private EditorSession? session;
    private CancellationTokenSource? operation;
    private bool busy, accepting, selecting, fitMode = true, comparing, previewing;
    private double zoom = 1;
    private Point start, end;
    private Point? click;
    private PixelRect selection;
    public byte[]? EditedImageBytes { get; private set; }
    public string EditedExtension { get; private set; } = ".png";

    public ImageEditorWindow(string path, double? lat = null, double? lon = null)
    {
        originalPath = path; latitude = lat; longitude = lon;
        InitializeComponent();
        Loaded += async (_, _) => await RunAsync(async token =>
        {
            session = await EditorSession.OpenAsync(path, token);
            ExportFormat.SelectedIndex = Path.GetExtension(path).ToLowerInvariant() switch { ".jpg" or ".jpeg" => 1, ".tif" or ".tiff" => 2, ".bmp" => 3, _ => 0 };
            await DisplayAsync(); Fit();
        });
        Closed += (_, _) => session?.Dispose();
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (busy) return;
        busy = true; operation = new(); BusyOverlay.Visibility = Visibility.Visible;
        try { await action(operation.Token); }
        catch (OperationCanceledException) { StatusText.Text = "Vorgang abgebrochen; letzter vollständiger Zustand bleibt erhalten."; }
        catch (Exception ex) { StatusText.Text = ex.Message; MessageBox.Show(this, ex.Message, "Bildeditor", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { busy = false; operation.Dispose(); operation = null; BusyOverlay.Visibility = Visibility.Collapsed; UpdateHistory(); }
    }

    private static BitmapImage Bitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); return bitmap;
    }
    private async Task DisplayAsync()
    {
        if (session == null) return;
        DisplayImage.Source = Bitmap(await File.ReadAllBytesAsync(session.CurrentPath));
        ImageContainer.Width = session.Width; ImageContainer.Height = session.Height;
        ImageInfoText.Text = $"{Path.GetFileName(originalPath)}\n{session.Width} × {session.Height} Pixel";
        GpsInfoText.Text = latitude.HasValue && longitude.HasValue
            ? FormattableString.Invariant($"GPS: {latitude:F6}, {longitude:F6}") : "Keine GPS-Daten";
        comparing = previewing = false;
        StatusText.Text = "Bereit. Vorschau und Bearbeitung verwenden dieselben Bildpixel.";
        UpdateHistory(); if (fitMode) Fit();
    }
    private void UpdateHistory() { UndoButton.IsEnabled = session?.CanUndo == true && !busy; RedoButton.IsEnabled = session?.CanRedo == true && !busy; }
    private void Fit()
    {
        if (session == null) return;
        fitMode = true; zoom = PixelGeometry.Fit(session.Width, session.Height, ImageScrollViewer.ViewportWidth, ImageScrollViewer.ViewportHeight);
        ApplyZoom();
    }
    private void ApplyZoom()
    {
        ImageScaleTransform.ScaleX = ImageScaleTransform.ScaleY = zoom;
        ZoomText.Text = $"{zoom * 100:0.##} %"; SelectionRect.StrokeThickness = 1.5 / zoom;
    }
    private void ZoomAt(double factor, Point viewportPoint)
    {
        double old = zoom; fitMode = false; zoom = Math.Clamp(zoom * factor, 0.00001, 16);
        double x = (ImageScrollViewer.HorizontalOffset + viewportPoint.X) / old;
        double y = (ImageScrollViewer.VerticalOffset + viewportPoint.Y) / old;
        ApplyZoom(); ImageScrollViewer.UpdateLayout();
        ImageScrollViewer.ScrollToHorizontalOffset(x * zoom - viewportPoint.X);
        ImageScrollViewer.ScrollToVerticalOffset(y * zoom - viewportPoint.Y);
    }
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomAt(1.2, new Point(ImageScrollViewer.ViewportWidth/2, ImageScrollViewer.ViewportHeight/2));
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomAt(1/1.2, new Point(ImageScrollViewer.ViewportWidth/2, ImageScrollViewer.ViewportHeight/2));
    private void ZoomActual_Click(object sender, RoutedEventArgs e) { fitMode = false; zoom = 1; ApplyZoom(); }
    private void ZoomFit_Click(object sender, RoutedEventArgs e) => Fit();
    private void ViewportChanged(object sender, SizeChangedEventArgs e) { if (fitMode) Dispatcher.BeginInvoke(Fit); }
    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    { if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { ZoomAt(e.Delta > 0 ? 1.1 : 1/1.1, e.GetPosition(ImageScrollViewer)); e.Handled = true; } }

    private void Image_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (busy || session == null || comparing || previewing) return;
        var p = e.GetPosition(ImageContainer); // WPF already inverts the layout transform: 1 local DIP = 1 image pixel.
        if (p.X < 0 || p.Y < 0 || p.X > session.Width || p.Y > session.Height) return;
        click = start = end = p;
        if (ModeCrop.IsChecked == true || ModeBlur.IsChecked == true || ModePixelate.IsChecked == true)
        { selecting = true; selection = PixelRect.Empty; ImageContainer.CaptureMouse(); DrawSelection(); }
        else { Canvas.SetLeft(ClickMarker, p.X - 4); Canvas.SetTop(ClickMarker, p.Y - 4); ClickMarker.Visibility = Visibility.Visible; }
        e.Handled = true;
    }
    private void Canvas_MouseMove(object sender, MouseEventArgs e)
    { if (selecting && session != null) { end = e.GetPosition(ImageContainer); DrawSelection(); } }
    private void DrawSelection()
    {
        if (session == null) return;
        selection = PixelGeometry.Selection(start.X,start.Y,end.X,end.Y,session.Width,session.Height);
        SelectionRect.Visibility = selection.IsEmpty ? Visibility.Collapsed : Visibility.Visible;
        Canvas.SetLeft(SelectionRect, selection.X); Canvas.SetTop(SelectionRect, selection.Y);
        SelectionRect.Width = selection.Width; SelectionRect.Height = selection.Height;
        StatusText.Text = $"Auswahl: {selection.X}, {selection.Y} — {selection.Width} × {selection.Height} Pixel";
    }
    private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
    { if (selecting) { end = e.GetPosition(ImageContainer); DrawSelection(); selecting = false; ImageContainer.ReleaseMouseCapture(); } }
    private void LostCapture(object sender, MouseEventArgs e) { if (selecting) ClearSelection(); }
    private void ClearSelection()
    {
        selecting = false; ImageContainer.ReleaseMouseCapture(); click = null; selection = PixelRect.Empty;
        SelectionRect.Visibility = ClickMarker.Visibility = Visibility.Collapsed;
    }
    private async void Clear_Click(object sender, RoutedEventArgs e) { if (!busy) { ClearSelection(); await DisplayAsync(); } }
    private async void ToolChanged(object sender, RoutedEventArgs e) { if (session != null && !busy) { ClearSelection(); await DisplayAsync(); } }

    private Action<PixelImage> GetOperation()
    {
        if (session == null) throw new InvalidOperationException("Kein Bild geladen.");
        var region = selection;
        if (ModeCrop.IsChecked == true || ModeBlur.IsChecked == true || ModePixelate.IsChecked == true)
        {
            if (region.IsEmpty) throw new InvalidOperationException("Bitte einen nicht leeren Bereich auswählen.");
            float strength = (float)(Math.Min(region.Width,region.Height) * EffectSize.Value / 100);
            if (ModeCrop.IsChecked == true) return image => image.Mutate(c => c.Crop(region));
            if (ModeBlur.IsChecked == true) return image => EditorSession.Blur(image,region,strength);
            return image => EditorSession.Pixelate(image,region,Math.Max(1,(int)Math.Round(strength)));
        }
        string text = WatermarkTextBox.Text;
        if (ModeGeo.IsChecked == true)
        {
            if (!latitude.HasValue || !longitude.HasValue || !PixelGeometry.ValidGps(latitude.Value,longitude.Value))
                throw new InvalidOperationException("Keine gültigen GPS-Koordinaten am Bild vorhanden.");
            text = string.Create(CultureInfo.InvariantCulture,$"Lat: {latitude.Value:F6}\nLon: {longitude.Value:F6}");
        }
        float size = (float)(Math.Min(session.Width,session.Height) * StampSize.Value / 100);
        double margin = Math.Max(1,Math.Min(session.Width,session.Height)*0.02);
        int anchor = StampAnchor.SelectedIndex;
        Point origin = anchor switch
        {
            1 => new(margin,margin), 2 => new(session.Width-margin,margin),
            3 => new(margin,session.Height-margin), 4 => new(session.Width-margin,session.Height-margin),
            _ => click ?? throw new InvalidOperationException("Bitte Stempelposition wählen.")
        };
        bool right = anchor is 2 or 4, bottom = anchor is 3 or 4;
        var color = StampColor.SelectedColor;
        var ink = SixLabors.ImageSharp.Color.FromRgba(color.R,color.G,color.B,color.A);
        return image => EditorSession.Stamp(image,text,size,ink,origin.X,origin.Y,right,bottom);
    }
    private async void Apply_Click(object sender, RoutedEventArgs e) => await RunAsync(async token =>
    {
        var action = GetOperation(); await session!.ApplyAsync(action,token); ClearSelection(); await DisplayAsync();
    });
    private async void Preview_Click(object sender, RoutedEventArgs e) => await RunAsync(async token =>
    {
        var action = GetOperation();
        var bytes = await Task.Run(() =>
        {
            using var image = Image.Load<Rgba32>(session!.CurrentPath); action(image); token.ThrowIfCancellationRequested();
            using var stream = new MemoryStream(); image.SaveAsPng(stream); return stream.ToArray();
        },token);
        var bitmap = Bitmap(bytes); DisplayImage.Source = bitmap;
        // Cropped preview has its own dimensions; selection remains the pending operation in original pixels.
        ImageContainer.Width = bitmap.PixelWidth; ImageContainer.Height = bitmap.PixelHeight;
        SelectionRect.Visibility = ClickMarker.Visibility = Visibility.Collapsed;
        previewing = true; StatusText.Text = "Vorschau — Anwenden übernimmt, Löschen kehrt zum Arbeitsbild zurück.";
    });
    private async void RotateLeft_Click(object sender, RoutedEventArgs e) => await RotateAsync(RotateMode.Rotate270);
    private async void RotateRight_Click(object sender, RoutedEventArgs e) => await RotateAsync(RotateMode.Rotate90);
    private async Task RotateAsync(RotateMode mode) => await RunAsync(async token =>
    { if (session != null) { await session.ApplyAsync(i => i.Mutate(c => c.Rotate(mode)),token); ClearSelection(); await DisplayAsync(); } });
    private async void Undo_Click(object sender, RoutedEventArgs e) { if (!busy && session != null) { session.Undo(); ClearSelection(); await DisplayAsync(); } }
    private async void Redo_Click(object sender, RoutedEventArgs e) { if (!busy && session != null) { session.Redo(); ClearSelection(); await DisplayAsync(); } }
    private async void Compare_Click(object sender, RoutedEventArgs e)
    {
        if (busy || session == null) return;
        if (comparing) { await DisplayAsync(); return; }
        await RunAsync(async token =>
        {
            var bytes = await Task.Run(() => { using var i=Image.Load<Rgba32>(originalPath); i.Mutate(c=>c.AutoOrient()); using var m=new MemoryStream(); i.SaveAsPng(m); return m.ToArray(); },token);
            var bitmap=Bitmap(bytes); DisplayImage.Source=bitmap; ImageContainer.Width=bitmap.PixelWidth; ImageContainer.Height=bitmap.PixelHeight;
            ClearSelection(); comparing=true; StatusText.Text="Originalansicht — erneut klicken für bearbeitetes Bild.";
        });
    }
    private void ShowExif_Click(object sender, RoutedEventArgs e)
    {
        try { var text=string.Join(Environment.NewLine,MetadataExtractor.ImageMetadataReader.ReadMetadata(originalPath).SelectMany(d=>d.Tags.Select(t=>$"{d.Name}: {t.Name} = {t.Description}")));
            new Window { Owner=this, Title="Originalmetadaten", Width=650, Height=500, Content=new TextBox { Text=text, IsReadOnly=true, TextWrapping=TextWrapping.Wrap, VerticalScrollBarVisibility=ScrollBarVisibility.Auto } }.ShowDialog(); }
        catch(Exception ex) { StatusText.Text=ex.Message; }
    }
    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (session == null || busy) return;
        await RunAsync(async token =>
        {
            EditedExtension=(string)((ComboBoxItem)ExportFormat.SelectedItem).Tag;
            EditedImageBytes=await session.ExportAsync(EditedExtension,(int)JpegQuality.Value,token);
        });
        if (EditedImageBytes != null) { accepting=true; DialogResult=true; }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    private void CancelOperation_Click(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (busy) { operation?.Cancel(); e.Cancel=true; return; }
        if (!accepting && session?.HasChanges == true && MessageBox.Show(this,"Ungespeicherte Änderungen verwerfen?","Bildeditor",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes) e.Cancel=true;
    }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key==Key.Escape) { Clear_Click(sender,e); e.Handled=true; }
        if (Keyboard.Modifiers==ModifierKeys.Control && Keyboard.FocusedElement is not TextBox)
        { if(e.Key==Key.Z) { Undo_Click(sender,e); e.Handled=true; } if(e.Key==Key.Y) { Redo_Click(sender,e); e.Handled=true; } }
    }
}
