using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DirectoryMonitor.Business;
using DirectoryMonitor.Business.Data;
using DirectoryMonitor.Business.Events;

namespace DirectoryMonitor.Client
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		// TODO: Move logic into ViewModels.

		private bool _isScanning;
		private bool _isSuspended;
		private bool _isStoped;

		private string _selectedDrive;

		private Timer _updateTimer;

		private FileSizeDirectoryMonitor _monitor;

		private ObservableCollection<DirectoryData> Directories;

		private Stopwatch _stopwatch = new Stopwatch();

		public MainWindow()
		{
			InitializeComponent();

			InitializeDrives();

			Directories = new ObservableCollection<DirectoryData>();

			ListViewDirectories.ItemsSource = Directories;
		}

		private void InitializeDrives()
		{
			var drives = DriveInfo.GetDrives();

			_selectedDrive = drives[0].Name;

			foreach (var drive in drives)
			{
				CmbDrives.Items.Add(drive.Name);
			}

			CmbDrives.SelectedValue = _selectedDrive;

			CmbDrives.SelectionChanged += (s, e) => {
				_selectedDrive = (string)CmbDrives.SelectedValue;
			};
		}

		private void InitializeMonitor()
		{
			if (_monitor == null)
			{
				_monitor = new FileSizeDirectoryMonitor();

				_monitor.DirectoryData += (s, e) =>
				{
					Dispatcher.BeginInvoke(new Action(() =>
					{
						if (_isStoped)
						{
							return;
						}

						switch (e.Type)
						{
							case DirectoryDataEventType.Add:
								Directories.Add(e.DirectoryData);
								break;
							case DirectoryDataEventType.Delete:
								Directories.Remove(e.DirectoryData);
								break;
						}
					}));
				};

				_updateTimer = new Timer(_ =>
				{
					if (!_isScanning || _isSuspended)
					{
						return;
					}

					Dispatcher.BeginInvoke(new Action(() =>
					{
						TxtStatus.Text = $"Scanning... Elapsed {_stopwatch.Elapsed:mm\\:ss}";
					}));
				}, null, 0, 1000);
			}
		}

		private void BtnStart_Click(object sender, RoutedEventArgs e)
		{
			if (_isScanning && !_isSuspended)
			{
				Suspend();

				return;
			}

			if (_isSuspended)
			{
				Resume();

				return;
			}

			Scan();
		}

		private void Scan()
		{
			Directories.Clear();

			_isScanning = true;
			_isStoped = false;

			_stopwatch.Restart();

			TxtStatus.Text = "Scanning...";
			BtnStart.Content = "Suspend";

			new TaskFactory().StartNew(async () =>
			{
				InitializeMonitor();

				await _monitor.Scan(_selectedDrive);

				_stopwatch.Stop();

				await Dispatcher.BeginInvoke(new Action(() =>
				{
					if (_isSuspended)
					{
						TxtStatus.Text = $"Suspended. Elapsed {_stopwatch.Elapsed}";
					}
					else
					{
						_isScanning = false;

						BtnStart.Content = "Start";
						TxtStatus.Text = _isStoped ? "" : $"Finished by {_stopwatch.Elapsed}";
					}
				}));
			},
			TaskCreationOptions.LongRunning);
		}

		private void Suspend()
		{
			_isSuspended = true;

			BtnStart.Content = "Resume";

			_stopwatch.Stop();
			_monitor.Suspend();
		}

		public void Resume()
		{
			_isSuspended = false;

			TxtStatus.Text = "Scanning...";
			BtnStart.Content = "Suspend";

			_stopwatch.Start();

			new TaskFactory().StartNew(async () =>
			{
				await _monitor.Resume();

				await Dispatcher.BeginInvoke(new Action(() =>
				{
					if (_isSuspended)
					{
						TxtStatus.Text = $"Suspended. Elapsed {_stopwatch.Elapsed}";
					}
					else
					{
						_isScanning = false;

						BtnStart.Content = "Start";
						TxtStatus.Text = _isStoped ? "" : $"Finished by {_stopwatch.Elapsed}";
					}
				}));
			},
			TaskCreationOptions.LongRunning);
		}

		private void ButtonStop_Click(object sender, RoutedEventArgs e)
		{
			_isScanning = false;
			_isSuspended = false;
			_isStoped = true;

			_monitor?.Stop();

			TxtStatus.Text = "";
			BtnStart.Content = "Start";
			Directories.Clear();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			_monitor?.Stop();

			base.OnClosing(e);
		}
	}
}
