using DirectoryMonitor.Business.Events;

namespace DirectoryMonitor.Business
{
	public interface IFileSizeDirectoryMonitor
	{
		event EventHandler<DirectoryDataEventArgs>? DirectoryData;

		Task Resume();
		Task Scan(string rootPath);
		void Stop();
		void Suspend();
	}
}