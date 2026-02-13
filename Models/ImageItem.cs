using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace PictureExifclone.Models
{
    public class ImageItem : INotifyPropertyChanged
    {
        private string _filePath = string.Empty;
        private BitmapImage? _thumbnail;
        private double? _latitude;
        private double? _longitude;
        private bool _isSelected;

        public string FilePath
        {
            get => _filePath;
            set
            {
                _filePath = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileName));
            }
        }

        public string FileName => System.IO.Path.GetFileName(FilePath);

        public BitmapImage? Thumbnail
        {
            get => _thumbnail;
            set
            {
                _thumbnail = value;
                OnPropertyChanged();
            }
        }

        public double? Latitude
        {
            get => _latitude;
            set
            {
                _latitude = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasGpsData));
                OnPropertyChanged(nameof(GpsInfo));
            }
        }

        public double? Longitude
        {
            get => _longitude;
            set
            {
                _longitude = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasGpsData));
                OnPropertyChanged(nameof(GpsInfo));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public bool HasGpsData => Latitude.HasValue && Longitude.HasValue;

        public string GpsInfo => HasGpsData 
            ? $"GPS: {Latitude:F6}, {Longitude:F6}" 
            : "Keine GPS-Daten";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
