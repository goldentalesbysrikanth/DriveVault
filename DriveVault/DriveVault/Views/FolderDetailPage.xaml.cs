using DriveVault.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DriveVault.Views
{
    public sealed partial class FolderDetailPage : Page
    {
        private string _folderId = "";

        public FolderDetailPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is string folderId)
            {
                _folderId = folderId;
                LoadData();
            }
        }

        private void LoadData()
        {
            var folders = DatabaseHelper.GetAllFolders();
            var folder = folders.FirstOrDefault(f => f.Id == _folderId);
            if (folder == null) return;

            // Title
            TitleText.Text = folder.FolderName;
            SizeText.Text = folder.SizeDisplay;
            DateText.Text = folder.FirstSeen.ToString("MMM dd, yyyy");

            // Drive name
            var drives = DatabaseHelper.GetAllDrives();
            var drive = drives.FirstOrDefault(d => d.Id == folder.DriveId);
            DriveText.Text = drive?.Label ?? "Unknown";

            // Subfolders
            var subfolders = new List<SubfolderViewModel>();
            try
            {
                if (Directory.Exists(folder.FolderPath))
                {
                    var dirs = Directory.GetDirectories(folder.FolderPath);
                    foreach (var dir in dirs)
                    {
                        try
                        {
                            var size = GetFolderSize(dir);
                            subfolders.Add(new SubfolderViewModel
                            {
                                Name = Path.GetFileName(dir),
                                Size = FormatSize(size)
                            });
                        }
                        catch { }
                    }
                }
            }
            catch { }

            if (subfolders.Any())
                SubfoldersListView.ItemsSource = subfolders
                    .OrderByDescending(s => s.SizeBytes).ToList();
            else
                SubfoldersListView.ItemsSource = new List<SubfolderViewModel>
                {
                    new SubfolderViewModel
                    {
                        Name = "No subfolders found",
                        Size = ""
                    }
                };
        }

        private void BackButton_Click(object sender,
            Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
        }

        private static long GetFolderSize(string path)
        {
            try
            {
                return new DirectoryInfo(path)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            catch { return 0; }
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

    public class SubfolderViewModel
    {
        public string Name { get; set; } = "";
        public string Size { get; set; } = "";
        public long SizeBytes { get; set; }
    }
}