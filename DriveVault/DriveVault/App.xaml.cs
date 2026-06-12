using DriveVault.Data;
using DriveVault.Services;
using Microsoft.UI.Xaml;

namespace DriveVault
{
    public partial class App : Application
    {
        public static DriveWatcher DriveWatcher { get; } = new DriveWatcher();
        public static MainWindow? MainWindow { get; private set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            // ✅ Auto backup — crash అయినా app open అవుతుంది
            try { BackupService.AutoBackup(); } catch { }

            MainWindow = new MainWindow();
            MainWindow.Activate();
        }
    }
}