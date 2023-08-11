using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using DirectoryMonitor.Business.Data;
using DirectoryMonitor.Business.Drive;
using DirectoryMonitor.Business.Events;

namespace DirectoryMonitor.Business
{
	// TODO: Add IDisposable.
	public class FileSizeDirectoryMonitor : IFileSizeDirectoryMonitor
	{
		#region Fields

		private bool _isScanning = false;

		// TODO: Move to parameters.
		private int _fileSize = 10485760;

		private DirectoryScanner<DirectoryData>? _directoryScanner;

		private ConcurrentDictionary<string, DirectoryData>? _resultDictionary;

		private ManualResetEvent _changesWorkerShutdownEvent = new ManualResetEvent(false);
		private ManualResetEvent _changesWorkerStartEvent = new ManualResetEvent(false);
		private Thread? _changesWorker;
		private ConcurrentDictionary<string, FileSystemWatcher> _fileSystemWatchers = new ConcurrentDictionary<string, FileSystemWatcher>();
		private ConcurrentQueue<string> _changesPendingDirectories = new ConcurrentQueue<string>();

		#endregion

		#region Properties

		public event EventHandler<DirectoryDataEventArgs>? DirectoryData;

		#endregion

		#region Methods
		public async Task Scan(string rootPath)
		{
			Stop();

			_isScanning = true;

			_resultDictionary = new ConcurrentDictionary<string, DirectoryData>();

			_changesWorkerStartEvent.Reset();
			_changesWorkerShutdownEvent.Reset();

			_changesWorker = new Thread(SyncDirectoryChanges);
			_changesWorker.Start();

			_directoryScanner = new DirectoryScanner<DirectoryData>();
			await _directoryScanner.Scan(rootPath, ScanProcessDirectory);

			_isScanning = false;

			_changesWorkerStartEvent.Set();
		}

		public void Stop()
		{
			_isScanning = false;

			_directoryScanner?.Stop();

			foreach (var watcher in _fileSystemWatchers)
			{
				watcher.Value.Dispose();
			}

			_fileSystemWatchers.Clear();

			_changesPendingDirectories.Clear();
			_changesWorkerStartEvent.Set();
			_changesWorkerShutdownEvent.Set();
			_changesWorker?.Join();
		}

		public void Suspend()
		{
			if (_directoryScanner == null)
			{
				throw new InvalidOperationException("DirectoryScanner is null.");
			}

			_isScanning = false;

			_directoryScanner.Suspend();
			_changesWorkerStartEvent.Set();
		}

		public async Task Resume()
		{
			if (_directoryScanner == null)
			{
				throw new InvalidOperationException("DirectoryScanner is null.");
			}

			_isScanning = true;

			_changesWorkerStartEvent.Reset();
			await _directoryScanner.Resume();

			_isScanning = false;
		}

		protected virtual void OnDirectoryData(DirectoryDataEventArgs e)
		{
			EventHandler<DirectoryDataEventArgs> handler = DirectoryData;
			if (handler != null)
			{
				handler(this, e);
			}
		}

		#endregion

		#region Private methods

		private DirectoryData? ScanProcessDirectory(DirectoryInfo directoryInfo, FileInfo[] files, DirectoryIterationData<DirectoryData> directoryIterationData)
		{
			var currentDirectoryTotalFileSize = files.Sum(f => f.Length);

			var currentParentData = directoryIterationData.Data;

			while (currentParentData != null)
			{
				LockFreeUpdate(ref currentParentData, (d) =>
				{
					d.TotalFileSize += currentDirectoryTotalFileSize;
					d.TotalFileCount += files.Length;

					return d;
				});

				currentParentData = currentParentData.Parent;
			}

			if (files.Any(f => f.Length >= _fileSize))
			{
				return GetNewDirectoryData(directoryIterationData.Path, files.Length, currentDirectoryTotalFileSize, directoryIterationData.Data);
			}

			return null;
		}

		private DirectoryData? WatcherProcessDirectory(DirectoryInfo directoryInfo, FileInfo[] files, DirectoryIterationData<DirectoryData> directoryIterationData)
		{
			var currentDirectoryTotalFileSize = files.Sum(f => f.Length);

			var fileSizeDelta = currentDirectoryTotalFileSize;
			var fileCountDelta = files.Length;

			if (_resultDictionary.TryGetValue(directoryIterationData.Path, out var existingDirectoryData))
			{
				fileSizeDelta -= existingDirectoryData.FileSize;
				fileCountDelta -= existingDirectoryData.FileCount;

				existingDirectoryData.FileSize = currentDirectoryTotalFileSize;
				existingDirectoryData.FileCount = files.Length;
			}

			var currentParentData = directoryIterationData.Data;

			while (currentParentData != null)
			{
				LockFreeUpdate(ref currentParentData, (d) =>
				{
					d.TotalFileSize += fileSizeDelta;
					d.TotalFileCount += fileCountDelta;

					return d;
				});

				currentParentData = currentParentData.Parent;
			}

			if (files.Any(f => f.Length >= _fileSize))
			{
				if (existingDirectoryData != null)
				{
					existingDirectoryData.TotalFileSize += fileSizeDelta;
					existingDirectoryData.TotalFileCount += fileCountDelta;

					return existingDirectoryData;
				}

				return GetNewDirectoryData(directoryIterationData.Path, files.Length, currentDirectoryTotalFileSize, directoryIterationData.Data);
			}
			else if (existingDirectoryData != null)
			{
				OnDirectoryData(new DirectoryDataEventArgs(existingDirectoryData, DirectoryDataEventType.Delete));
			}

			return null;
		}

		private DirectoryData GetNewDirectoryData(string path, int filesCount, long filesSize, DirectoryData? parent)
		{
			var directoryData = new DirectoryData
			{
				Path = path,
				FileCount = filesCount,
				FileSize = filesSize,
				TotalFileCount = filesCount,
				TotalFileSize = filesSize,
				Parent = parent
			};

			OnDirectoryData(new DirectoryDataEventArgs(directoryData, DirectoryDataEventType.Add));

			if (_resultDictionary.TryAdd(directoryData.Path, directoryData))
			{
				if (directoryData.Parent == null)
				{
					var fileWatcher = new FileSystemWatcher(directoryData.Path);

					if (_fileSystemWatchers.TryAdd(path, fileWatcher))
					{
						fileWatcher.EnableRaisingEvents = true;
						fileWatcher.IncludeSubdirectories = true;

						fileWatcher.NotifyFilter = NotifyFilters.Size | NotifyFilters.FileName | NotifyFilters.DirectoryName;

						fileWatcher.Created += FileWatcher_Changed;
						fileWatcher.Changed += FileWatcher_Changed;
						fileWatcher.Deleted += FileWatcher_Changed;
					}
					else
					{
						fileWatcher.Dispose();
					}
				}
			}

			return directoryData;
		}

		private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			if (e.ChangeType == WatcherChangeTypes.Deleted)
			{
				_deleteActions?.Post(e.FullPath);

				return;
			}

			if (!_changesPendingDirectories.Contains(e.FullPath))
			{
				_changesPendingDirectories.Enqueue(e.FullPath);

				if (!_isScanning)
				{
					_changesWorkerStartEvent.Set();
				}
			}
		}

		private ActionBlock<string>? _deleteActions;

		private bool IsDirectory(string path)
		{
			return Directory.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.Directory);
		}

		private async void SyncDirectoryChanges()
		{
			_deleteActions = new ActionBlock<string>(ProcessDelete);

			while (true)
			{
				_changesWorkerStartEvent.WaitOne(Timeout.Infinite);

				if (_changesWorkerShutdownEvent.WaitOne(0))
				{
					break;
				}

				while (_changesPendingDirectories.TryDequeue(out var currentDirectory))
				{
					_changesWorkerStartEvent.WaitOne(Timeout.Infinite);

					var directoryName = IsDirectory(currentDirectory) ? currentDirectory : Path.GetDirectoryName(currentDirectory);

					if (directoryName == null || !Directory.Exists(directoryName))
					{
						continue;
					}

					await Task.Run(async () =>
					{
						try
						{
							var parentDirectoryData = GetParentDirectoryData(directoryName);

							var directoryIterationData = new DirectoryIterationData<DirectoryData>
							{
								Path = directoryName,
								Data = parentDirectoryData
							};

							var directoryScanner = new DirectoryScanner<DirectoryData>();
							await directoryScanner.Scan(directoryIterationData, WatcherProcessDirectory);
						}
						catch (Exception ex)
						{
							// TODO: Log error.
						}
					});
				}

				_changesWorkerStartEvent.Reset();
			}

			_deleteActions.Complete();
		}

		private void ProcessDelete(string path)
		{
			if (_resultDictionary.TryGetValue(path, out var directoryData))
			{
				OnDirectoryData(new DirectoryDataEventArgs(directoryData, DirectoryDataEventType.Delete));

				_resultDictionary.TryRemove(path, out var _);

				if (directoryData.Parent == null)
				{
					return;
				}

				var currentParentData1 = directoryData.Parent;

				while (currentParentData1 != null)
				{
					LockFreeUpdate(ref currentParentData1, (d) =>
					{
						d.TotalFileSize -= directoryData.TotalFileSize;
						d.TotalFileCount -= directoryData.TotalFileCount;

						return d;
					});

					currentParentData1 = currentParentData1.Parent;
				}

				return;
			}
			
			var parentDirectoryData2 = GetParentDirectoryData(path);

			if (parentDirectoryData2 != null)
			{
				_changesPendingDirectories.Enqueue(parentDirectoryData2.Path);

				if (!_isScanning)
				{
					_changesWorkerStartEvent.Set();
				}
			}
		}

		private DirectoryData? GetParentDirectoryData(string path)
		{
			if (_resultDictionary == null || _resultDictionary.Count == 0)
			{
				return null;
			}

			var currentPath = path;
			var rootPath = Path.GetPathRoot(currentPath);

			while (PathX.TryGetParentPath(currentPath, out var parentPath))
			{
				if (_resultDictionary.TryGetValue(parentPath, out var parentDirectoryData))
				{
					return parentDirectoryData;
				}

				if (rootPath == parentPath)
				{
					return null;
				}

				currentPath = parentPath;
			}

			return null;
		}

		private static void LockFreeUpdate<T>(ref T field, Func<T, T> updateFunction) where T : class
		{
			var spinWait = new SpinWait();

			while (true)
			{
				T snapshot1 = field;
				T calc = updateFunction(snapshot1);

				T snapshot2 = Interlocked.CompareExchange(ref field, calc, snapshot1);

				if (snapshot1 == snapshot2)
				{
					return;
				}

				spinWait.SpinOnce();
			}
		}

		#endregion
	}
}
