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
            ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"📍 {Latitude:F6}, {Longitude:F6}")
            : "Keine GPS-Daten";

        private int _routeNumber, _routeIndex;

        /// <summary>1-based virtual route number, 0 = no route (no GPS).</summary>
        public int RouteNumber { get => _routeNumber; private set { _routeNumber = value; OnPropertyChanged(); OnPropertyChanged(nameof(RouteInfo)); } }
        /// <summary>1-based position along the route (south→north or west→east).</summary>
        public int RouteIndex { get => _routeIndex; private set { _routeIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(RouteInfo)); } }
        private bool _isBranch;
        /// <summary>True if the photo lies on a side branch (e.g. house connection), not on the trunk.</summary>
        public bool IsBranch { get => _isBranch; private set { _isBranch = value; OnPropertyChanged(); OnPropertyChanged(nameof(RouteInfo)); } }
        public string RouteInfo => RouteNumber > 0 ? $"Trasse {RouteNumber} · Nr. {RouteIndex}{(IsBranch ? " · Abzweig" : "")}" : "";

        public void SetRoute(int number, int index, bool isBranch = false) { RouteNumber = number; RouteIndex = index; IsBranch = isBranch; }

        private readonly Stack<(string Path, double? Latitude, double? Longitude)> _history = new();

        public bool CanUndo => _history.Count > 0;

        /// <summary>Remember the current state before a save/GPS change so it can be undone.</summary>
        public void PushHistory()
        {
            _history.Push((FilePath, Latitude, Longitude));
            OnPropertyChanged(nameof(CanUndo));
        }

        /// <summary>Restores the previous path and coordinates. Returns the path that was active before undo.</summary>
        public string? Undo()
        {
            if (_history.Count == 0) return null;
            string current = FilePath;
            var previous = _history.Pop();
            FilePath = previous.Path; Latitude = previous.Latitude; Longitude = previous.Longitude;
            OnPropertyChanged(nameof(CanUndo));
            return current;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
