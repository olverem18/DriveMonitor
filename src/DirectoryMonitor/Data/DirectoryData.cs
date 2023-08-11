using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DirectoryMonitor.Business.Data
{
    public class DirectoryData : INotifyPropertyChanged
    {
        private int _totalFileCount;
        private long _totalFileSize;

        public string Path { get; set; }

        public int TotalFileCount
        {
            get { return _totalFileCount; }
            set
            {
                if (_totalFileCount != value)
                {
                    _totalFileCount = value;
                    OnPropertyChanged("TotalFileCount");
                }
            }
        }

        public long TotalFileSize
        {
            get { return _totalFileSize; }
            set
            {
                if (_totalFileSize != value)
                {
                    _totalFileSize = value;
                    OnPropertyChanged("TotalFileSize");
                }
            }
        }

        public int FileCount { get; set; }

        public long FileSize { get; set; }

        public DirectoryData? Parent { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}