using DriveVault.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace DriveVault.Views
{
    public sealed partial class ActivityPage : Page
    {
        private int _selectedDays = 7;

        public ActivityPage()
        {
            this.InitializeComponent();
            DaysFilterCombo.ItemsSource = new List<string>
            {
                "Last 1 day",
                "Last 3 days",
                "Last 7 days",
                "Last 30 days",
                "All time"
            };
            DaysFilterCombo.SelectedIndex = 2;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadData();
        }

        public void LoadData()
        {
            var activity = _selectedDays == 0
                ? DatabaseHelper.GetAllActivity()
                : GetFilteredActivity(_selectedDays);

            var drives = DatabaseHelper.GetAllDrives();

            TotalEventsText.Text = activity.Count.ToString();
            FoldersAddedText.Text = activity.Count(a => a.EventType == "folder_added").ToString();
            FoldersRemovedText.Text = activity.Count(a => a.EventType == "folder_removed").ToString();
            ReindexedText.Text = activity.Count(a => a.EventType == "drive_reindexed").ToString();

            ActivityListView.ItemsSource = activity.Select(a =>
            {
                var drive = drives.FirstOrDefault(d => d.Id == a.DriveId);
                return new ActivityItemViewModel
                {
                    Description = GetDescription(a),
                    SubInfo = GetSubInfo(a),
                    DriveName = drive?.Label ?? a.DriveName,
                    Timestamp = a.Timestamp.ToString("MMM dd, yyyy · hh:mm tt"),
                    EventType = a.EventType,
                };
            }).ToList();
        }

        // ✅ Screen 1 — All events, filtered by days
        private static List<ActivityLog> GetFilteredActivity(int days)
        {
            var all = DatabaseHelper.GetAllActivity();
            if (days == 0) return all;
            var since = DateTime.Now.AddDays(-days);
            return all.Where(a => a.Timestamp >= since).ToList();
        }

        private static string GetDescription(ActivityLog a) => a.EventType switch
        {
            "folder_added" => $"📁 Added: {a.FolderName}",
            "folder_removed" => $"🗑 Removed: {a.FolderName}",
            "files_added" => $"➕ Files added in: {a.FolderName}",
            "files_removed" => $"➖ Files removed from: {a.FolderName}",
            "drive_reindexed" => $"⟳ Re-indexed: {a.DriveName}",
            "drive_auto_indexed" => $"🔄 Auto indexed: {a.DriveName}",
            "drive_connected" => $"🔌 Connected: {a.DriveName}",
            "drive_disconnected" => $"⏏ Disconnected: {a.DriveName}",
            "drive_removed" => $"🗑 Removed drive: {a.DriveName}",
            "log_reset" => "⚠️ Activity log was reset",
            "log_exported" => $"⬇ Log exported: {a.FolderName}",
            "app_reset" => "🔄 App data was reset",
            "unauthorized_attempt" => $"🚨 Unauthorized: {a.FolderName}",
            _ => a.FolderName
        };

        private static string GetSubInfo(ActivityLog a)
        {
            if (a.FileCount == 0 && string.IsNullOrEmpty(a.FileTypeSummary))
                return "";

            var parts = new List<string>();
            if (a.FileCount > 0)
                parts.Add($"{a.FileCount:N0} files");

            if (!string.IsNullOrEmpty(a.FileTypeSummary))
                parts.AddRange(a.FileTypeSummary
                    .Split('|').Take(3).Select(t => t.Trim()));

            if (a.SizeBytes > 0)
                parts.Add(a.SizeDisplay);

            return string.Join(" · ", parts);
        }

        private void DaysFilterCombo_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            _selectedDays = DaysFilterCombo.SelectedIndex switch
            {
                0 => 1,
                1 => 3,
                2 => 7,
                3 => 30,
                4 => 0,
                _ => 7
            };
            LoadData();
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await VerifyPassword("export")) return;

            var activity = DatabaseHelper.GetAllActivity();
            var formatPanel = new StackPanel { Spacing = 8 };
            var csvBtn = new RadioButton { Content = "CSV (Excel)", IsChecked = true };
            var txtBtn = new RadioButton { Content = "TXT (Notepad)" };
            formatPanel.Children.Add(new TextBlock
            {
                Text = "Choose export format:",
                Style = (Style)Application.Current.Resources["BodyTextBlockStyle"]
            });
            formatPanel.Children.Add(csvBtn);
            formatPanel.Children.Add(txtBtn);

            var formatDialog = new ContentDialog
            {
                Title = "Export Activity Log",
                Content = formatPanel,
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            if (await formatDialog.ShowAsync() != ContentDialogResult.Primary) return;

            bool isCsv = csvBtn.IsChecked == true;
            string content = isCsv ? BuildCsvContent(activity) : BuildTxtContent(activity);

            var picker = new FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.SuggestedFileName =
                $"DriveVault_ActivityLog_{DateTime.Now:yyyyMMdd_HHmm}";
            if (isCsv)
                picker.FileTypeChoices.Add("CSV File", new List<string> { ".csv" });
            else
                picker.FileTypeChoices.Add("Text File", new List<string> { ".txt" });

            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            await FileIO.WriteTextAsync(file, content);
            DatabaseHelper.LogActivity(
                "log_exported", "", "", $"Exported to {file.Name}", "System");

            await new ContentDialog
            {
                Title = "Export Complete",
                Content = $"Activity log exported to:\n{file.Path}",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
            LoadData();
        }

        private async void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await VerifyPassword("reset")) return;

            var confirm = new ContentDialog
            {
                Title = "⚠️ Reset Activity Log",
                Content = "This will permanently delete ALL activity history.\nThis action cannot be undone.\n\nAre you sure?",
                PrimaryButtonText = "Yes, Reset",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            DatabaseHelper.ResetActivityLog();
            LoadData();

            await new ContentDialog
            {
                Title = "Reset Complete",
                Content = "Activity log has been reset.\nThis action has been recorded.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
        }

        private async System.Threading.Tasks.Task<bool> VerifyPassword(string action)
        {
            var savedPassword = DatabaseHelper.GetSetting("activity_password", "");
            if (string.IsNullOrEmpty(savedPassword))
            {
                await new ContentDialog
                {
                    Title = "No Password Set",
                    Content = "Please set a password in Settings first.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();
                return false;
            }

            var passBox = new PasswordBox { PlaceholderText = "Enter password", Width = 300 };
            var dialog = new ContentDialog
            {
                Title = action == "export" ? "🔒 Authorize Export" : "🔒 Authorize Reset",
                Content = passBox,
                PrimaryButtonText = "Confirm",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return false;

            if (passBox.Password != savedPassword)
            {
                await new ContentDialog
                {
                    Title = "Access Denied",
                    Content = "Incorrect password.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();
                DatabaseHelper.LogActivity(
                    "unauthorized_attempt", "", "",
                    $"Failed {action} attempt", "System");
                return false;
            }
            return true;
        }

        private static string BuildCsvContent(List<ActivityLog> activity)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Event Type,Folder Name,Drive Name,File Count,File Types,Size");
            foreach (var a in activity)
                sb.AppendLine(
                    $"\"{a.Timestamp:MMM dd, yyyy · hh:mm tt}\"," +
                    $"\"{a.EventType}\"," +
                    $"\"{a.FolderName}\"," +
                    $"\"{a.DriveName}\"," +
                    $"\"{a.FileCount}\"," +
                    $"\"{a.FileTypeSummary}\"," +
                    $"\"{a.SizeDisplay}\"");
            return sb.ToString();
        }

        private static string BuildTxtContent(List<ActivityLog> activity)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine("          DRIVE VAULT — ACTIVITY LOG");
            sb.AppendLine($"          Exported: {DateTime.Now:MMM dd, yyyy · hh:mm tt}");
            sb.AppendLine("═══════════════════════════════════════════════════");
            sb.AppendLine();
            foreach (var a in activity)
            {
                var icon = a.EventType switch
                {
                    "folder_added" => "[ADDED]      ",
                    "folder_removed" => "[REMOVED]    ",
                    "files_added" => "[FILES+]     ",
                    "files_removed" => "[FILES-]     ",
                    "drive_reindexed" => "[REINDEX]    ",
                    "drive_auto_indexed" => "[AUTO-INDEX] ",
                    "drive_connected" => "[CONNECTED]  ",
                    "drive_disconnected" => "[DISCONNECT] ",
                    "drive_removed" => "[DRIVE-DEL]  ",
                    "log_reset" => "[RESET]      ",
                    "log_exported" => "[EXPORT]     ",
                    "app_reset" => "[APP-RESET]  ",
                    "unauthorized_attempt" => "[ALERT]      ",
                    _ => "[INFO]       "
                };
                sb.AppendLine($"{icon} {a.Timestamp:MMM dd, yyyy · hh:mm tt}");
                if (!string.IsNullOrEmpty(a.FolderName))
                    sb.AppendLine($"             Folder : {a.FolderName}");
                if (!string.IsNullOrEmpty(a.DriveName))
                    sb.AppendLine($"             Drive  : {a.DriveName}");
                if (a.FileCount > 0)
                    sb.AppendLine($"             Files  : {a.FileCount:N0}");
                if (!string.IsNullOrEmpty(a.FileTypeSummary))
                    sb.AppendLine($"             Types  : {a.FileTypeSummary.Replace("|", " · ")}");
                if (a.SizeBytes > 0)
                    sb.AppendLine($"             Size   : {a.SizeDisplay}");
                sb.AppendLine("───────────────────────────────────────────────");
            }
            sb.AppendLine();
            sb.AppendLine($"Total records: {activity.Count}");
            return sb.ToString();
        }
    }

    public class ActivityItemViewModel
    {
        public string Description { get; set; } = "";
        public string SubInfo { get; set; } = "";
        public string DriveName { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public string EventType { get; set; } = "";
    }
}