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
            if (drive == null) return;

            TitleText.Text = drive.Label;
            DriveTypeText.Text = drive.DriveType + " · " + drive.MountPath;
            HealthText.Text = drive.HealthStatus;
            StorageText.Text = FormatSize(drive.UsedBytes) + " / " +
                                 FormatSize(drive.TotalBytes);
            StorageBar.Value = drive.UsedPercent;

            var folders = DatabaseHelper.GetFoldersByDrive(_driveId)
    .Where(f => f.IsTopLevel)
    .ToList();

            // ✅ Folder count
            FolderCountText.Text = $"{folders.Count} folders";
            // ✅ FolderListViewModel — matches x:DataType in XAML
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

            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Width = 300
            };
            var statusText = new TextBlock
            {
                Text = "Starting...",
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                MaxWidth = 300
            };
            // ✅ ThemeResource — no Colors.Gray
            var percentText = new TextBlock
            {
                Text = "0%",
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = (Brush)Application.Current
                    .Resources["TextFillColorSecondaryBrush"]
            };

            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(progressBar);
            panel.Children.Add(statusText);
            panel.Children.Add(percentText);

            var dialog = new ContentDialog
            {
                Title = $"Indexing {drive.Label}...",
                Content = panel,
                XamlRoot = XamlRoot
            };

            _watcher.IndexProgress += (current, total, folderName) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    var pct = total > 0 ? (current * 100.0 / total) : 0;
                    progressBar.Value = pct;
                    statusText.Text = $"Indexing: {folderName}";
                    percentText.Text = $"{current} of {total} ({pct:F0}%)";
                });
            };

            var indexTask = Task.Run(() => _watcher.IndexDriveFull(drive));
            dialog.ShowAsync();
            await indexTask;
            dialog.Hide();
            LoadData();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1_099_511_627_776) return $"{bytes / 1_099_511_627_776.0:F1} TB";
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            return $"{bytes / 1024.0:F1} KB";
        }
    }
}