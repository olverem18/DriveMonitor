using System.Diagnostics;
using DirectoryMonitor.Business.Events;

namespace DirectoryMonitor.Business.Tests
{
    public class Playground
	{
		[Fact]
		public void GetRelativePath_ReturnsNotNullResult()
		{
			var relativeTo = @"C:\work\test";
			var path = @"C:\work\test\1\2";

			// var expecteRelativePath = @"1\2";

			var relativePath = path.StartsWith(relativeTo);

			Assert.True(relativePath);

			// Assert.Equal(expecteRelativePath, relativePath);
		}

		[Fact]
		public void GetRelativePath_ReturnsNullResult()
		{
			var relativeTo = @"C:\work\test\";
			var path = @"C:\work\test1\1\2";

			// var relativePath = Path.GetRelativePath(relativeTo, path);

			var relativePath = path.StartsWith(relativeTo);

			Assert.False(relativePath);

			// Assert.Null(relativePath);
		}

		[Fact]
		public void GetRelativePath_ReturnsNullResult1()
		{
			var relativeTo = @"C:\work\test\";
			var path = @"C:\work\test1\1\2";

			// var relativePath = Path.GetRelativePath(relativeTo, path);

			var relativePath = path.StartsWith(null ?? "");

			Assert.False(relativePath);

			// Assert.Null(relativePath);
		}

		[Fact]
		public void ScanTest()
		{
			var stopWatch = new Stopwatch();
			stopWatch.Start();

			// DirectoryService.Scan(@"I:\");

			stopWatch.Stop();

			var time = stopWatch.Elapsed;

			var t = 1;
		}

		[Fact]
		public void ScanTest1()
		{
			var stopWatch = new Stopwatch();
			stopWatch.Start();

			var monitor = new FileSizeDirectoryMonitor();

			monitor.DirectoryData += Monitor_DirectoryData;

			// monitor.Scan(@"C:\");

			stopWatch.Stop();

			var time = stopWatch.Elapsed;

			var t = 1;
		}

		[Fact]
		public async Task ScanTest2()
		{
			var stopWatch = new Stopwatch();
			stopWatch.Start();

			//var monitor = new FileSizeDirectoryMonitor();

			//monitor.DirectoryData += Monitor_DirectoryData;

			var count = 0;
			var count1 = 0;

			//await DirectoryHelper.ScanNew(@"C:\", (a, b, e) =>
			//{
			//	count1++;
			//});

			stopWatch.Stop();

			var time1 = stopWatch.Elapsed;
			stopWatch.Restart();

			//DirectoryHelper.Scan(@"C:\", (a, b, e) =>
			//{
			//	count++;
			//});

			stopWatch.Stop();

			var time = stopWatch.Elapsed;

			var t = 1;
		}

		private void Monitor_DirectoryData(object? sender, DirectoryDataEventArgs e)
		{
			//Task.Run(() =>
			//{
			//	Debug.WriteLine($"{e.DirectoryData.Path}: Files size: {e.DirectoryData.TotalFileSize}, Files count: {e.DirectoryData.TotalFileCount}, {(e.DirectoryData.Finished ? "Done" : "In Progress")}.");
			//});
			// Debug.WriteLine($"{e.DirectoryData.Path}: Files size: {e.DirectoryData.TotalFileSize}, Files count: {e.DirectoryData.TotalFileCount}, {(e.DirectoryData.Finished ? "Done" : "In Progress")}.");
		}
	}
}