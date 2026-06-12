using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;
using Windows.UI;

namespace DriveVault.Converters
{
    public class StatusBadgeBgConverter : IValueConverter
    {
        private static readonly SolidColorBrush OnlineBg =
            new(Color.FromArgb(40, 0, 200, 0));
        private static readonly SolidColorBrush OfflineBg =
            new(Color.FromArgb(40, 150, 150, 150));

        public object Convert(object value, Type targetType,
            object parameter, string language)
            => value is string s && s == "Online" ? OnlineBg : OfflineBg;

        public object ConvertBack(object value, Type targetType,
            object parameter, string language)
            => throw new NotImplementedException();
    }

    public class StatusBadgeFgConverter : IValueConverter
    {
        private static readonly SolidColorBrush OnlineFg =
            new(Color.FromArgb(255, 0, 200, 0));
        private static readonly SolidColorBrush OfflineFg =
            new(Color.FromArgb(255, 150, 150, 150));

        public object Convert(object value, Type targetType,
            object parameter, string language)
            => value is string s && s == "Online" ? OnlineFg : OfflineFg;

        public object ConvertBack(object value, Type targetType,
            object parameter, string language)
            => throw new NotImplementedException();
    }

    public class ProgressColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush FullBrush =
            new(Color.FromArgb(255, 220, 50, 50));
        private static readonly SolidColorBrush NormalBrush =
            new(Color.FromArgb(255, 155, 89, 182));

        public object Convert(object value, Type targetType,
            object parameter, string language)
            => value is double d && d >= 90 ? FullBrush : NormalBrush;

        public object ConvertBack(object value, Type targetType,
            object parameter, string language)
            => throw new NotImplementedException();
    }

    public class AvatarColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush[] Brushes =
        {
            new(Color.FromArgb(255,  99, 102, 241)),
            new(Color.FromArgb(255, 236,  72, 153)),
            new(Color.FromArgb(255,  16, 185, 129)),
            new(Color.FromArgb(255, 245, 158,  11)),
            new(Color.FromArgb(255,  59, 130, 246)),
            new(Color.FromArgb(255, 239,  68,  68)),
            new(Color.FromArgb(255, 139,  92, 246)),
            new(Color.FromArgb(255,  20, 184, 166)),
        };

        public object Convert(object value, Type targetType,
            object parameter, string language)
        {
            if (value is not string name || string.IsNullOrEmpty(name))
                return Brushes[0];
            var index = Math.Abs(name.GetHashCode()) % Brushes.Length;
            return Brushes[index];
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, string language)
            => throw new NotImplementedException();
    }

    public class ActivityDotColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush Green = new(Colors.Green);
        private static readonly SolidColorBrush Red = new(Colors.Red);
        private static readonly SolidColorBrush LightGreen = new(Colors.LimeGreen);
        private static readonly SolidColorBrush OrangeRed = new(Colors.OrangeRed);
        private static readonly SolidColorBrush SteelBlue = new(Colors.SteelBlue);
        private static readonly SolidColorBrush Orange = new(Colors.Orange);
        private static readonly SolidColorBrush Gray = new(Colors.Gray);

        public object Convert(object value, Type targetType,
            object parameter, string language)
            => value is string s
                ? s switch
                {
                    "folder_added" => Green,
                    "folder_removed" => Red,
                    "files_added" => LightGreen,
                    "files_removed" => OrangeRed,
                    "drive_reindexed" => SteelBlue,
                    "log_reset" => Orange,
                    "unauthorized_attempt" => Red,
                    _ => Gray
                }
                : Gray;

        public object ConvertBack(object value, Type targetType,
            object parameter, string language)
            => throw new NotImplementedException();
    }

    public class SubInfoVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, string language)
            => value is string s && !string.IsNullOrEmpty(s)
                ? Microsoft.UI.Xaml.Visibility.Visible
                : Microsoft.UI.Xaml.Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType,
            object parameter, string language)
            => throw new NotImplementedException();
    }
}