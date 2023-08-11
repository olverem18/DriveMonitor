using System.Management;

namespace DirectoryMonitor.Business.Drive
{
	public static class PathX
	{
		public static bool TryGetParentPath(string path, out string? parentPath)
		{
			parentPath = null;

			if (string.IsNullOrEmpty(path))
			{
				return false;
			}

			var lastDirectoryIndex = path.LastIndexOf(Path.DirectorySeparatorChar);

			if (lastDirectoryIndex == -1)
			{
				return false;
			}

			parentPath = path.Substring(0, lastDirectoryIndex);

			if (parentPath.EndsWith(':'))
			{
				parentPath = $"{parentPath}{Path.DirectorySeparatorChar}";
			}

			return true;
		}

		public static DriveType GetDriveType(string path)
		{
			try
			{
				var rootPath = Path.GetPathRoot(path);

				if (string.IsNullOrEmpty(rootPath))
				{
					return DriveType.None;
				}

				rootPath = rootPath[0].ToString();

				var scope = new ManagementScope(@"\\.\root\microsoft\windows\storage");
				scope.Connect();

				using var partitionSearcher = new ManagementObjectSearcher($"select * from MSFT_Partition where DriveLetter='{rootPath}'");
				partitionSearcher.Scope = scope;

				var partitions = partitionSearcher.Get();

				if (partitions.Count == 0)
				{
					return DriveType.None;
				}

				string? diskNumber = null;

				foreach (var currentPartition in partitions)
				{
					diskNumber = currentPartition["DiskNumber"].ToString();

					if (!string.IsNullOrEmpty(diskNumber))
					{
						break;
					}
				}

				if (string.IsNullOrEmpty(diskNumber))
				{
					return DriveType.None;
				}

				using var diskSearcher = new ManagementObjectSearcher($"SELECT * FROM MSFT_PhysicalDisk WHERE DeviceId='{diskNumber}'");
				diskSearcher.Scope = scope;

				var physicakDisks = diskSearcher.Get();

				if (physicakDisks.Count == 0)
				{
					return DriveType.None;
				}

				foreach (var currentDisk in physicakDisks)
				{
					var mediaType = Convert.ToInt16(currentDisk["MediaType"]);

					switch (mediaType)
					{
						case 3:
							return DriveType.Hdd;
						case 4:
							return DriveType.Ssd;
						default:
							return DriveType.None;
					}	
				}

				return DriveType.None;
			}
			catch (Exception ex)
			{
				// TODO: Log error.

				return DriveType.None;
			}
		}
	}
}
