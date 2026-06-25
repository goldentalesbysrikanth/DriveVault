using DriveVault.Data;
using DriveVault.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Windows.ApplicationModel;
using Windows.Storage;

namespace DriveVault.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadSettings();
        }

        private void LoadSettings()
        {
            // ✅ ADDED — load saved theme and set correct radio button
            ThemeLight.Checked -= ThemeLight_Checked;
            ThemeDark.Checked -= ThemeDark_Checked;
            ThemeSystem.Checked -= ThemeSystem_Checked;

            var savedTheme = DatabaseHelper.GetSetting("app_theme", "system");
            ThemeLight.IsChecked = savedTheme == "light";
            ThemeDark.IsChecked = savedTheme == "dark";
            ThemeSystem.IsChecked = savedTheme == "system";

            ThemeLight.Checked += ThemeLight_Checked;
            ThemeDark.Checked += ThemeDark_Checked;
            ThemeSystem.Checked += ThemeSystem_Checked;

            // ── Everything below identical to original ─────────────

            var installDate = DatabaseHelper.GetSetting("install_date", "");
            if (string.IsNullOrEmpty(installDate))
            {
                installDate = DateTime.Now.ToString("o");
                DatabaseHelper.SaveSetting("install_date", installDate);
            }

            if (!DateTime.TryParse(installDate, null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var installed))
            {
                installed = DateTime.Now;
                DatabaseHelper.SaveSetting("install_date", installed.ToString("o"));
            }

            var daysLeft = 10 - (int)(DateTime.Now - installed).TotalDays;
            TrialStatusText.Text = daysLeft > 0
                ? $"🕐 {daysLeft} day{(daysLeft != 1 ? "s" : "")} remaining in trial"
                : "🔒 Trial expired";
            TrialEndText.Text = daysLeft > 0
                ? $"Trial ends {installed.AddDays(10):MMM dd, yyyy}"
                : $"Trial ended {installed.AddDays(10):MMM dd, yyyy}";

            LaunchAtStartupToggle.Toggled -= LaunchAtStartupToggle_Toggled;
            LaunchAtStartupToggle.IsOn =
                DatabaseHelper.GetSetting("launch_at_startup", "false") == "true";
            LaunchAtStartupToggle.Toggled += LaunchAtStartupToggle_Toggled;

            AutoIndexToggle.Toggled -= AutoIndexToggle_Toggled;
            AutoIndexToggle.IsOn =
                DatabaseHelper.GetSetting("auto_index", "true") == "true";
            AutoIndexToggle.Toggled += AutoIndexToggle_Toggled;

            AskBeforeIndexToggle.Toggled -= AskBeforeIndexToggle_Toggled;
            AskBeforeIndexToggle.IsOn =
                DatabaseHelper.GetSetting("ask_before_index", "false") == "true";
            AskBeforeIndexToggle.Toggled += AskBeforeIndexToggle_Toggled;

            AlertThresholdSlider.ValueChanged -= AlertThresholdSlider_ValueChanged;
            var threshold = DatabaseHelper.GetSetting("alert_threshold", "90");
            AlertThresholdSlider.Value = double.Parse(threshold);
            AlertThresholdText.Text = $"{threshold}%";
            AlertThresholdSlider.ValueChanged += AlertThresholdSlider_ValueChanged;

            AlertDaysSlider.ValueChanged -= AlertDaysSlider_ValueChanged;
            var days = DatabaseHelper.GetSetting("alert_days_unseen", "3");
            AlertDaysSlider.Value = double.Parse(days);
            AlertDaysText.Text = $"{days} day{(days == "1" ? "" : "s")}";
            AlertDaysSlider.ValueChanged += AlertDaysSlider_ValueChanged;

            LoadExcludedDrives();

            PasscodeStatusText.Text = AppLockService.HasPasscode
                ? "✅ Passcode is set" : "No passcode set";
            SetPasscodeBtn.Content = AppLockService.HasPasscode
                ? "Change Passcode" : "Set Passcode";
            RemovePasscodeBtn.Visibility = AppLockService.HasPasscode
                ? Visibility.Visible : Visibility.Collapsed;
            ForgotPasscodeBtn.Visibility = AppLockService.HasPasscode
                ? Visibility.Visible : Visibility.Collapsed;

            AppLockToggle.Toggled -= AppLockToggle_Toggled;
            AppLockToggle.IsOn = AppLockService.IsAppLockEnabled;
            AppLockToggle.IsEnabled = AppLockService.HasPasscode;
            AppLockToggle.Toggled += AppLockToggle_Toggled;

            var pwd = DatabaseHelper.GetSetting("activity_password", "");
            PasswordStatusText.Text = string.IsNullOrEmpty(pwd)
                ? "No password set" : "✅ Password is set";

            DbPathText.Text = Path.Combine(
                ApplicationData.Current.LocalFolder.Path, "drivevault.db");

            var backups = BackupService.GetAvailableBackups();
            LastBackupText.Text = backups.Any()
                ? $"Last backup: {backups.First().TimeAgo} ({backups.First().SizeDisplay})"
                : "No backups found";
        }

        private void LoadExcludedDrives()
        {
            var raw = DatabaseHelper.GetSetting("excluded_drives", "");
            var list = raw.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            ExcludedDrivesListView.ItemsSource =
                list.Count > 0 ? list : null;
            ExcludedDrivesListView.Visibility =
                list.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ✅ ADDED — 3 theme handlers
        private void ThemeLight_Checked(object sender, RoutedEventArgs e)
        {
            DatabaseHelper.SaveSetting("app_theme", "light");
            DatabaseHelper.LogSettingChange("App Theme", "other", "light");
            if (App.MainWindow is MainWindow mw) mw.ApplyTheme();
        }

        private void ThemeDark_Checked(object sender, RoutedEventArgs e)
        {
            DatabaseHelper.SaveSetting("app_theme", "dark");
            DatabaseHelper.LogSettingChange("App Theme", "other", "dark");
            if (App.MainWindow is MainWindow mw) mw.ApplyTheme();
        }

        private void ThemeSystem_Checked(object sender, RoutedEventArgs e)
        {
            DatabaseHelper.SaveSetting("app_theme", "system");
            DatabaseHelper.LogSettingChange("App Theme", "other", "system");
            if (App.MainWindow is MainWindow mw) mw.ApplyTheme();
        }

        // ─── Everything below identical to original ───────────────

        private async void LaunchAtStartupToggle_Toggled(object sender,
            RoutedEventArgs e)
        {
            var isOn = LaunchAtStartupToggle.IsOn;
            var oldVal = isOn ? "false" : "true";
            DatabaseHelper.SaveSetting("launch_at_startup",
                isOn ? "true" : "false");
            DatabaseHelper.LogSettingChange("Launch at Startup",
                oldVal, isOn ? "true" : "false");
            try
            {
                var startupTask = await StartupTask.GetAsync("DriveVaultStartup");
                if (isOn)
                {
                    if (startupTask.State == StartupTaskState.Disabled)
                        await startupTask.RequestEnableAsync();
                }
                else
                {
                    if (startupTask.State == StartupTaskState.Enabled ||
                        startupTask.State == StartupTaskState.EnabledByPolicy)
                        startupTask.Disable();
                }
            }
            catch { }
        }

        private void AutoIndexToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var isOn = AutoIndexToggle.IsOn;
            var oldVal = isOn ? "false" : "true";
            DatabaseHelper.SaveSetting("auto_index", isOn ? "true" : "false");
            DatabaseHelper.LogSettingChange("Auto Index",
                oldVal, isOn ? "true" : "false");
        }

        private void AskBeforeIndexToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var isOn = AskBeforeIndexToggle.IsOn;
            var oldVal = isOn ? "false" : "true";
            DatabaseHelper.SaveSetting("ask_before_index",
                isOn ? "true" : "false");
            DatabaseHelper.LogSettingChange("Ask Before Index",
                oldVal, isOn ? "true" : "false");
        }

        private void AlertThresholdSlider_ValueChanged(object sender,
            Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            var val = (int)AlertThresholdSlider.Value;
            var oldVal = DatabaseHelper.GetSetting("alert_threshold", "90");
            AlertThresholdText.Text = $"{val}%";
            DatabaseHelper.SaveSetting("alert_threshold", val.ToString());
            if (oldVal != val.ToString())
                DatabaseHelper.LogSettingChange("Alert Threshold",
                    $"{oldVal}%", $"{val}%");
        }

        private void AlertDaysSlider_ValueChanged(object sender,
            Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            var val = (int)AlertDaysSlider.Value;
            var oldVal = DatabaseHelper.GetSetting("alert_days_unseen", "3");
            AlertDaysText.Text = $"{val} day{(val == 1 ? "" : "s")}";
            DatabaseHelper.SaveSetting("alert_days_unseen", val.ToString());
            if (oldVal != val.ToString())
                DatabaseHelper.LogSettingChange("Alert Days Unseen",
                    $"{oldVal} days", $"{val} days");
        }

        private void AddExcludedDrive_Click(object sender, RoutedEventArgs e)
        {
            var name = NewExcludedDriveBox.Text.Trim();
            if (string.IsNullOrEmpty(name)) return;
            var raw = DatabaseHelper.GetSetting("excluded_drives", "");
            var list = raw.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            if (!list.Contains(name))
            {
                list.Add(name);
                DatabaseHelper.SaveSetting("excluded_drives",
                    string.Join(",", list));
                DatabaseHelper.LogSettingChange("Excluded Drives",
                    raw, string.Join(",", list));
            }
            NewExcludedDriveBox.Text = "";
            LoadExcludedDrives();
        }

        private void RemoveExcludedDrive_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string name)
            {
                var raw = DatabaseHelper.GetSetting("excluded_drives", "");
                var list = raw.Split(',')
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s) && s != name)
                    .ToList();
                var newVal = string.Join(",", list);
                DatabaseHelper.SaveSetting("excluded_drives", newVal);
                DatabaseHelper.LogSettingChange("Excluded Drives", raw, newVal);
                LoadExcludedDrives();
            }
        }

        private async void SetPasscode_Click(object sender, RoutedEventArgs e)
        {
            if (AppLockService.HasPasscode)
                if (!await VerifyCurrentPasscode()) return;

            var newPass = new PasswordBox
            {
                PlaceholderText = "Enter new passcode (min 4 chars)",
                Width = 280
            };
            var confirmPass = new PasswordBox
            {
                PlaceholderText = "Confirm new passcode",
                Width = 280,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var errorText = new TextBlock
            {
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                    .Resources["SystemFillColorCriticalBrush"],
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock
            {
                Text = "Set a passcode to protect sensitive actions.\nYou will receive a recovery key — save it safely!",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                    .Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(newPass);
            panel.Children.Add(confirmPass);
            panel.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = AppLockService.HasPasscode
                    ? "Change Passcode" : "Set App Passcode",
                Content = panel,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            while (true)
            {
                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                if (string.IsNullOrEmpty(newPass.Password))
                {
                    errorText.Text = "Passcode cannot be empty.";
                    errorText.Visibility = Visibility.Visible;
                    continue;
                }
                if (newPass.Password.Length < 4)
                {
                    errorText.Text = "Passcode must be at least 4 characters.";
                    errorText.Visibility = Visibility.Visible;
                    continue;
                }
                if (newPass.Password != confirmPass.Password)
                {
                    errorText.Text = "Passcodes do not match.";
                    errorText.Visibility = Visibility.Visible;
                    continue;
                }

                var wasSet = AppLockService.HasPasscode;
                var recoveryKey = AppLockService.SetPasscode(newPass.Password);
                LoadSettings();

                DatabaseHelper.LogSettingChange("App Passcode",
                    wasSet ? "Changed" : "Not Set", "Set");

                var recoveryPanel = new StackPanel { Spacing = 12 };
                recoveryPanel.Children.Add(new TextBlock
                {
                    Text = "⚠️ Save this recovery key safely!\nYou will need it if you forget your passcode.\nThis key will NOT be shown again.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                        .Resources["SystemFillColorCautionBrush"],
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });

                var keyBorder = new Border
                {
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                        .Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                        .Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 12)
                };
                keyBorder.Child = new TextBlock
                {
                    Text = recoveryKey,
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                };
                recoveryPanel.Children.Add(keyBorder);

                var copyBtn = new Button
                {
                    Content = "📋 Copy Recovery Key",
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                copyBtn.Click += (s, args) =>
                {
                    var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dp.SetText(recoveryKey);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                    copyBtn.Content = "✅ Copied!";
                };
                recoveryPanel.Children.Add(copyBtn);
                recoveryPanel.Children.Add(new TextBlock
                {
                    Text = "Store this in a safe place (notepad, password manager, etc.)",
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                        .Resources["TextFillColorSecondaryBrush"],
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                await new ContentDialog
                {
                    Title = "✅ Passcode Set — Save Recovery Key",
                    Content = recoveryPanel,
                    CloseButtonText = "I've saved my recovery key",
                    XamlRoot = XamlRoot
                }.ShowAsync();

                return;
            }
        }

        private async void RemovePasscode_Click(object sender, RoutedEventArgs e)
        {
            if (!await VerifyCurrentPasscode()) return;

            var confirm = new ContentDialog
            {
                Title = "Remove Passcode",
                Content = "Are you sure you want to remove the app passcode?\nThis will also disable App Lock.",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            {
                AppLockService.RemovePasscode();
                DatabaseHelper.LogSettingChange("App Passcode", "Set", "Removed");
                LoadSettings();
            }
        }

        private async void ForgotPasscode_Click(object sender, RoutedEventArgs e)
        {
            var recoveryBox = new TextBox
            {
                PlaceholderText = "Enter recovery key (e.g. DV-XXXX-XXXX-XXXX)",
                Width = 300
            };
            var newPass = new PasswordBox
            {
                PlaceholderText = "New passcode",
                Width = 300,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var confirmPass = new PasswordBox
            {
                PlaceholderText = "Confirm new passcode",
                Width = 300,
                Margin = new Thickness(0, 8, 0, 0)
            };
            var errorText = new TextBlock
            {
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                    .Resources["SystemFillColorCriticalBrush"],
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock
            {
                Text = "Enter your recovery key to reset your passcode.",
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                    .Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(recoveryBox);
            panel.Children.Add(newPass);
            panel.Children.Add(confirmPass);
            panel.Children.Add(errorText);

            var dialog = new ContentDialog
            {
                Title = "🔑 Forgot Passcode",
                Content = panel,
                PrimaryButtonText = "Reset Passcode",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            while (true)
            {
                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary) return;

                if (string.IsNullOrEmpty(recoveryBox.Text))
                {
                    errorText.Text = "Please enter your recovery key.";
                    errorText.Visibility = Visibility.Visible;
                    continue;
                }
                if (newPass.Password.Length < 4)
                {
                    errorText.Text = "New passcode must be at least 4 characters.";
                    errorText.Visibility = Visibility.Visible;
                    continue;
                }
                if (newPass.Password != confirmPass.Password)
                {
                    errorText.Text = "Passcodes do not match.";
                    errorText.Visibility = Visibility.Visible;
                    continue;
                }

                var newRecoveryKey = AppLockService.ResetPasscodeWithRecovery(
                    recoveryBox.Text, newPass.Password);

                if (newRecoveryKey == "invalid_recovery")
                {
                    errorText.Text = "❌ Invalid recovery key. Please try again.";
                    errorText.Visibility = Visibility.Visible;
                    continue;
                }

                if (newRecoveryKey == "invalid_passcode")
                {
                    errorText.Text = "❌ Invalid passcode.";
                    errorText.Visibility = Visibility.Visible;
                    continue;
                }

                DatabaseHelper.LogSettingChange("App Passcode",
                    "Reset via Recovery Key", "New Passcode Set");

                var recoveryPanel = new StackPanel { Spacing = 12 };
                recoveryPanel.Children.Add(new TextBlock
                {
                    Text = "✅ Passcode reset successfully!\n\n⚠️ New Recovery Key — Save it safely:",
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                });

                var keyBorder = new Border
                {
                    Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                        .Resources["CardBackgroundFillColorDefaultBrush"],
                    BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                        .Resources["CardStrokeColorDefaultBrush"],
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 12, 16, 12)
                };
                keyBorder.Child = new TextBlock
                {
                    Text = newRecoveryKey,
                    FontSize = 20,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                };
                recoveryPanel.Children.Add(keyBorder);

                var copyBtn = new Button
                {
                    Content = "📋 Copy Recovery Key",
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                copyBtn.Click += (s, args) =>
                {
                    var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dp.SetText(newRecoveryKey);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                    copyBtn.Content = "✅ Copied!";
                };
                recoveryPanel.Children.Add(copyBtn);

                await new ContentDialog
                {
                    Title = "Passcode Reset Complete",
                    Content = recoveryPanel,
                    CloseButtonText = "I've saved my recovery key",
                    XamlRoot = XamlRoot
                }.ShowAsync();

                LoadSettings();
                return;
            }
        }

        private void AppLockToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var isOn = AppLockToggle.IsOn;
            AppLockService.SetAppLock(isOn);
            DatabaseHelper.LogSettingChange("App Lock",
                isOn ? "Disabled" : "Enabled",
                isOn ? "Enabled" : "Disabled");
        }

        private void SavePassword_Click(object sender, RoutedEventArgs e)
        {
            var pwd = ActivityPasswordBox.Password;
            if (string.IsNullOrEmpty(pwd)) return;
            var hadPassword = !string.IsNullOrEmpty(
                DatabaseHelper.GetSetting("activity_password", ""));
            DatabaseHelper.SaveSetting("activity_password", pwd);
            ActivityPasswordBox.Password = "";
            PasswordStatusText.Text = "✅ Password saved!";
            DatabaseHelper.LogSettingChange("Activity Password",
                hadPassword ? "Changed" : "Not Set", "Set");
        }

        private async void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            var path = BackupService.CreateManualBackup();

            if (path.StartsWith("Error"))
            {
                await new ContentDialog
                {
                    Title = "Backup Failed",
                    Content = path,
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();
                return;
            }

            DatabaseHelper.LogActivity(
                "backup_created", "", "", "Manual backup created", "System");

            await new ContentDialog
            {
                Title = "✅ Backup Created",
                Content = $"Backup saved successfully.\n{path}",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();

            LoadSettings();
        }

        private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (AppLockService.HasPasscode)
                if (!await VerifyCurrentPasscode()) return;

            var backups = BackupService.GetAvailableBackups();

            if (!backups.Any())
            {
                await new ContentDialog
                {
                    Title = "No Backups Found",
                    Content = "No backup files available to restore.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();
                return;
            }

            var listView = new ListView
            {
                SelectionMode = ListViewSelectionMode.Single,
                MaxHeight = 300
            };

            foreach (var b in backups)
            {
                listView.Items.Add(new ListViewItem
                {
                    Content = new StackPanel
                    {
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock
                            {
                                Text  = b.DisplayName,
                                Style = (Style)Application.Current
                                    .Resources["BodyTextBlockStyle"]
                            },
                            new TextBlock
                            {
                                Text       = $"{b.TimeAgo} · {b.SizeDisplay}",
                                Style      = (Style)Application.Current
                                    .Resources["CaptionTextBlockStyle"],
                                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application
                                    .Current.Resources["TextFillColorSecondaryBrush"]
                            }
                        }
                    },
                    Tag = b.FilePath
                });
            }

            if (backups.Any()) listView.SelectedIndex = 0;

            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(new TextBlock
            {
                Text = "⚠️ Current data will be replaced with the backup.\nAll changes after the backup date will be lost.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                    .Resources["SystemFillColorCautionBrush"]
            });
            panel.Children.Add(listView);

            var dialog = new ContentDialog
            {
                Title = "Restore from Backup",
                Content = panel,
                PrimaryButtonText = "Restore",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            if (listView.SelectedItem is not ListViewItem selected) return;

            var filePath = selected.Tag?.ToString() ?? "";
            BackupService.CreateManualBackup();
            var success = BackupService.RestoreBackup(filePath);

            if (success)
            {
                DatabaseHelper.LogActivity(
                    "backup_restored", "", "",
                    $"Restored: {Path.GetFileName(filePath)}", "System");

                await new ContentDialog
                {
                    Title = "✅ Restore Complete",
                    Content = "Database restored successfully.\nPlease restart the app to see changes.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();
            }
            else
            {
                await new ContentDialog
                {
                    Title = "Restore Failed",
                    Content = "Could not restore the backup file.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();
            }
        }

        private void PurchaseLicense_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://drivevault.app/buy",
                UseShellExecute = true
            });
        }

        private void OpenDbFolder_Click(object sender, RoutedEventArgs e)
        {
            var dbPath = Path.Combine(
                ApplicationData.Current.LocalFolder.Path, "drivevault.db");
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{dbPath}\"",
                UseShellExecute = true
            });
        }

        private async void ResetApp_Click(object sender, RoutedEventArgs e)
        {
            if (AppLockService.HasPasscode)
                if (!await VerifyCurrentPasscode()) return;

            var confirmBox = new TextBox
            {
                PlaceholderText = "Type RESET to confirm",
                Width = 250
            };

            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock
            {
                Text = "⚠️ This will permanently delete all drives, shoots, and folders.\nActivity log and settings are preserved.\n\nA backup will be created before reset.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current
                    .Resources["TextFillColorSecondaryBrush"]
            });
            panel.Children.Add(confirmBox);

            var dialog = new ContentDialog
            {
                Title = "Reset All Data?",
                Content = panel,
                PrimaryButtonText = "Delete Everything",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary &&
                confirmBox.Text == "RESET")
            {
                BackupService.CreateManualBackup();

                foreach (var drive in DatabaseHelper.GetAllDrives())
                    DatabaseHelper.RemoveDrive(drive.Id);

                DatabaseHelper.LogActivity(
                    "app_reset", "", "", "App data reset", "System");

                await new ContentDialog
                {
                    Title = "Reset Complete",
                    Content = "All drive and folder data has been deleted.\nA backup was created before reset.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot
                }.ShowAsync();

                LoadSettings();
            }
        }

        private async System.Threading.Tasks.Task<bool> VerifyCurrentPasscode()
        {
            var passBox = new PasswordBox
            {
                PlaceholderText = "Enter your passcode",
                Width = 280
            };

            var dialog = new ContentDialog
            {
                Title = "🔒 Enter Passcode",
                Content = passBox,
                PrimaryButtonText = "Confirm",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                return false;

            if (AppLockService.VerifyPasscode(passBox.Password))
                return true;

            await new ContentDialog
            {
                Title = "❌ Wrong Passcode",
                Content = "Incorrect passcode. Access denied.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();

            return false;
        }
    }
}