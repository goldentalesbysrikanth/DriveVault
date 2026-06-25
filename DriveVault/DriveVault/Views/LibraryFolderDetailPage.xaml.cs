using DriveVault.Data;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DriveVault.Views
{
    public sealed partial class LibraryFolderDetailPage : Page
    {
        private string _folderId = "";

        public LibraryFolderDetailPage()
        {
            this.InitializeComponent();
        }

        // ✅ FIX — returns correct white/black based on actual theme
        // Application.Current.Resources always returns light theme brush
        // This helper checks actual theme and returns correct color
        private SolidColorBrush PrimaryFg =>
            new SolidColorBrush(ActualTheme == ElementTheme.Dark
                ? Windows.UI.Color.FromArgb(255, 255, 255, 255)
                : Windows.UI.Color.FromArgb(255, 26, 26, 26));

        private SolidColorBrush SecondaryFg =>
    new SolidColorBrush(ActualTheme == ElementTheme.Dark
        ? Windows.UI.Color.FromArgb(255, 180, 180, 180)
        : Windows.UI.Color.FromArgb(255, 100, 100, 100));

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
            var drives = DatabaseHelper.GetAllDrives();

            DriveFolder? folder = null;
            foreach (var drive in drives)
            {
                folder = DatabaseHelper.GetFoldersByDrive(drive.Id)
                    .FirstOrDefault(f => f.Id == _folderId);
                if (folder != null) break;
            }
            if (folder == null) return;

            var drive2 = drives.FirstOrDefault(d => d.Id == folder.DriveId);

            TitleText.Text = folder.FolderName;
            TotalSizeText.Text = folder.SizeDisplay;
            TotalFilesText.Text = $"{folder.FileCount:N0} files";
            DriveNameText.Text = drive2?.Label ?? "Unknown";
            CreatedText.Text = folder.FirstSeen.ToString("MMM dd, yyyy");

            BuildFileTypeSummary(folder.FileTypeSummary);
            TreeContainer.Children.Clear();

            if (drive2?.IsConnected == true && Directory.Exists(folder.FolderPath))
            {
                BuildLiveTree(TreeContainer, folder.FolderPath, 0);
            }
            else
            {
                var msgPanel = new StackPanel { Spacing = 4 };
                msgPanel.Children.Add(new TextBlock
                {
                    Text = "⚫ Drive is offline",
                    Style = (Style)Application.Current
                        .Resources["BodyStrongTextBlockStyle"],
                    Foreground = PrimaryFg
                });
                msgPanel.Children.Add(new TextBlock
                {
                    Text = $"Connect \"{drive2?.Label ?? "this drive"}\" to browse subfolders.",
                    Style = (Style)Application.Current
                        .Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"]
                });
                msgPanel.Children.Add(new TextBlock
                {
                    Text = $"Last indexed: {folder.LastSeen:MMM dd, yyyy · hh:mm tt}",
                    Style = (Style)Application.Current
                        .Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"]
                });

                TreeContainer.Children.Add(new Border
                {
                    Background = (Brush)Application.Current
                        .Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Brush)Application.Current
                        .Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16),
                    Margin = new Thickness(0, 0, 0, 8),
                    Child = msgPanel
                });

                BuildDBTree(TreeContainer, folder.FolderPath,
                    folder.DriveId, 0);
            }
        }

        private static string GetRelativePath(string fullPath)
        {
            try
            {
                var root = System.IO.Path.GetPathRoot(fullPath) ?? "";
                return fullPath.Substring(root.Length).TrimEnd('\\', '/');
            }
            catch { return fullPath; }
        }

        private void BuildDBTree(StackPanel container,
            string parentPath, string driveId, int depth)
        {
            try
            {
                var allFolders = DatabaseHelper.GetFoldersByDrive(driveId);
                var parentRel = GetRelativePath(parentPath);

                var children = allFolders
                    .Where(f =>
                    {
                        var childRel = GetRelativePath(f.FolderPath);
                        if (childRel.Equals(parentRel,
                            StringComparison.OrdinalIgnoreCase)) return false;
                        if (!childRel.StartsWith(parentRel,
                            StringComparison.OrdinalIgnoreCase)) return false;
                        var remainder = childRel
                            .Substring(parentRel.Length)
                            .TrimStart('\\', '/');
                        return !string.IsNullOrEmpty(remainder) &&
                               !remainder.Contains('\\') &&
                               !remainder.Contains('/');
                    })
                    .OrderBy(f => f.FolderName)
                    .ToList();

                if (!children.Any()) return;

                foreach (var child in children)
                {
                    var childRel2 = GetRelativePath(child.FolderPath);
                    bool hasChildren = allFolders.Any(f =>
                    {
                        var fRel = GetRelativePath(f.FolderPath);
                        if (fRel.Equals(childRel2,
                            StringComparison.OrdinalIgnoreCase)) return false;
                        if (!fRel.StartsWith(childRel2,
                            StringComparison.OrdinalIgnoreCase)) return false;
                        var rel = fRel.Substring(childRel2.Length)
                            .TrimStart('\\', '/');
                        return !string.IsNullOrEmpty(rel) &&
                               !rel.Contains('\\') &&
                               !rel.Contains('/');
                    });

                    var rowPanel = new StackPanel { Spacing = 0 };
                    var headerGrid = new Grid
                    {
                        Padding = new Thickness(depth * 20 + 8, 8, 12, 8)
                    };
                    headerGrid.ColumnDefinitions.Add(
                        new ColumnDefinition { Width = GridLength.Auto });
                    headerGrid.ColumnDefinitions.Add(
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    headerGrid.ColumnDefinitions.Add(
                        new ColumnDefinition { Width = GridLength.Auto });

                    var expandBtn = new Button
                    {
                        Content = hasChildren ? "▶" : "  ",
                        Width = 28,
                        Height = 28,
                        Padding = new Thickness(0),
                        FontSize = 10,
                        Background = new SolidColorBrush(Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        Margin = new Thickness(0, 0, 8, 0),
                        IsEnabled = hasChildren
                    };
                    Grid.SetColumn(expandBtn, 0);

                    var metaParts = new List<string>();
                    if (child.FileCount > 0)
                        metaParts.Add($"{child.FileCount:N0} files");
                    if (!string.IsNullOrEmpty(child.FileTypeSummary))
                    {
                        metaParts.AddRange(
                            child.FileTypeSummary.Split('|')
                            .Take(4)
                            .Select(t => t.Trim()));
                    }
                    metaParts.Add(child.FirstSeen.ToString("MMM dd, yyyy"));

                    var infoPanel = new StackPanel { Spacing = 2 };
                    infoPanel.Children.Add(new TextBlock
                    {
                        Text = "📁 " + child.FolderName,
                        Style = (Style)Application.Current
                            .Resources["BodyTextBlockStyle"],
                        Foreground = PrimaryFg   // ✅ theme-aware
                    });
                    infoPanel.Children.Add(new TextBlock
                    {
                        Text = string.Join(" · ", metaParts),
                        Style = (Style)Application.Current
                            .Resources["CaptionTextBlockStyle"],
                        Foreground = SecondaryFg,
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                    });
                    Grid.SetColumn(infoPanel, 1);

                    var sizeText = new TextBlock
                    {
                        Text = child.SizeDisplay,
                        Style = (Style)Application.Current
                            .Resources["BodyTextBlockStyle"],
                        Foreground = PrimaryFg,   // ✅ theme-aware
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(12, 0, 0, 0)
                    };
                    Grid.SetColumn(sizeText, 2);

                    headerGrid.Children.Add(expandBtn);
                    headerGrid.Children.Add(infoPanel);
                    headerGrid.Children.Add(sizeText);

                    var childrenPanel = new StackPanel
                    {
                        Spacing = 0,
                        Visibility = Visibility.Collapsed
                    };

                    bool isExpanded = false;
                    if (hasChildren)
                    {
                        var capturedPath = child.FolderPath;
                        var capturedDrive = driveId;
                        expandBtn.Click += (s, e) =>
                        {
                            isExpanded = !isExpanded;
                            expandBtn.Content = isExpanded ? "▼" : "▶";
                            childrenPanel.Visibility = isExpanded
                                ? Visibility.Visible
                                : Visibility.Collapsed;
                            if (isExpanded && childrenPanel.Children.Count == 0)
                                BuildDBTree(childrenPanel,
                                    capturedPath, capturedDrive, depth + 1);
                        };
                    }

                    rowPanel.Children.Add(headerGrid);
                    rowPanel.Children.Add(new Border
                    {
                        BorderBrush = (Brush)Application.Current
                            .Resources["DividerStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Margin = new Thickness(depth * 20 + 8, 0, 0, 0)
                    });
                    if (hasChildren)
                        rowPanel.Children.Add(childrenPanel);

                    container.Children.Add(rowPanel);
                }
            }
            catch { }
        }

        private void BuildLiveTree(StackPanel container, string path, int depth)
        {
            try
            {
                var dirs = Directory.GetDirectories(path)
                    .OrderBy(d => d).ToList();

                if (dirs.Count == 0 && depth == 0)
                {
                    container.Children.Add(new TextBlock
                    {
                        Text = "No subfolders found.",
                        Style = (Style)Application.Current
                            .Resources["CaptionTextBlockStyle"],
                        Foreground = (Brush)Application.Current
                            .Resources["TextFillColorSecondaryBrush"],
                        Margin = new Thickness(8)
                    });
                    return;
                }

                foreach (var dir in dirs)
                {
                    try
                    {
                        var name = Path.GetFileName(dir);
                        var subDirs = Directory.GetDirectories(dir);
                        bool hasChildren = subDirs.Length > 0;

                        var allFiles = new DirectoryInfo(dir)
                            .EnumerateFiles("*", SearchOption.AllDirectories)
                            .ToList();
                        var size = allFiles.Sum(f => f.Length);
                        var fileCount = allFiles.Count;
                        var created = Directory.GetCreationTime(dir);
                        var typeSummary = GetFileTypeSummary(allFiles);

                        var rowPanel = new StackPanel { Spacing = 0 };
                        var headerGrid = new Grid
                        {
                            Padding = new Thickness(depth * 20 + 8, 8, 12, 8)
                        };
                        headerGrid.ColumnDefinitions.Add(
                            new ColumnDefinition { Width = GridLength.Auto });
                        headerGrid.ColumnDefinitions.Add(
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        headerGrid.ColumnDefinitions.Add(
                            new ColumnDefinition { Width = GridLength.Auto });

                        var expandBtn = new Button
                        {
                            Content = hasChildren ? "▶" : "  ",
                            Width = 28,
                            Height = 28,
                            Padding = new Thickness(0),
                            FontSize = 10,
                            Background = new SolidColorBrush(Colors.Transparent),
                            BorderThickness = new Thickness(0),
                            Margin = new Thickness(0, 0, 8, 0),
                            IsEnabled = hasChildren
                        };
                        Grid.SetColumn(expandBtn, 0);

                        var metaParts = new List<string>();
                        if (fileCount > 0)
                            metaParts.Add($"{fileCount:N0} files");
                        if (!string.IsNullOrEmpty(typeSummary))
                            metaParts.AddRange(
                                typeSummary.Split('|').Take(4)
                                .Select(t => t.Trim()));
                        metaParts.Add(created.ToString("MMM dd, yyyy"));

                        var infoPanel = new StackPanel { Spacing = 2 };
                        infoPanel.Children.Add(new TextBlock
                        {
                            Text = "📁 " + name,
                            Style = (Style)Application.Current
                                .Resources["BodyTextBlockStyle"],
                            Foreground = PrimaryFg   // ✅ theme-aware
                        });
                        infoPanel.Children.Add(new TextBlock
                        {
                            Text = string.Join(" · ", metaParts),
                            Style = (Style)Application.Current
                                .Resources["CaptionTextBlockStyle"],
                            Foreground = SecondaryFg,
                            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                        });
                        Grid.SetColumn(infoPanel, 1);

                        var sizeText = new TextBlock
                        {
                            Text = FormatSize(size),
                            Style = (Style)Application.Current
                                .Resources["BodyTextBlockStyle"],
                            Foreground = PrimaryFg,   // ✅ theme-aware
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(12, 0, 0, 0)
                        };
                        Grid.SetColumn(sizeText, 2);

                        headerGrid.Children.Add(expandBtn);
                        headerGrid.Children.Add(infoPanel);
                        headerGrid.Children.Add(sizeText);

                        var childrenPanel = new StackPanel
                        {
                            Spacing = 0,
                            Visibility = Visibility.Collapsed
                        };

                        bool isExpanded = false;
                        if (hasChildren)
                        {
                            var capturedDir = dir;
                            expandBtn.Click += (s, e) =>
                            {
                                isExpanded = !isExpanded;
                                expandBtn.Content = isExpanded ? "▼" : "▶";
                                childrenPanel.Visibility = isExpanded
                                    ? Visibility.Visible
                                    : Visibility.Collapsed;
                                if (isExpanded && childrenPanel.Children.Count == 0)
                                    BuildLiveTree(childrenPanel,
                                        capturedDir, depth + 1);
                            };
                        }

                        rowPanel.Children.Add(headerGrid);
                        rowPanel.Children.Add(new Border
                        {
                            BorderBrush = (Brush)Application.Current
                                .Resources["DividerStrokeColorDefaultBrush"],
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            Margin = new Thickness(depth * 20 + 8, 0, 0, 0)
                        });
                        if (hasChildren)
                            rowPanel.Children.Add(childrenPanel);

                        container.Children.Add(rowPanel);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void BuildFileTypeSummary(string summary)
        {
            FileTypesPanel.Children.Clear();

            if (string.IsNullOrEmpty(summary))
            {
                FileTypesPanel.Children.Add(new TextBlock
                {
                    Text = "No file type data",
                    Style = (Style)Application.Current
                        .Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"]
                });
                return;
            }

            foreach (var part in summary.Split('|'))
            {
                FileTypesPanel.Children.Add(new Border
                {
                    Background = new SolidColorBrush(
                        Windows.UI.Color.FromArgb(255, 27, 58, 92)),
                    BorderBrush = new SolidColorBrush(
                        Windows.UI.Color.FromArgb(80, 77, 166, 255)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(16),
                    Padding = new Thickness(10, 4, 10, 4),
                    Child = new TextBlock
                    {
                        Text = part.Trim(),
                        Style = (Style)Application.Current
                            .Resources["CaptionTextBlockStyle"],
                        Foreground = new SolidColorBrush(Colors.White)
                    }
                });
            }
        }

        private static string GetFileTypeSummary(List<FileInfo> files)
        {
            if (files.Count == 0) return "";
            return string.Join("|", files
                .GroupBy(f => f.Extension.ToLower())
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g =>
                    $"{g.Count()} {(string.IsNullOrEmpty(g.Key) ? "other" : g.Key)}"));
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
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