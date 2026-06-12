using DriveVault.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;

namespace DriveVault.Views
{
    public sealed partial class TrialExpiredPage : Page
    {
        public TrialExpiredPage()
        {
            this.InitializeComponent();
            LoadTrialInfo();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            LoadTrialInfo();
        }

        private void LoadTrialInfo()
        {
            var installDate = DatabaseHelper.GetSetting("install_date", "");
            if (!string.IsNullOrEmpty(installDate) &&
                DateTime.TryParse(installDate, out var installed))
            {
                var expiredOn = installed.AddDays(10);
                TrialEndText.Text =
                    $"Trial expired on {expiredOn:MMM dd, yyyy}";
            }
        }

        private void PurchaseButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://drivevault.app/buy",
                UseShellExecute = true
            });
        }

        private async void EnterLicenseButton_Click(object sender, RoutedEventArgs e)
        {
            var keyBox = new TextBox
            {
                PlaceholderText = "Enter your license key",
                Width = 300
            };

            var dialog = new ContentDialog
            {
                Title = "Enter License Key",
                Content = keyBox,
                PrimaryButtonText = "Activate",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var key = keyBox.Text.Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    DatabaseHelper.SaveSetting("license_key", key);
                    DatabaseHelper.SaveSetting("license_activated", "true");

                    var success = new ContentDialog
                    {
                        Title = "✅ License Activated!",
                        Content = "Thank you! Drive Vault is now fully unlocked.",
                        CloseButtonText = "Continue",
                        XamlRoot = XamlRoot
                    };
                    await success.ShowAsync();

                    Frame.Navigate(typeof(OverviewPage));
                }
                else
                {
                    var invalid = new ContentDialog
                    {
                        Title = "Invalid Key",
                        Content = "Please enter a valid license key.",
                        CloseButtonText = "OK",
                        XamlRoot = XamlRoot
                    };
                    await invalid.ShowAsync();
                }
            }
        }

        private void ReadOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            DatabaseHelper.SaveSetting("read_only_mode", "true");
            Frame.Navigate(typeof(OverviewPage));
        }
    }
}