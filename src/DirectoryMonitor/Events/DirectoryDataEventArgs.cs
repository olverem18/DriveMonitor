using DirectoryMonitor.Business.Data;

namespace DirectoryMonitor.Business.Events
{
    public class DirectoryDataEventArgs : EventArgs
    {
        public DirectoryDataEventArgs(DirectoryData directoryData, DirectoryDataEventType type)
        {
            DirectoryData = directoryData;
            Type = type;
        }

        public DirectoryData DirectoryData { get; }

        public DirectoryDataEventType Type { get; set; }
    }
}