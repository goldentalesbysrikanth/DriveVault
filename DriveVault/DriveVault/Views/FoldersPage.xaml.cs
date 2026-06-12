using DriveVault.Data;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DriveVault.Views
{
    public sealed partial class FoldersPage : Page
    {
        private List<DriveFolder> _allFolders = new();
        private List<Drive> _allDrives = new();
        private string _currentSort = "Newest first";

        public FoldersPage()
        {
            this.InitializeComponent();
            SortCombo.ItemsSource = new List<string>
            {
                "Newest first",
                "Oldest first",
                "Largest first",
                "Smallest first",
                "Name (A-Z)"
            };
            SortCombo.SelectedIndex = 0;
            LoadData();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadData();
        }

        public void LoadData(string? highlightFolderId = null)
        {
            _allDrives = DatabaseHelper.GetAllDrives();

            _allFolders = DatabaseHelper.GetAllFolders()
                .GroupBy(f => f.FolderPath)
                .Select(g => g.OrderByDescending(f =>
                    _allDrives.Any(d => d.Id == f.DriveId) ? 1 : 0)
                    .First())
                .ToList();

            var driveItems = new List<string> { "All Drives" };
            driveItems.AddRange(_allDrives.Select(d => d.Label));
            DriveFilterCombo.ItemsSource = driveItems;
            DriveFilterCombo.SelectedIndex = 0;

            if (!string.IsNullOrEmpty(highlightFolderId))
            {
                // ✅ Target folder మొదటికి move చేయండి
                var target = _allFolders
                    .FirstOrDefault(f => f.Id == highlightFolderId);
                if (target != null)
                {
                    var reordered = new List<DriveFolder> { target };
                    reordered.AddRange(
                        _allFolders.Where(f => f.Id != highlightFolderId));
                    RefreshList(reordered, highlightFolderId);
                    return;
                }
            }

            RefreshList(_allFolders);
        }

        private void RefreshList(List<DriveFolder> folders,
            string? highlightFolderId = null)
        {
            List<DriveFolder> sorted;

            if (!string.IsNullOrEmpty(highlightFolderId))
                sorted = folders;
            else
                sorted = _currentSort switch
                {
                    "Oldest first" => folders.OrderBy(f => f.FirstSeen).ToList(),
                    "Largest first" => folders.OrderByDescending(f => f.SizeBytes).ToList(),
                    "Smallest first" => folders.OrderBy(f => f.SizeBytes).ToList(),
                    "Name (A-Z)" => folders.OrderBy(f => f.FolderName).ToList(),
                    _ => folders.OrderByDescending(f => f.FirstSeen).ToList()
                };

            FolderCountText.Text = $"{sorted.Count} folders";

            FoldersListView.ItemsSource = sorted.Select(f =>
            {
                var drive = _allDrives.FirstOrDefault(d => d.Id == f.DriveId);
                return new FolderListViewModel
                {
                    FolderId = f.Id,
                    FolderName = f.FolderName,
                    SizeDisplay = f.SizeDisplay,
                    DriveName = drive?.Label ?? "Unknown",
                    DateAdded = f.FirstSeen.ToString("MMM dd, yyyy"),
                    FileCount = f.FileCount > 0
                        ? f.FileCount.ToString("N0") : "—",
                    IsHighlighted = f.Id == highlightFolderId
                };
            }).ToList();

            // ✅ First item select చేయండి
            if (!string.IsNullOrEmpty(highlightFolderId))
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    try
                    {
                        if (FoldersListView.Items.Count > 0)
                        {
                            FoldersListView.SelectedIndex = 0;
                            FoldersListView.ScrollIntoView(
                                FoldersListView.Items[0]);
                        }
                    }
                    catch { }
                });
            }
        }

        private void DriveFilterCombo_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (DriveFilterCombo.SelectedItem is string selected)
            {
                if (selected == "All Drives")
                    RefreshList(_allFolders);
                else
                {
                    var drive = _allDrives.FirstOrDefault(d => d.Label == selected);
                    if (drive != null)
                        RefreshList(_allFolders
                            .Where(f => f.DriveId == drive.Id).ToList());
                }
            }
        }

        private void SortCombo_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (SortCombo.SelectedItem is string sort)
            {
                _currentSort = sort;
                var current = DriveFilterCombo.SelectedItem as string;
                if (current == null || current == "All Drives")
                    RefreshList(_allFolders);
                else
                {
                    var drive = _allDrives.FirstOrDefault(d => d.Label == current);
                    if (drive != null)
                        RefreshList(_allFolders
                            .Where(f => f.DriveId == drive.Id).ToList());
                }
            }
        }

        private void FoldersListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is FolderListViewModel vm)
                Frame.Navigate(typeof(LibraryFolderDetailPage), vm.FolderId);
        }
    }

    public class FolderListViewModel
    {
        public string FolderId { get; set; } = "";
        public string FolderName { get; set; } = "";
        public string SizeDisplay { get; set; } = "";
        public string DriveName { get; set; } = "";
        public string DateAdded { get; set; } = "";
        public string FileCount { get; set; } = "";
        public bool IsHighlighted { get; set; } = false;

        // ✅ Highlight background — purple tint for searched item
        public string HighlightBackground =>
            IsHighlighted ? "#1A9B59B6" : "Transparent";
    }
}