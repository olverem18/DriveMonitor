using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using DirectoryMonitor.Business.Data;
using DirectoryMonitor.Business.Drive;
using DriveType = DirectoryMonitor.Business.Drive.DriveType;

namespace DirectoryMonitor.Business
{
	public class DirectoryScanner<T> : IDirectoryScanner<T> where T : class
	{
		private bool _suspended;

		private DriveType _driveType;

		private Stack<DirectoryIterationData<T>>? _directoryStack;
		private ConcurrentStack<DirectoryIterationData<T>>? _directoryStackAsync;
		private Func<DirectoryInfo, FileInfo[], DirectoryIterationData<T>, T>? _processDirectory;

		public bool IsFinished => _directoryStack == null || _directoryStack.Count == 0;

		public async Task Scan(string rootPath, Func<DirectoryInfo, FileInfo[], DirectoryIterationData<T>, T> processDirectory)
		{
			if (rootPath == null)
			{
				throw new ArgumentNullException(nameof(rootPath));
			}

			if (string.IsNullOrWhiteSpace(rootPath))
			{
				throw new ArgumentException("Incorrect parameter.", nameof(rootPath));
			}

			if (!Directory.Exists(rootPath))
			{
				throw new ArgumentException($"Cannot find directory '{rootPath}'.", nameof(rootPath));
			}

			_directoryStack = new Stack<DirectoryIterationData<T>>();
			_processDirectory = processDirectory;

			_driveType = PathX.GetDriveType(rootPath);

			await ProcessDirectoryStack(
				new[]
				{
					new DirectoryIterationData<T>
					{
						Path = rootPath
					}
				},
				_processDirectory);
		}

		public async Task Scan(DirectoryIterationData<T> directoryIterationData, Func<DirectoryInfo, FileInfo[], DirectoryIterationData<T>, T> processDirectory)
		{
			if (directoryIterationData == null)
			{
				throw new ArgumentNullException(nameof(directoryIterationData));
			}

			if (string.IsNullOrWhiteSpace(directoryIterationData.Path))
			{
				throw new ArgumentException("Incorrect parameter.", nameof(directoryIterationData.Path));
			}

			if (!Directory.Exists(directoryIterationData.Path))
			{
				throw new ArgumentException($"Cannot find directory '{directoryIterationData.Data}'.", nameof(directoryIterationData.Path));
			}

			_directoryStack = new Stack<DirectoryIterationData<T>>();
			_processDirectory = processDirectory;

			_driveType = PathX.GetDriveType(directoryIterationData.Path);

			await ProcessDirectoryStack(
				new[]
				{
					directoryIterationData
				},
				_processDirectory);
		}

		public void Stop()
		{
			_suspended = true;
		}

		public void Suspend()
		{
			_directoryStackAsync = new ConcurrentStack<DirectoryIterationData<T>>();

			_suspended = true;
		}

		public async Task Resume()
		{
			_suspended = false;

			await ProcessDirectoryStack(_directoryStackAsync, _processDirectory);
		}

		private async Task ProcessDirectoryStack(IEnumerable<DirectoryIterationData<T>> directories, Func<DirectoryInfo, FileInfo[], DirectoryIterationData<T>, T> processDirectory)
		{
			if (directories == null)
			{
				return;
			}

			var remainingDirectories = 0;

			ActionBlock<DirectoryIterationData<T>> scanDirectoryBlock = null;

			scanDirectoryBlock = new ActionBlock<DirectoryIterationData<T>>((currentDirectoryIterationData) =>
			{
				try
				{
					if (_suspended)
					{
						_directoryStackAsync?.Push(currentDirectoryIterationData);

						return;
					}

					var directoryInfo = new DirectoryInfo(currentDirectoryIterationData.Path);

					var files = directoryInfo.GetFiles();

					var iterationData = processDirectory(directoryInfo, files, currentDirectoryIterationData);

					var subDirectories = directoryInfo.GetDirectories();

					foreach (var subDirectory in subDirectories)
					{
						var directoryData = new DirectoryIterationData<T>
						{
							Path = subDirectory.FullName,
							Data = iterationData
						};

						scanDirectoryBlock?.Post(directoryData);
						Interlocked.Increment(ref remainingDirectories);
					}
				}
				catch
				{
					// TODO: Log Error.
				}
				finally
				{
					Interlocked.Decrement(ref remainingDirectories);
				}
			}, new ExecutionDataflowBlockOptions
			{
				MaxDegreeOfParallelism = _driveType == DriveType.Ssd ? (Environment.ProcessorCount / 2) : 1
			});

			Parallel.ForEach(directories, (currentDirectory) =>
			{
				Interlocked.Increment(ref remainingDirectories);

				scanDirectoryBlock.Post(currentDirectory);
			});

			while (remainingDirectories > 0)
			{
				await Task.Delay(100);
			}
		}
	}
}
