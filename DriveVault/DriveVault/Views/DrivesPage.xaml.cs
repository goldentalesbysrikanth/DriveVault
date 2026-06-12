using DriveVault.Data;
using DriveVault.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DriveVault.Views
{
    public sealed partial class DrivesPage : Page
    {
        private DriveWatcher _watcher = App.DriveWatcher;
        private string? _selectedDriveId;
        private List<Drive> _allDrives = new();
        private string _currentFilter = "All";
        private string _currentSort = "Name (A-Z)";

        public DrivesPage()
        {
            this.InitializeComponent();
            SortCombo.ItemsSource = new List<string>
            {
                "Name (A-Z)",
                "More available space",
                "Less available space",
                "Last connected"
            };
            SortCombo.SelectedIndex = 0;
            DrivesListView.Visibility = Visibility.Collapsed;
            DrivesGridView.Visibility = Visibility.Visible;
            LoadData();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadData();
        }

        public void LoadData()
        {
            _allDrives = DatabaseHelper.GetAllDrives();
            ApplyFilterAndSort();
        }

        private void ApplyFilterAndSort()
        {
            var filtered = _currentFilter switch
            {
                "Online" => _allDrives.Where(d => d.IsConnected).ToList(),
                "Offline" => _allDrives.Where(d => !d.IsConnected).ToList(),
                "NearlyFull" => _allDrives.Where(d => d.IsNearlyFull).ToList(),
                _ => _allDrives
            };

            var sorted = _currentSort switch
            {
                "More available space" => filtered
                    .OrderByDescending(d => d.FreeBytes).ToList(),
                "Less available space" => filtered
                    .OrderBy(d => d.FreeBytes).ToList(),
                "Last connected" => filtered
                    .OrderByDescending(d => d.LastSeen).ToList(),
                _ => filtered.OrderBy(d => d.Label).ToList()
            };

            var online = _allDrives.Count(d => d.IsConnected);
            var offline = _allDrives.Count(d => !d.IsConnected);
            DriveCountText.Text =
                $"{_allDrives.Count} total · {online} online · {offline} offline";

            var folders = DatabaseHelper.GetAllFolders();

            // ✅ NO SolidColorBrush in ViewModel — converters handle in XAML
            var viewModels = sorted.Select(d =>
            {
                var shootCount = folders.Count(f => f.DriveId == d.Id);
                return new DriveItemViewModel
                {
                    DriveId = d.Id,
                    Label = d.Label,
                    DriveType = $"{d.DriveType} · {d.MountPath}",
                    SubInfo = $"{d.DriveType} · {d.MountPath} · {d.SerialNumber}",
                    UsedDisplay = FormatSize(d.UsedBytes) + " / " + FormatSize(d.TotalBytes),
                    FreeDisplay = FormatSize(d.FreeBytes) + " free",
                    PercentDisplay = $"{d.UsedPercent:F0}%",
                    UsedPercent = d.UsedPercent,
                    ShootsCount = $"{shootCount} shoots",
                    HealthStatus = d.HealthStatus,
                    OnlineStatus = d.IsConnected ? "Online" : "Offline",
                    LastSeenText = d.IsConnected
                        ? "Connected now"
                        : GetLastSeen(d.LastSeen),
                };
            }).ToList();

            DrivesListView.ItemsSource = viewModels;
            DrivesGridView.ItemsSource = viewModels;
            UpdateFilterButtons();
        }

        private void UpdateFilterButtons()
        {
            FilterAllBtn.Style = _currentFilter == "All"
                ? (Style)Application.Current.Resources["AccentButtonStyle"]
                : (Style)Application.Current.Resources["DefaultButtonStyle"];
            FilterOnlineBtn.Style = _currentFilter == "Online"
                ? (Style)Application.Current.Resources["AccentButtonStyle"]
                : (Style)Application.Current.Resources["DefaultButtonStyle"];
            FilterOfflineBtn.Style = _currentFilter == "Offline"
                ? (Style)Application.Current.Resources["AccentButtonStyle"]
                : (Style)Application.Current.Resources["DefaultButtonStyle"];
            FilterNearlyFullBtn.Style = _currentFilter == "NearlyFull"
                ? (Style)Application.Current.Resources["AccentButtonStyle"]
                : (Style)Application.Current.Resources["DefaultButtonStyle"];
        }

        private void FilterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string filter)
            {
                _currentFilter = filter;
                ApplyFilterAndSort();
            }
        }

        private void SortCombo_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (SortCombo.SelectedItem is string sort)
            {
                _currentSort = sort;
                ApplyFilterAndSort();
            }
        }

        private void GridViewToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (DrivesListView == null) return;
            DrivesListView.Visibility = Visibility.Collapsed;
            DrivesGridView.Visibility = Visibility.Visible;
        }

        private void GridViewToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (DrivesListView == null) return;
            DrivesListView.Visibility = Visibility.Visible;
            DrivesGridView.Visibility = Visibility.Collapsed;
        }

        private void DrivesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is DriveItemViewModel vm)
                Frame.Navigate(typeof(DriveDetailPage), vm.DriveId);
        }

        private void DrivesGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is DriveItemViewModel vm)
                Frame.Navigate(typeof(DriveDetailPage), vm.DriveId);
        }

        private void Drives_RightTapped(object sender,
            RightTappedRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement el &&
                el.DataContext is DriveItemViewModel vm)
            {
                _selectedDriveId = vm.DriveId;
                var menu = (MenuFlyout)Resources["DriveContextMenu"];
                menu.ShowAt(el, e.GetPosition(el));
            }
        }

        private async void ReindexMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDriveId == null) return;
            await ReindexDrive(_selectedDriveId);
        }

        private async void RemoveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedDriveId == null) return;
            var drive = DatabaseHelper.GetAllDrives()
                .FirstOrDefault(d => d.Id == _selectedDriveId);
            if (drive == null) return;

            var dialog = new ContentDialog
            {
                Title = "Remove Drive",
                Content = $"Remove '{drive.Label}' from Drive Vault?\nThis will not delete any files.",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                _watcher.MarkDriveRemoved(drive.MountPath);
                // ✅ Drive removed log
                DatabaseHelper.LogActivity(
                    "drive_removed", drive.Id, "",
                    drive.Label, drive.Label);
                DatabaseHelper.RemoveDrive(_selectedDriveId);
                LoadData();
            }
        }

        private async void ReindexAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var drive in DatabaseHelper.GetAllDrives()
                .Where(d => d.IsConnected))
                await ReindexDrive(drive.Id);
        }

        public async Task ReindexDrive(string driveId)
        {
            var drive = DatabaseHelper.GetAllDrives()
                .FirstOrDefault(d => d.Id == driveId);
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
            // ✅ ThemeResource — Color.FromArgb కాదు
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

        private static string GetLastSeen(DateTime lastSeen)
        {
            var diff = DateTime.Now - lastSeen;
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return lastSeen.ToString("MMM dd, yyyy");
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1_099_511_627_776) return $"{bytes / 1_099_511_627_776.0:F1} TB";
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            return $"{bytes / 1024.0:F1} KB";
        }
    }

    // ✅ NO SolidColorBrush — converters handle brushes in XAML
    public class DriveItemViewModel
    {
        public string DriveId { get; set; } = "";
        public string Label { get; set; } = "";
        public string DriveType { get; set; } = "";
        public string SubInfo { get; set; } = "";
        public string UsedDisplay { get; set; } = "";
        public string FreeDisplay { get; set; } = "";
        public string PercentDisplay { get; set; } = "";
        public double UsedPercent { get; set; }
        public string ShootsCount { get; set; } = "";
        public string HealthStatus { get; set; } = "";
        public string OnlineStatus { get; set; } = "";
        public string LastSeenText { get; set; } = "";
    }
}