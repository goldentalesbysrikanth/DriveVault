using DriveVault.Data;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace DriveVault.Views
{
    public sealed partial class ClientsPage : Page
    {
        private List<ClientViewModel> _allClients = new();
        private List<WorkflowViewModel> _allWorkflows = new();
        private string _currentTab = "Clients";
        private string _currentWfFilter = "All";
        private string _currentSort = "Name (A-Z)";

        private static readonly string[] GroupAOptions =
            { "Not Shared", "Pending", "On Hold", "Shared" };

        private static readonly string[] GroupBOptions =
            { "NA", "Not Started", "Started", "In Progress",
              "Awaiting Client's Response", "On Hold", "Delivered" };

        public ClientsPage()
        {
            this.InitializeComponent();

            SortComboBox.ItemsSource = new List<string>
            {
                "Name (A-Z)",
                "Name (Z-A)",
                "Drive Name",
                "Size (Largest)",
                "Size (Smallest)",
                "Shoots (Most)",
                "Shoots (Least)"
            };
            SortComboBox.SelectedIndex = 0;

            LoadData();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadData();
        }

        public void LoadData(string? highlightFolderId = null)
        {
            var folders = DatabaseHelper.GetAllFolders();
            var drives = DatabaseHelper.GetAllDrives();
            var workflows = DatabaseHelper.GetAllWorkflows();

            var clientGroups = folders
                .GroupBy(f => f.FolderName)
                .OrderBy(g => g.Key)
                .ToList();

            _allClients = clientGroups.Select(g =>
            {
                var best = g.OrderByDescending(f =>
                {
                    var d = drives.FirstOrDefault(d => d.Id == f.DriveId);
                    return d?.IsConnected == true ? 1 : 0;
                }).First();

                var drive = drives.FirstOrDefault(d => d.Id == best.DriveId);
                var wf = workflows.FirstOrDefault(w => w.ClientName == g.Key);
                var hasWf = wf != null;
                var totalSize = g.Sum(f => f.SizeBytes);
                var shootCount = g.Count();

                return new ClientViewModel
                {
                    FolderId = best.Id,
                    FolderName = g.Key,
                    TotalSizeBytes = totalSize,
                    SizeDisplay = FormatSize(totalSize),
                    DriveName = drive?.Label ?? "Unknown",
                    DriveId = drive?.Id ?? "",
                    SubInfo = $"{shootCount} shoot{(shootCount != 1 ? "s" : "")} · {FormatSize(totalSize)}",
                    Initials = GetInitials(g.Key),
                    HasWorkflow = hasWf,
                    WorkflowProgress = wf?.ProgressPercent ?? 0,
                    NeedsWorkflowPrompt = !hasWf && totalSize >= 500L * 1024 * 1024 * 1024,
                    WorkflowBadgeText = hasWf ? $"{wf!.ProgressDisplay}" : "+ Workflow",
                    WorkflowBadgeColor = hasWf ? "#FF7C3AED" : "#FF6B7280",
                    IsHighlighted = best.Id == highlightFolderId,
                    ShootCount = shootCount
                };
            }).ToList();

            _allWorkflows = _allClients.Select(c =>
            {
                var wf = workflows.FirstOrDefault(w => w.ClientName == c.FolderName);
                return BuildWorkflowViewModel(c, wf);
            }).ToList();

            TotalClientsText.Text = _allClients.Count.ToString();
            TotalSizeText.Text = FormatSize(_allClients.Sum(c => c.TotalSizeBytes));
            DrivesUsedText.Text = _allClients
                .Select(c => c.DriveName).Distinct().Count().ToString();
            WorkflowCountText.Text = _allClients.Count(c => c.HasWorkflow).ToString();

            if (!string.IsNullOrEmpty(highlightFolderId))
            {
                var target = _allClients
                    .FirstOrDefault(c => c.FolderId == highlightFolderId);
                if (target != null)
                {
                    var reordered = new List<ClientViewModel> { target };
                    reordered.AddRange(
                        _allClients.Where(c => c.FolderId != highlightFolderId));
                    RefreshClientList(reordered, highlightFolderId);
                    RefreshWorkflowList(_allWorkflows);
                    return;
                }
            }

            RefreshClientList(_allClients);
            RefreshWorkflowList(_allWorkflows);
        }

        // Called from global search — matches by FolderName
        public void LoadDataByName(string folderName)
        {
            var folders = DatabaseHelper.GetAllFolders();
            var drives = DatabaseHelper.GetAllDrives();
            var workflows = DatabaseHelper.GetAllWorkflows();

            var clientGroups = folders
                .GroupBy(f => f.FolderName)
                .OrderBy(g => g.Key)
                .ToList();

            _allClients = clientGroups.Select(g =>
            {
                var best = g.OrderByDescending(f =>
                {
                    var d = drives.FirstOrDefault(d => d.Id == f.DriveId);
                    return d?.IsConnected == true ? 1 : 0;
                }).First();

                var drive = drives.FirstOrDefault(d => d.Id == best.DriveId);
                var wf = workflows.FirstOrDefault(w => w.ClientName == g.Key);
                var hasWf = wf != null;
                var totalSize = g.Sum(f => f.SizeBytes);
                var shootCount = g.Count();

                return new ClientViewModel
                {
                    FolderId = best.Id,
                    FolderName = g.Key,
                    TotalSizeBytes = totalSize,
                    SizeDisplay = FormatSize(totalSize),
                    DriveName = drive?.Label ?? "Unknown",
                    DriveId = drive?.Id ?? "",
                    SubInfo = $"{shootCount} shoot{(shootCount != 1 ? "s" : "")} · {FormatSize(totalSize)}",
                    Initials = GetInitials(g.Key),
                    HasWorkflow = hasWf,
                    WorkflowProgress = wf?.ProgressPercent ?? 0,
                    NeedsWorkflowPrompt = !hasWf && totalSize >= 500L * 1024 * 1024 * 1024,
                    WorkflowBadgeText = hasWf ? $"{wf!.ProgressDisplay}" : "+ Workflow",
                    WorkflowBadgeColor = hasWf ? "#FF7C3AED" : "#FF6B7280",
                    IsHighlighted = string.Equals(g.Key, folderName,
                                            StringComparison.OrdinalIgnoreCase),
                    ShootCount = shootCount
                };
            }).ToList();

            _allWorkflows = _allClients.Select(c =>
            {
                var wf = workflows.FirstOrDefault(w => w.ClientName == c.FolderName);
                return BuildWorkflowViewModel(c, wf);
            }).ToList();

            TotalClientsText.Text = _allClients.Count.ToString();
            TotalSizeText.Text = FormatSize(_allClients.Sum(c => c.TotalSizeBytes));
            DrivesUsedText.Text = _allClients
                .Select(c => c.DriveName).Distinct().Count().ToString();
            WorkflowCountText.Text = _allClients.Count(c => c.HasWorkflow).ToString();

            var target = _allClients.FirstOrDefault(c =>
                string.Equals(c.FolderName, folderName,
                    StringComparison.OrdinalIgnoreCase));

            if (target != null)
            {
                var reordered = new List<ClientViewModel> { target };
                reordered.AddRange(_allClients.Where(c =>
                    !string.Equals(c.FolderName, folderName,
                        StringComparison.OrdinalIgnoreCase)));
                RefreshClientList(reordered, target.FolderId);
                RefreshWorkflowList(_allWorkflows);
            }
            else
            {
                RefreshClientList(_allClients);
                RefreshWorkflowList(_allWorkflows);
            }
        }

        // Skip sort when highlighting so item stays at position 0
        private void RefreshClientList(List<ClientViewModel> clients,
            string? highlightFolderId = null)
        {
            List<ClientViewModel> sorted;

            if (!string.IsNullOrEmpty(highlightFolderId))
            {
                sorted = clients;
            }
            else
            {
                sorted = _currentSort switch
                {
                    "Name (Z-A)" => clients.OrderByDescending(c => c.FolderName).ToList(),
                    "Drive Name" => clients.OrderBy(c => c.DriveName)
                                                .ThenBy(c => c.FolderName).ToList(),
                    "Size (Largest)" => clients.OrderByDescending(c => c.TotalSizeBytes).ToList(),
                    "Size (Smallest)" => clients.OrderBy(c => c.TotalSizeBytes).ToList(),
                    "Shoots (Most)" => clients.OrderByDescending(c => c.ShootCount).ToList(),
                    "Shoots (Least)" => clients.OrderBy(c => c.ShootCount).ToList(),
                    _ => clients.OrderBy(c => c.FolderName).ToList()
                };
            }

            ClientsListView.ItemsSource = sorted;

            if (!string.IsNullOrEmpty(highlightFolderId))
            {
                DispatcherQueue.TryEnqueue(async () =>
                {
                    try
                    {
                        await Task.Delay(100);
                        if (ClientsListView.Items.Count > 0)
                        {
                            ClientsListView.SelectedIndex = 0;
                            ClientsListView.ScrollIntoView(
                                ClientsListView.Items[0]);
                        }
                    }
                    catch { }
                });
            }
        }

        private void RefreshWorkflowList(List<WorkflowViewModel> workflows)
        {
            var filtered = _currentWfFilter switch
            {
                "Attached" => workflows.Where(w => w.HasWorkflow).ToList(),
                "Pending" => workflows.Where(w => !w.HasWorkflow).ToList(),
                "InProgress" => workflows.Where(w => w.HasWorkflow &&
                                    w.ProgressPercent > 0 &&
                                    w.ProgressPercent < 100).ToList(),
                "Completed" => workflows.Where(w => w.HasWorkflow &&
                                    w.ProgressPercent >= 100).ToList(),
                _ => workflows
            };
            WorkflowListView.ItemsSource = filtered;
        }

        private void SortComboBox_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (SortComboBox.SelectedItem is string sort)
            {
                _currentSort = sort;
                RefreshClientList(_allClients);
            }
        }

        private void TabClients_Click(object sender, RoutedEventArgs e)
        {
            _currentTab = "Clients";
            ClientsListView.Visibility = Visibility.Visible;
            WorkflowPanel.Visibility = Visibility.Collapsed;
            WfFiltersPanel.Visibility = Visibility.Collapsed;
            SortPanel.Visibility = Visibility.Visible;
            TabClientsBtn.Style =
                (Style)Application.Current.Resources["AccentButtonStyle"];
            TabWorkflowBtn.Style =
                (Style)Application.Current.Resources["DefaultButtonStyle"];
        }

        private void TabWorkflow_Click(object sender, RoutedEventArgs e)
        {
            _currentTab = "Workflow";
            ClientsListView.Visibility = Visibility.Collapsed;
            WorkflowPanel.Visibility = Visibility.Visible;
            WfFiltersPanel.Visibility = Visibility.Visible;
            SortPanel.Visibility = Visibility.Collapsed;
            TabWorkflowBtn.Style =
                (Style)Application.Current.Resources["AccentButtonStyle"];
            TabClientsBtn.Style =
                (Style)Application.Current.Resources["DefaultButtonStyle"];
        }

        private void WfFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            _currentWfFilter = btn.Tag?.ToString() ?? "All";

            foreach (var b in new[]
            {
                WfFilterAllBtn, WfFilterAttachedBtn,
                WfFilterPendingBtn, WfFilterInProgressBtn,
                WfFilterDoneBtn
            })
                b.Style = (Style)Application.Current
                    .Resources["DefaultButtonStyle"];

            btn.Style = (Style)Application.Current
                .Resources["AccentButtonStyle"];

            RefreshWorkflowList(_allWorkflows);
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender,
            AutoSuggestBoxTextChangedEventArgs args)
        {
            var query = sender.Text.ToLower();
            if (string.IsNullOrWhiteSpace(query))
            {
                RefreshClientList(_allClients);
                return;
            }
            RefreshClientList(_allClients
                .Where(c => c.FolderName.ToLower().Contains(query) ||
                            c.DriveName.ToLower().Contains(query))
                .ToList());
        }

        private async void ClientsListView_ItemClick(object sender,
            ItemClickEventArgs e)
        {
            if (e.ClickedItem is not ClientViewModel vm) return;

            var folders = DatabaseHelper.GetAllFolders()
                .Where(f => f.FolderName == vm.FolderName).ToList();
            var drives = DatabaseHelper.GetAllDrives();
            var wf = DatabaseHelper.GetWorkflow(vm.FolderName);

            var panel = new StackPanel { Spacing = 16 };

            var infoGrid = new Grid();
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });
            infoGrid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });

            void AddInfo(int col, string label, string value)
            {
                var sp = new StackPanel { Spacing = 4 };
                sp.Children.Add(new TextBlock
                {
                    Text = label,
                    Style = (Style)Application.Current
                        .Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"]
                });
                sp.Children.Add(new TextBlock
                {
                    Text = value,
                    Style = (Style)Application.Current
                        .Resources["BodyStrongTextBlockStyle"]
                });
                Grid.SetColumn(sp, col);
                infoGrid.Children.Add(sp);
            }

            AddInfo(0, "Total Size", vm.SizeDisplay);
            AddInfo(1, "Shoots", folders.Count.ToString());
            AddInfo(2, "Latest shoot",
                folders.Max(f => f.FirstSeen).ToString("MMM dd, yyyy"));
            panel.Children.Add(infoGrid);
            panel.Children.Add(MakeDivider());

            var drivesUsed = folders
                .Select(f => drives.FirstOrDefault(d => d.Id == f.DriveId))
                .Where(d => d != null)
                .DistinctBy(d => d!.Id)
                .ToList();

            foreach (var d in drivesUsed)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = d!.IsConnected
                        ? $"  {d.Label} — connected"
                        : $"  {d.Label} — last seen {GetLastSeen(d.LastSeen)}",
                    Style = (Style)Application.Current
                        .Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"]
                });
            }

            panel.Children.Add(MakeDivider());

            if (wf != null)
            {
                panel.Children.Add(BuildWorkflowReadView(wf));
                panel.Children.Add(MakeDivider());
            }

            panel.Children.Add(new TextBlock
            {
                Text = "Shoots",
                Style = (Style)Application.Current
                    .Resources["BodyStrongTextBlockStyle"]
            });

            foreach (var f in folders.OrderByDescending(f => f.FirstSeen))
            {
                var d = drives.FirstOrDefault(d => d.Id == f.DriveId);

                var shootHeader = new Grid { Margin = new Thickness(0, 8, 0, 4) };
                shootHeader.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });
                shootHeader.ColumnDefinitions.Add(new ColumnDefinition
                { Width = GridLength.Auto });

                var shootLeft = new StackPanel { Spacing = 2 };
                shootLeft.Children.Add(new TextBlock
                {
                    Text = "  " + f.FolderName,
                    Style = (Style)Application.Current
                        .Resources["BodyStrongTextBlockStyle"]
                });
                shootLeft.Children.Add(new TextBlock
                {
                    Text = $"{d?.Label ?? "?"} · {f.FileCount:N0} files",
                    Style = (Style)Application.Current
                        .Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"]
                });
                Grid.SetColumn(shootLeft, 0);

                var shootRight = new TextBlock
                {
                    Text = f.SizeDisplay,
                    Style = (Style)Application.Current
                        .Resources["BodyStrongTextBlockStyle"],
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(shootRight, 1);

                shootHeader.Children.Add(shootLeft);
                shootHeader.Children.Add(shootRight);
                panel.Children.Add(shootHeader);

                var allDriveFolders = DatabaseHelper.GetFoldersByDrive(f.DriveId);

                // ✅ CHANGED — use relative path comparison so drive letter
                // changes (G:\ → H:\) don't break subfolder display
                var fRel = StripRoot(f.FolderPath);
                var subFolders = allDriveFolders
                    .Where(sf =>
                    {
                        var sfRel = StripRoot(sf.FolderPath);
                        if (sfRel.Equals(fRel,
                            StringComparison.OrdinalIgnoreCase)) return false;
                        if (!sfRel.StartsWith(fRel,
                            StringComparison.OrdinalIgnoreCase)) return false;
                        var relative = sfRel.Substring(fRel.Length)
                            .TrimStart('\\', '/');
                        return !string.IsNullOrEmpty(relative) &&
                               !relative.Contains('\\') &&
                               !relative.Contains('/');
                    })
                    .OrderBy(sf => sf.FolderName)
                    .ToList();

                if (subFolders.Any())
                {
                    foreach (var sub in subFolders)
                    {
                        var subRow = new Grid
                        {
                            Margin = new Thickness(16, 0, 0, 0),
                            Padding = new Thickness(8, 6, 8, 6)
                        };
                        subRow.ColumnDefinitions.Add(new ColumnDefinition
                        { Width = GridLength.Auto });
                        subRow.ColumnDefinitions.Add(new ColumnDefinition
                        { Width = new GridLength(1, GridUnitType.Star) });
                        subRow.ColumnDefinitions.Add(new ColumnDefinition
                        { Width = GridLength.Auto });

                        var subIcon = new TextBlock
                        {
                            Text = "📁",
                            FontSize = 14,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 8, 0)
                        };
                        Grid.SetColumn(subIcon, 0);

                        var subInfo = new StackPanel { Spacing = 1 };
                        subInfo.Children.Add(new TextBlock
                        {
                            Text = sub.FolderName,
                            Style = (Style)Application.Current
                                .Resources["BodyTextBlockStyle"]
                        });

                        var metaParts = new List<string>();
                        if (sub.FileCount > 0)
                            metaParts.Add($"{sub.FileCount:N0} files");
                        if (!string.IsNullOrEmpty(sub.FileTypeSummary))
                        {
                            metaParts.AddRange(
                                sub.FileTypeSummary.Split('|')
                                .Take(3)
                                .Select(t => t.Trim()));
                        }
                        if (metaParts.Any())
                        {
                            subInfo.Children.Add(new TextBlock
                            {
                                Text = string.Join(" · ", metaParts),
                                Style = (Style)Application.Current
                                    .Resources["CaptionTextBlockStyle"],
                                Foreground = (Brush)Application.Current
                                    .Resources["TextFillColorSecondaryBrush"],
                                TextWrapping = TextWrapping.Wrap
                            });
                        }
                        Grid.SetColumn(subInfo, 1);

                        var subSize = new TextBlock
                        {
                            Text = sub.SizeDisplay,
                            Style = (Style)Application.Current
                                .Resources["CaptionTextBlockStyle"],
                            Foreground = (Brush)Application.Current
                                .Resources["TextFillColorSecondaryBrush"],
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(8, 0, 0, 0)
                        };
                        Grid.SetColumn(subSize, 2);

                        subRow.Children.Add(subIcon);
                        subRow.Children.Add(subInfo);
                        subRow.Children.Add(subSize);

                        panel.Children.Add(new Border
                        {
                            BorderBrush = (Brush)Application.Current
                                .Resources["DividerStrokeColorDefaultBrush"],
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            Child = subRow
                        });
                    }
                }
                else
                {
                    panel.Children.Add(new TextBlock
                    {
                        Text = d?.IsConnected == true
                            ? "  No subfolders"
                            : "  Re-index drive to see subfolders",
                        Style = (Style)Application.Current
                            .Resources["CaptionTextBlockStyle"],
                        Foreground = (Brush)Application.Current
                            .Resources["TextFillColorSecondaryBrush"],
                        Margin = new Thickness(16, 2, 0, 8)
                    });
                }
            }

            panel.Children.Add(MakeDivider());

            var actionRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 8 };
            ContentDialog? parentDialog = null;

            var wfBtn = new Button
            {
                Content = wf != null ? "Edit Workflow" : "Attach Workflow",
                Style = (Style)Application.Current
                    .Resources["AccentButtonStyle"]
            };
            wfBtn.Click += async (s, _) =>
            {
                parentDialog?.Hide();
                await ShowWorkflowEditor(vm.FolderName, folders, drives);
                LoadData();
            };
            actionRow.Children.Add(wfBtn);

            if (wf != null)
            {
                var removeBtn = new Button { Content = "Remove Workflow" };
                removeBtn.Click += async (s, _) =>
                {
                    parentDialog?.Hide();
                    var confirm = new ContentDialog
                    {
                        Title = "Remove Workflow",
                        Content = $"Remove workflow from \"{vm.FolderName}\"?",
                        PrimaryButtonText = "Remove",
                        CloseButtonText = "Cancel",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = XamlRoot
                    };
                    if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                    {
                        DatabaseHelper.DeleteWorkflow(vm.FolderName);
                        DatabaseHelper.LogActivity(
                            "workflow_removed", "", "",
                            $"Workflow Removed: {vm.FolderName}",
                            "System");
                        LoadData();
                    }
                };
                actionRow.Children.Add(removeBtn);
            }

            panel.Children.Add(actionRow);

            parentDialog = new ContentDialog
            {
                Title = vm.FolderName,
                Content = new ScrollViewer
                {
                    Content = panel,
                    MaxHeight = 520,
                    MinWidth = 480
                },
                CloseButtonText = "Close",
                XamlRoot = XamlRoot
            };

            await parentDialog.ShowAsync();
            LoadData();
        }

        private async void WorkflowListView_ItemClick(object sender,
            ItemClickEventArgs e)
        {
            if (e.ClickedItem is not WorkflowViewModel wvm) return;

            var folders = DatabaseHelper.GetAllFolders()
                .Where(f => f.FolderName == wvm.ClientName).ToList();
            var drives = DatabaseHelper.GetAllDrives();

            await ShowWorkflowEditor(wvm.ClientName, folders, drives);
            LoadData();
        }

        private async System.Threading.Tasks.Task ShowWorkflowEditor(
            string clientName,
            List<DriveFolder> folders,
            List<Drive> drives)
        {
            var existing = DatabaseHelper.GetWorkflow(clientName);
            var startDate = folders.Any()
                ? folders.Max(f => f.FirstSeen)
                : DateTime.Now;

            bool isNew = existing == null;

            var wf = existing ?? new ClientWorkflow
            {
                ClientName = clientName,
                ProjectStartDate = startDate
            };

            var state = new ClientWorkflow
            {
                SelectionLinkStatus = wf.SelectionLinkStatus,
                ClientHDDCopyStatus = wf.ClientHDDCopyStatus,
                EditedPhotosStatus = wf.EditedPhotosStatus,
                CinematicVideoStatus = wf.CinematicVideoStatus,
                TraditionalVideoStatus = wf.TraditionalVideoStatus,
                AlbumDesigningStatus = wf.AlbumDesigningStatus,
                CompleteProjectStatus = wf.CompleteProjectStatus,
                Notes = wf.Notes,
                ProjectStartDate = startDate
            };

            var progressText = new TextBlock
            {
                FontSize = 13,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 124, 58, 237))
            };

            void RefreshProgress()
            {
                progressText.Text = $"Progress  ·  {state.ProgressDisplay}";
            }

            StackPanel MakeSegmentedField(
                string fieldLabel,
                string[] options,
                string currentValue,
                Action<string> onChanged)
            {
                var container = new StackPanel { Spacing = 8 };
                container.Children.Add(new TextBlock
                {
                    Text = fieldLabel,
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorPrimaryBrush"]
                });

                var selectedValue = currentValue;
                var buttons = new List<Button>();

                var row1 = new StackPanel
                { Orientation = Orientation.Horizontal, Spacing = 6 };
                var row2 = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    Margin = new Thickness(0, 6, 0, 0)
                };

                void ApplyAllStyles(string selected)
                {
                    foreach (var b in buttons)
                    {
                        var isSel = b.Tag?.ToString() == selected;
                        b.Style = null;
                        b.CornerRadius = new CornerRadius(4);
                        b.BorderThickness = new Thickness(1);

                        if (isSel)
                        {
                            b.Background = new SolidColorBrush(
                                Color.FromArgb(255, 124, 58, 237));
                            b.Foreground = new SolidColorBrush(Colors.White);
                            b.BorderBrush = new SolidColorBrush(
                                Color.FromArgb(255, 124, 58, 237));

                            b.Resources["ButtonBackgroundPointerOver"] =
                                new SolidColorBrush(
                                    Color.FromArgb(255, 109, 40, 217));
                            b.Resources["ButtonForegroundPointerOver"] =
                                new SolidColorBrush(Colors.White);
                            b.Resources["ButtonBorderBrushPointerOver"] =
                                new SolidColorBrush(
                                    Color.FromArgb(255, 109, 40, 217));
                            b.Resources["ButtonBackgroundPressed"] =
                                new SolidColorBrush(
                                    Color.FromArgb(255, 91, 33, 182));
                            b.Resources["ButtonForegroundPressed"] =
                                new SolidColorBrush(Colors.White);
                        }
                        else
                        {
                            b.Background = new SolidColorBrush(
                                Color.FromArgb(0, 0, 0, 0));
                            b.Foreground = (Brush)Application.Current
                                .Resources["TextFillColorSecondaryBrush"];
                            b.BorderBrush = (Brush)Application.Current
                                .Resources["CardStrokeColorDefaultBrush"];

                            b.Resources["ButtonBackgroundPointerOver"] =
                                new SolidColorBrush(
                                    Color.FromArgb(20, 124, 58, 237));
                            b.Resources["ButtonForegroundPointerOver"] =
                                new SolidColorBrush(
                                    Color.FromArgb(255, 124, 58, 237));
                            b.Resources["ButtonBorderBrushPointerOver"] =
                                new SolidColorBrush(
                                    Color.FromArgb(255, 124, 58, 237));
                            b.Resources["ButtonBackgroundPressed"] =
                                new SolidColorBrush(
                                    Color.FromArgb(40, 124, 58, 237));
                            b.Resources["ButtonForegroundPressed"] =
                                new SolidColorBrush(
                                    Color.FromArgb(255, 124, 58, 237));
                        }
                    }
                }

                for (int i = 0; i < options.Length; i++)
                {
                    var optCopy = options[i];
                    var btn = new Button
                    {
                        Content = optCopy,
                        Tag = optCopy,
                        Padding = new Thickness(14, 7, 14, 7),
                        MinWidth = 0,
                        FontSize = 12
                    };
                    buttons.Add(btn);

                    if (i < 4) row1.Children.Add(btn);
                    else row2.Children.Add(btn);
                }

                ApplyAllStyles(selectedValue);

                foreach (var b in buttons)
                {
                    b.Click += (s, _) =>
                    {
                        selectedValue = ((Button)s).Tag?.ToString() ?? "";
                        onChanged(selectedValue);
                        ApplyAllStyles(selectedValue);
                        RefreshProgress();
                    };
                }

                var rowsPanel = new StackPanel { Spacing = 0 };
                rowsPanel.Children.Add(row1);
                if (row2.Children.Count > 0)
                    rowsPanel.Children.Add(row2);

                container.Children.Add(rowsPanel);
                return container;
            }

            var notesBox = new TextBox
            {
                PlaceholderText = "Add notes...",
                Text = wf.Notes,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 72,
                AcceptsReturn = true,
                FontSize = 12,
                Padding = new Thickness(12, 10, 12, 10)
            };

            var panel = new StackPanel { Spacing = 0, Width = 620 };
            var days = (int)(DateTime.Now - startDate).TotalDays;

            var metaPanel = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 16 };

            void AddMeta(string label, string value)
            {
                var sp = new StackPanel { Spacing = 3 };
                sp.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = 9,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    CharacterSpacing = 60,
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"]
                });
                sp.Children.Add(new TextBlock
                {
                    Text = value,
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorPrimaryBrush"]
                });
                metaPanel.Children.Add(sp);
            }

            AddMeta("STARTED", startDate.ToString("MMM dd, yyyy"));
            AddMeta("DAYS RUNNING", $"{days} day{(days != 1 ? "s" : "")}");

            var metaGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            metaGrid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });
            metaGrid.ColumnDefinitions.Add(new ColumnDefinition
            { Width = GridLength.Auto });
            metaGrid.Children.Add(metaPanel);
            Grid.SetColumn(progressText, 1);
            metaGrid.Children.Add(progressText);
            panel.Children.Add(metaGrid);

            RefreshProgress();

            void AddSection(string title)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 10,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    CharacterSpacing = 80,
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"],
                    Margin = new Thickness(0, 4, 0, 12)
                });
            }

            void AddDivider(double top = 16, double bottom = 16)
            {
                panel.Children.Add(new Border
                {
                    BorderBrush = (Brush)Application.Current
                        .Resources["DividerStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Margin = new Thickness(0, top, 0, bottom)
                });
            }

            void AddField(StackPanel field)
            {
                field.Margin = new Thickness(0, 0, 0, 16);
                panel.Children.Add(field);
            }

            AddSection("DELIVERY");
            AddField(MakeSegmentedField("Selection Link",
                GroupAOptions, state.SelectionLinkStatus,
                v => state.SelectionLinkStatus = v));
            AddField(MakeSegmentedField("Client HDD Copy",
                GroupAOptions, state.ClientHDDCopyStatus,
                v => state.ClientHDDCopyStatus = v));

            AddDivider();
            AddSection("PRODUCTION");
            AddField(MakeSegmentedField("Edited Photos",
                GroupBOptions, state.EditedPhotosStatus,
                v => state.EditedPhotosStatus = v));
            AddField(MakeSegmentedField("Cinematic Video",
                GroupBOptions, state.CinematicVideoStatus,
                v => state.CinematicVideoStatus = v));
            AddField(MakeSegmentedField("Traditional Video",
                GroupBOptions, state.TraditionalVideoStatus,
                v => state.TraditionalVideoStatus = v));
            AddField(MakeSegmentedField("Album Designing",
                GroupBOptions, state.AlbumDesigningStatus,
                v => state.AlbumDesigningStatus = v));

            AddDivider();
            AddSection("OVERALL");
            AddField(MakeSegmentedField("Complete Project Status",
                new[] { "NA", "Not Started", "Started",
                        "In Progress", "On Hold", "Delivered" },
                state.CompleteProjectStatus,
                v => state.CompleteProjectStatus = v));

            AddDivider();
            panel.Children.Add(new TextBlock
            {
                Text = "Notes",
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = (Brush)Application.Current
                    .Resources["TextFillColorPrimaryBrush"],
                Margin = new Thickness(0, 0, 0, 8)
            });
            panel.Children.Add(notesBox);

            var scroll = new ScrollViewer
            {
                Content = panel,
                MaxHeight = 640,
                VerticalScrollMode = ScrollMode.Auto,
                Padding = new Thickness(0, 0, 12, 0)
            };

            var dialog = new ContentDialog
            {
                Title = clientName,
                Content = scroll,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return;

            wf.SelectionLinkStatus = state.SelectionLinkStatus;
            wf.ClientHDDCopyStatus = state.ClientHDDCopyStatus;
            wf.EditedPhotosStatus = state.EditedPhotosStatus;
            wf.CinematicVideoStatus = state.CinematicVideoStatus;
            wf.TraditionalVideoStatus = state.TraditionalVideoStatus;
            wf.AlbumDesigningStatus = state.AlbumDesigningStatus;
            wf.CompleteProjectStatus = state.CompleteProjectStatus;
            wf.Notes = notesBox.Text;
            wf.ProjectStartDate = startDate;
            wf.LastUpdatedAt = DateTime.Now;

            DatabaseHelper.SaveWorkflow(wf);

            DatabaseHelper.LogActivity(
                isNew ? "workflow_attached" : "workflow_updated",
                "", "",
                isNew ? $"Workflow Attached: {clientName}"
                      : $"Workflow Updated: {clientName}",
                "System");
        }

        public async System.Threading.Tasks.Task
            PromptWorkflowIfNeeded(string clientName, long totalBytes)
        {
            if (totalBytes < 500L * 1024 * 1024 * 1024) return;
            if (DatabaseHelper.GetWorkflow(clientName) != null) return;

            var dialog = new ContentDialog
            {
                Title = "Attach Workflow?",
                Content = new TextBlock
                {
                    Text =
                        $"\"{clientName}\" has {FormatSize(totalBytes)} of data.\n\n" +
                        "Would you like to attach a workflow?",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = "Attach Now",
                SecondaryButtonText = "Do Later",
                CloseButtonText = "Never for this client",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                var folders = DatabaseHelper.GetAllFolders()
                    .Where(f => f.FolderName == clientName).ToList();
                var drives = DatabaseHelper.GetAllDrives();
                await ShowWorkflowEditor(clientName, folders, drives);
                LoadData();
            }
            else if (result == ContentDialogResult.None)
            {
                DatabaseHelper.SaveSetting($"wf_skip_{clientName}", "true");
            }
        }

        private static StackPanel BuildWorkflowReadView(ClientWorkflow wf)
        {
            var panel = new StackPanel { Spacing = 12 };

            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition
            { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition
            { Width = GridLength.Auto });

            headerRow.Children.Add(new TextBlock
            {
                Text = "Workflow",
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 124, 58, 237)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var progressBadge = new Border
            {
                Background = new SolidColorBrush(
                    Color.FromArgb(30, 124, 58, 237)),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(12, 4, 12, 4)
            };
            progressBadge.Child = new TextBlock
            {
                Text = $"{wf.ProgressDisplay} complete",
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(
                    Color.FromArgb(255, 124, 58, 237))
            };
            Grid.SetColumn(progressBadge, 1);
            headerRow.Children.Add(progressBadge);
            panel.Children.Add(headerRow);

            panel.Children.Add(new Border
            {
                BorderBrush = (Brush)Application.Current
                    .Resources["DividerStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 1, 0, 0)
            });

            void AddRow(string label, string value)
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(160) });
                row.ColumnDefinitions.Add(new ColumnDefinition
                { Width = new GridLength(1, GridUnitType.Star) });

                var lbl = new TextBlock
                {
                    Text = label,
                    FontSize = 12,
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"],
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(lbl, 0);

                var dotColor = GetStatusDotColor(value);
                var valPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    VerticalAlignment = VerticalAlignment.Center
                };
                valPanel.Children.Add(new Ellipse
                {
                    Width = 7,
                    Height = 7,
                    Fill = new SolidColorBrush(dotColor),
                    VerticalAlignment = VerticalAlignment.Center
                });
                valPanel.Children.Add(new TextBlock
                {
                    Text = value,
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(dotColor)
                });
                Grid.SetColumn(valPanel, 1);

                row.Children.Add(lbl);
                row.Children.Add(valPanel);
                panel.Children.Add(row);
            }

            AddRow("Selection Link", wf.SelectionLinkStatus);
            AddRow("Client HDD Copy", wf.ClientHDDCopyStatus);
            AddRow("Edited Photos", wf.EditedPhotosStatus);
            AddRow("Cinematic Video", wf.CinematicVideoStatus);
            AddRow("Traditional Video", wf.TraditionalVideoStatus);
            AddRow("Album Designing", wf.AlbumDesigningStatus);
            AddRow("Project Status", wf.CompleteProjectStatus);

            if (!string.IsNullOrEmpty(wf.Notes))
            {
                panel.Children.Add(new Border
                {
                    BorderBrush = (Brush)Application.Current
                        .Resources["DividerStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(0, 1, 0, 0)
                });
                panel.Children.Add(new TextBlock
                {
                    Text = wf.Notes,
                    FontSize = 12,
                    Foreground = (Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"],
                    TextWrapping = TextWrapping.Wrap
                });
            }

            return panel;
        }

        private static WorkflowViewModel BuildWorkflowViewModel(
            ClientViewModel c, ClientWorkflow? wf)
        {
            var hasWf = wf != null;
            var progress = wf?.ProgressPercent ?? 0;

            string StatusBadgeColor()
            {
                if (!hasWf) return "#FF6B7280";
                if (progress >= 100) return "#FF059669";
                if (progress >= 50) return "#FF7C3AED";
                return "#FF9B59B6";
            }

            string StatusBadgeText()
            {
                if (!hasWf) return "No Workflow";
                if (progress >= 100) return "Completed";
                if (progress >= 50) return "In Progress";
                return "Started";
            }

            string StatusTextColor(string? status)
            {
                if (status == null || status == "NA" ||
                    status == "Not Started")
                    return "#FF9CA3AF";
                return status switch
                {
                    "Delivered" => "#FF059669",
                    "In Progress" => "#FF7C3AED",
                    "Awaiting Client's Response" => "#FF2563EB",
                    "Started" => "#FF0EA5E9",
                    "On Hold" => "#FFD97706",
                    "Shared" => "#FF059669",
                    "Pending" => "#FFD97706",
                    "Not Shared" => "#FF9CA3AF",
                    _ => "#FF9CA3AF"
                };
            }

            string StatusLabel(string? status)
            {
                if (status == null || status == "NA") return "N/A";
                return status;
            }

            var days = wf != null
                ? (int)(DateTime.Now - wf.ProjectStartDate).TotalDays
                : 0;

            var avatarColors = new[]
            {
                "#FF6366F1","#FF8B5CF6","#FF06B6D4",
                "#FF10B981","#FF3B82F6","#FF6D28D9",
                "#FF0891B2","#FF059669"
            };
            var avatarColor = avatarColors[
                Math.Abs(c.FolderName.GetHashCode()) % avatarColors.Length];

            return new WorkflowViewModel
            {
                ClientName = c.FolderName,
                Initials = c.Initials,
                AvatarColor = avatarColor,
                HasWorkflow = hasWf,
                ProgressPercent = progress,
                ProgressDisplay = $"{progress:F0}%",
                StatusBadgeColor = StatusBadgeColor(),
                StatusBadgeText = StatusBadgeText(),
                DaysInfo = hasWf
                    ? $"{wf!.ProjectStartDate:MMM dd, yyyy}  ·  {days} day{(days != 1 ? "s" : "")} running"
                    : c.SubInfo,
                PhotosColor = StatusTextColor(wf?.EditedPhotosStatus),
                PhotosLabel = StatusLabel(wf?.EditedPhotosStatus),
                CinColor = StatusTextColor(wf?.CinematicVideoStatus),
                CinLabel = StatusLabel(wf?.CinematicVideoStatus),
                TradColor = StatusTextColor(wf?.TraditionalVideoStatus),
                TradLabel = StatusLabel(wf?.TraditionalVideoStatus),
                AlbumColor = StatusTextColor(wf?.AlbumDesigningStatus),
                AlbumLabel = StatusLabel(wf?.AlbumDesigningStatus),
            };
        }

        private static Color GetStatusDotColor(string status) => status switch
        {
            "Delivered" or "Shared" => Color.FromArgb(255, 5, 150, 105),
            "In Progress" or "Pending" => Color.FromArgb(255, 124, 58, 237),
            "Awaiting Client's Response" => Color.FromArgb(255, 37, 99, 235),
            "Started" => Color.FromArgb(255, 14, 165, 233),
            "On Hold" => Color.FromArgb(255, 217, 119, 6),
            "Not Started" or "Not Shared" => Color.FromArgb(255, 156, 163, 175),
            _ => Color.FromArgb(255, 209, 213, 219)
        };

        private static Border MakeDivider() => new Border
        {
            BorderBrush = (Brush)Application.Current
                .Resources["DividerStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(0, 1, 0, 0)
        };

        // ✅ Strip drive letter for drive-letter-independent path comparison
        // "G:\Foo\Bar" → "Foo\Bar"
        private static string StripRoot(string path)
        {
            try
            {
                var root = System.IO.Path.GetPathRoot(path) ?? "";
                return path.Substring(root.Length).TrimEnd('\\', '/');
            }
            catch { return path; }
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var words = name.Trim().Split(' ',
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
                return $"{words[0][0]}{words[1][0]}".ToUpper();
            return name.Length >= 2
                ? name[..2].ToUpper() : name[..1].ToUpper();
        }

        private static string GetLastSeen(DateTime dt)
        {
            var diff = DateTime.Now - dt;
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return dt.ToString("MMM dd, yyyy");
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

    public class ClientViewModel
    {
        public string FolderId { get; set; } = "";
        public string FolderName { get; set; } = "";
        public long TotalSizeBytes { get; set; }
        public string SizeDisplay { get; set; } = "";
        public string DriveName { get; set; } = "";
        public string DriveId { get; set; } = "";
        public string SubInfo { get; set; } = "";
        public string Initials { get; set; } = "";
        public bool HasWorkflow { get; set; }
        public double WorkflowProgress { get; set; }
        public bool NeedsWorkflowPrompt { get; set; }
        public string WorkflowBadgeText { get; set; } = "+ Workflow";
        public string WorkflowBadgeColor { get; set; } = "#FF6B7280";
        public bool IsHighlighted { get; set; } = false;
        public int ShootCount { get; set; }
        public string HighlightBackground =>
            IsHighlighted ? "#1A9B59B6" : "Transparent";
    }

    public class WorkflowViewModel
    {
        public string ClientName { get; set; } = "";
        public string Initials { get; set; } = "";
        public string AvatarColor { get; set; } = "#FF6366F1";
        public bool HasWorkflow { get; set; }
        public double ProgressPercent { get; set; }
        public string ProgressDisplay { get; set; } = "0%";
        public string StatusBadgeColor { get; set; } = "#FF6B7280";
        public string StatusBadgeText { get; set; } = "No Workflow";
        public string DaysInfo { get; set; } = "";
        public string PhotosColor { get; set; } = "#FF9CA3AF";
        public string PhotosLabel { get; set; } = "N/A";
        public string CinColor { get; set; } = "#FF9CA3AF";
        public string CinLabel { get; set; } = "N/A";
        public string TradColor { get; set; } = "#FF9CA3AF";
        public string TradLabel { get; set; } = "N/A";
        public string AlbumColor { get; set; } = "#FF9CA3AF";
        public string AlbumLabel { get; set; } = "N/A";
    }
}