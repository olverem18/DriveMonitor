using DirectoryMonitor.Business.Data;

namespace DirectoryMonitor.Business
{
	public interface IDirectoryScanner<T> where T : class
	{
		bool IsFinished { get; }

		Task Scan(DirectoryIterationData<T> directoryIterationData, Func<DirectoryInfo, FileInfo[], DirectoryIterationData<T>, T> processDirectory);
		
		Task Scan(string rootPath, Func<DirectoryInfo, FileInfo[], DirectoryIterationData<T>, T> processDirectory);
		
		void Stop();
		
		void Suspend();

		Task Resume();
	}
}