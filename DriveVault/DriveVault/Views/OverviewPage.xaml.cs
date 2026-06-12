using DriveVault.Data;
using DriveVault.Converters;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;

namespace DriveVault.Views
{
    public sealed partial class OverviewPage : Page
    {
        public OverviewPage()
        {
            this.InitializeComponent();
            LoadData();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadData();
        }

        public void LoadData()
        {
            var drives = DatabaseHelper.GetAllDrives();
            var alerts = DatabaseHelper.GetActiveAlerts();

            var folders = DatabaseHelper.GetAllFolders();

            // ✅ Stats cards — show చేయాలి, click వద్దు
            TotalDrivesText.Text = drives.Count.ToString();
            ConnectedDrivesText.Text =
                drives.Count(d => d.IsConnected) + " connected";

            var indexedDrives = drives.Where(d => d.IsFullyIndexed).ToList();
            StorageUsedText.Text =
                FormatSize(indexedDrives.Sum(d => d.TotalBytes));
            StorageTotalText.Text =
                FormatSize(indexedDrives.Sum(d => d.UsedBytes)) + " used";

            TotalFoldersText.Text = folders.Count.ToString();
            var thisMonth = folders.Count(f =>
                f.FirstSeen.Month == DateTime.Now.Month &&
                f.FirstSeen.Year == DateTime.Now.Year);
            ThisMonthText.Text = $"{thisMonth} this month";

            // ✅ Drive Status
            DrivesListView.ItemsSource = drives
                .OrderByDescending(d => d.IsConnected)
                .Take(7)
                .Select(d => new OverviewDriveViewModel
                {
                    DriveId = d.Id,
                    Label = d.Label,
                    UsedDisplay = FormatSize(d.UsedBytes) + " / " +
                                  FormatSize(d.TotalBytes),
                    FreeDisplay = FormatSize(d.FreeBytes) + " free",
                    UsedPercent = d.UsedPercent,
                    OnlineStatus = d.IsConnected ? "Online" : "Offline",
                }).ToList();

            // ✅ Alerts
            AlertsCountText.Text = alerts.Count.ToString();
            if (alerts.Any())
            {
                AlertsDetailText.Text =
                    string.Join(", ", alerts.Select(a => a.Label)) +
                    " nearly full";
                AlertsDetailText.Foreground =
                    (Brush)Application.Current
                        .Resources["SystemFillColorCautionBrush"];
            }
            else
            {
                AlertsDetailText.Text = "All drives healthy ✅";
                AlertsDetailText.Foreground =
                    (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"];
            }

            // ✅ Recent Activity — all event types, limit 5
            var activity = DatabaseHelper.GetAllActivity()
                .Take(5)
                .ToList();

            var activityList = activity.Select(a =>
            {
                var subInfo = "";
                if (a.FileCount > 0)
                    subInfo = $"{a.FileCount:N0} files";
                if (a.SizeBytes > 0)
                    subInfo += (subInfo.Length > 0 ? " · " : "") +
                               a.SizeDisplay;

                return new OverviewActivityViewModel
                {
                    Icon = GetIcon(a.EventType),
                    Description = GetDescription(a),
                    SubInfo = subInfo,
                    TimeAgo = GetTimeAgo(a.Timestamp),
                    EventType = a.EventType,
                };
            }).ToList();

            ActivityListView.ItemsSource = activityList;
            ViewAllEventsBtn.Content =
                $"View all {DatabaseHelper.GetAllActivity().Count} events →";
        }

        private static string GetIcon(string eventType) => eventType switch
        {
            "folder_added" => "📁",
            "folder_removed" => "🗑",
            "drive_reindexed" => "⟳",
            "drive_connected" => "🔌",
            "drive_disconnected" => "⏏",
            "drive_removed" => "🗑",
            "files_added" => "➕",
            "files_removed" => "➖",
            "workflow_attached" => "📋",
            "workflow_removed" => "📋",
            "setting_changed" => "⚙",
            "app_reset" => "🔄",
            "log_reset" => "⚠️",
            "log_exported" => "⬇",
            _ => "ℹ"
        };

        private static string GetDescription(ActivityLog a) => a.EventType switch
        {
            "folder_added" => $"📁 Added: {a.FolderName}",
            "folder_removed" => $"🗑 Removed: {a.FolderName}",
            "files_added" => $"➕ Files added: {a.FolderName}",
            "files_removed" => $"➖ Files removed: {a.FolderName}",
            "drive_connected" => $"🔌 Connected: {a.DriveName}",
            "drive_disconnected" => $"⏏ Disconnected: {a.DriveName}",
            "drive_removed" => $"🗑 Drive removed: {a.DriveName}",
            "drive_reindexed" => $"⟳ Re-indexed: {a.DriveName}",
            "workflow_attached" => $"📋 Workflow: {a.FolderName}",
            "workflow_removed" => $"📋 Workflow removed: {a.FolderName}",
            "setting_changed" => $"⚙ Setting: {a.FolderName}",
            "app_reset" => "🔄 App data reset",
            "log_reset" => "⚠️ Activity log reset",
            "log_exported" => "⬇ Log exported",
            _ => a.FolderName
        };

        private static string GetTimeAgo(DateTime timestamp)
        {
            var diff = DateTime.Now - timestamp;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return timestamp.ToString("MMM dd");
        }

        private async void AlertsCard_Click(object sender,
            PointerRoutedEventArgs e)
        {
            var alerts = DatabaseHelper.GetActiveAlerts();
            if (!alerts.Any())
            {
                await new ContentDialog
                {
                    Title = "No Alerts",
                    Content = "All drives are healthy!",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();
                return;
            }

            var panel = new StackPanel { Spacing = 12 };
            foreach (var drive in alerts)
            {
                var drivePanel = new StackPanel { Spacing = 8 };
                drivePanel.Children.Add(new TextBlock
                {
                    Text = $"⚠️ {drive.Label} — {drive.UsedPercent:F0}% full",
                    Style = (Style)Application.Current
                        .Resources["BodyStrongTextBlockStyle"]
                });

                var snoozePanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8
                };

                foreach (var days in new[] { 1, 2, 3 })
                {
                    var btn = new Button
                    {
                        Content = $"Snooze {days} day{(days > 1 ? "s" : "")}",
                        Tag = $"{drive.Id}|{days}"
                    };
                    btn.Click += (s, args) =>
                    {
                        if (s is Button b && b.Tag is string tag)
                        {
                            var parts = tag.Split('|');
                            if (parts.Length == 2 &&
                                int.TryParse(parts[1], out int d))
                            {
                                DatabaseHelper.SnoozeAlert(parts[0], d);
                                _activeAlertDialog?.Hide();
                                LoadData();
                            }
                        }
                    };
                    snoozePanel.Children.Add(btn);
                }

                drivePanel.Children.Add(snoozePanel);
                panel.Children.Add(drivePanel);
            }

            _activeAlertDialog = new ContentDialog
            {
                Title = $"{alerts.Count} Drive Alert(s)",
                Content = new ScrollViewer
                {
                    Content = panel,
                    MaxHeight = 400
                },
                CloseButtonText = "Close",
                XamlRoot = XamlRoot
            };

            await _activeAlertDialog.ShowAsync();
            _activeAlertDialog = null;
        }

        private ContentDialog? _activeAlertDialog;

        private void DrivesListView_ItemClick(object sender,
            ItemClickEventArgs e)
        {
            if (e.ClickedItem is OverviewDriveViewModel vm)
                Frame.Navigate(typeof(DrivesPage), vm.DriveId);
        }

        private void ViewAllDrives_Click(object sender, RoutedEventArgs e)
            => Frame.Navigate(typeof(DrivesPage));

        private void ViewAllLibrary_Click(object sender, RoutedEventArgs e)
            => Frame.Navigate(typeof(FoldersPage));

        // ✅ Popup — last 1 day, all event types, scrollable
        private async void ViewAllActivity_Click(object sender,
            RoutedEventArgs e)
        {
            // ✅ Last 1 day — all event types
            var since = DateTime.Now.AddDays(-1);
            var activity = DatabaseHelper.GetAllActivity()
                .Where(a => a.Timestamp >= since)
                .ToList();

            var drives = DatabaseHelper.GetAllDrives();
            var panel = new StackPanel { Spacing = 0 };

            if (!activity.Any())
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "No activity in the last 24 hours.",
                    Style = (Style)Application.Current
                        .Resources["BodyTextBlockStyle"],
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"],
                    Margin = new Thickness(0, 8, 0, 8)
                });
            }
            else
            {
                foreach (var a in activity)
                {
                    var drive = drives.FirstOrDefault(d => d.Id == a.DriveId);

                    var row = new Grid
                    { Padding = new Thickness(0, 10, 0, 10) };
                    row.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition
                    { Width = GridLength.Auto });

                    // Icon circle
                    var iconBorder = new Border
                    {
                        Width = 32,
                        Height = 32,
                        CornerRadius = new CornerRadius(16),
                        Background = (Brush)Application.Current
                            .Resources["CardBackgroundFillColorDefaultBrush"],
                        Margin = new Thickness(0, 0, 12, 0)
                    };
                    iconBorder.Child = new TextBlock
                    {
                        Text = GetIcon(a.EventType),
                        FontSize = 14,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(iconBorder, 0);

                    // Description + subinfo
                    var textPanel = new StackPanel
                    {
                        Spacing = 2,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    textPanel.Children.Add(new TextBlock
                    {
                        Text = GetDescription(a),
                        Style = (Style)Application.Current
                            .Resources["BodyTextBlockStyle"]
                    });

                    // ✅ SubInfo — file count + size
                    var subInfo = "";
                    if (a.FileCount > 0)
                        subInfo = $"{a.FileCount:N0} files";
                    if (a.SizeBytes > 0)
                        subInfo += (subInfo.Length > 0 ? " · " : "") +
                                   a.SizeDisplay;
                    if (!string.IsNullOrEmpty(subInfo))
                    {
                        textPanel.Children.Add(new TextBlock
                        {
                            Text = subInfo,
                            Style = (Style)Application.Current
                                .Resources["CaptionTextBlockStyle"],
                            Foreground = (Brush)Application.Current
                                .Resources["TextFillColorSecondaryBrush"]
                        });
                    }

                    Grid.SetColumn(textPanel, 1);

                    // Time
                    var timeText = new TextBlock
                    {
                        Text = GetTimeAgo(a.Timestamp),
                        Style = (Style)Application.Current
                            .Resources["CaptionTextBlockStyle"],
                        Foreground = (Brush)Application.Current
                            .Resources["TextFillColorSecondaryBrush"],
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(8, 0, 0, 0)
                    };
                    Grid.SetColumn(timeText, 2);

                    row.Children.Add(iconBorder);
                    row.Children.Add(textPanel);
                    row.Children.Add(timeText);
                    panel.Children.Add(row);

                    // Divider
                    panel.Children.Add(new Border
                    {
                        BorderBrush = (Brush)Application.Current
                            .Resources["DividerStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(0, 0, 0, 1)
                    });
                }
            }

            await new ContentDialog
            {
                Title =
                    $"Last 24 Hours Activity — {activity.Count} events",
                Content = new ScrollViewer
                {
                    Content = panel,
                    MaxHeight = 500,
                    MinWidth = 460
                },
                CloseButtonText = "Close",
                XamlRoot = XamlRoot
            }.ShowAsync();
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

    public class OverviewDriveViewModel
    {
        public string DriveId { get; set; } = "";
        public string Label { get; set; } = "";
        public string UsedDisplay { get; set; } = "";
        public string FreeDisplay { get; set; } = "";
        public double UsedPercent { get; set; }
        public string OnlineStatus { get; set; } = "";
    }

    public class OverviewActivityViewModel
    {
        public string Icon { get; set; } = "";
        public string Description { get; set; } = "";
        public string SubInfo { get; set; } = "";
        public string TimeAgo { get; set; } = "";
        public string EventType { get; set; } = "";
    }
}