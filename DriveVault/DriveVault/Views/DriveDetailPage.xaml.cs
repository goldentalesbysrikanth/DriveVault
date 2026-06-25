using DriveVault.Data;
using DriveVault.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DriveVault.Views
{
    public sealed partial class DriveDetailPage : Page
    {
        private string _driveId = "";
        private DriveWatcher _watcher = App.DriveWatcher;

        public DriveDetailPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string driveId)
            {
                _driveId = driveId;
                LoadData();
            }
        }

        public void LoadData()
        {
            var drives = DatabaseHelper.GetAllDrives();
            var drive = drives.FirstOrDefault(d => d.Id == _driveId);

            if (drive == null)
            {
                var oldDrive = DatabaseHelper.GetAllDrives()
                    .FirstOrDefault(d => d.Id == _driveId);
                drive = drives.FirstOrDefault(d =>
                    d.IsConnected && !d.IsFullyIndexed);
            }
            if (drive == null) return;

            _driveId = drive.Id;
            TitleText.Text = drive.Label;
            DriveTypeText.Text = drive.DriveType + " · " + drive.MountPath;
            HealthText.Text = drive.HealthStatus;
            StorageText.Text = FormatSize(drive.UsedBytes) + " / " +
                                 FormatSize(drive.TotalBytes);
            StorageBar.Value = drive.UsedPercent;

            var folders = DatabaseHelper.GetFoldersByDrive(_driveId)
                .Where(f => f.IsTopLevel)
                .ToList();

            FolderCountText.Text = $"{folders.Count} folders";
            FoldersListView.ItemsSource = folders.Select(f => new FolderListViewModel
            {
                FolderId = f.Id,
                FolderName = f.FolderName,
                SizeDisplay = f.SizeDisplay,
                DateAdded = "Added " + f.FirstSeen.ToString("MMM dd, yyyy · hh:mm tt")
            }).ToList();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
        }

        private void FoldersListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            // No action
        }

        private async void ReindexButton_Click(object sender, RoutedEventArgs e)
        {
            var drives = DatabaseHelper.GetAllDrives();
            var drive = drives.FirstOrDefault(d => d.Id == _driveId);
            if (drive == null) return;

            // ✅ Drive connected check — unchanged
            if (!drive.IsConnected)
            {
                await new ContentDialog
                {
                    Title = "Drive Offline",
                    Content = $"\"{drive.Label}\" is not connected.\nPlease connect the drive first.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();
                return;
            }

            // ✅ FIX — removed _watcher.MarkDriveRemoved(drive.MountPath)
            // That line was creating a new drive record on every re-index
            // causing duplicate folders in the list

            // ✅ Use sidebar banner instead of blocking dialog
            App.MainWindow?.ShowIndexingBanner(drive.Label);

            System.Diagnostics.Debug.WriteLine(
                $"ReindexButton: {drive.Label} — " +
                $"Connected: {drive.IsConnected} — " +
                $"Indexed: {drive.IsFullyIndexed}");

            try
            {
                await Task.Run(() => _watcher.IndexDriveFull(drive));
            }
            finally
            {
                App.MainWindow?.HideIndexingBanner();
                LoadData();
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1_099_511_627_776)
                return $"{bytes / 1_099_511_627_776.0:F1} TB";
            if (bytes >= 1_073_741_824)
                return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576)
                return $"{bytes / 1_048_576.0:F1} MB";
            return $"{bytes / 1024.0:F1} KB";
        }
    }
}