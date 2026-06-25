using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage;

namespace DriveVault.Services
{
    public static class BackupService
    {
        private static string LocalFolder =>
            ApplicationData.Current.LocalFolder.Path;

        private static string DbPath =>
            Path.Combine(LocalFolder, "drivevault.db");

        private static string BackupFolder =>
            Path.Combine(LocalFolder, "Backups");

        // ─── Auto Backup ──────────────────────────────────────────

        /// <summary>
        /// App open అయినప్పుడు call చేయండి — daily backup
        /// </summary>
        public static void AutoBackup()
        {
            try
            {
                if (!File.Exists(DbPath)) return;

                Directory.CreateDirectory(BackupFolder);

                var today = DateTime.Now.ToString("yyyy-MM-dd");
                var backupPath = Path.Combine(BackupFolder,
                    $"drivevault_{today}.db");

                // Today's backup already ఉంటే skip
                if (File.Exists(backupPath)) return;

                File.Copy(DbPath, backupPath, overwrite: true);

                // 7 days కంటే పాతవి delete చేయండి
                CleanOldBackups(7);
            }
            catch { }
        }

        // ─── Manual Backup ────────────────────────────────────────

        public static string CreateManualBackup()
        {
            try
            {
                Directory.CreateDirectory(BackupFolder);

                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
                var backupPath = Path.Combine(BackupFolder,
                    $"drivevault_manual_{timestamp}.db");

                File.Copy(DbPath, backupPath, overwrite: true);
                return backupPath;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        // ─── List Backups ─────────────────────────────────────────

        public static List<BackupInfo> GetAvailableBackups()
        {
            var list = new List<BackupInfo>();

            try
            {
                if (!Directory.Exists(BackupFolder))
                    return list;

                var files = Directory.GetFiles(BackupFolder, "*.db")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList();

                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var isManual = fileName.Contains("manual");

                   
                    DateTime date = info.CreationTime;
                    if (fileName.StartsWith("drivevault_"))
                    {
                        var datePart = fileName
    .Replace("drivevault_manual_", "")
    .Replace("drivevault_", "");

                        // ✅ Parse correctly — format is yyyy-MM-dd or yyyy-MM-dd_HH-mm
                        var normalized = datePart.Replace("_", " ").Replace("-", "/");
                        // Fix time part: "2026/06/20 14/30" → "2026/06/20 14:30"
                        if (normalized.Length > 10)
                        {
                            var datePortion = normalized.Substring(0, 10);
                            var timePortion = normalized.Substring(11).Replace("/", ":");
                            normalized = datePortion + " " + timePortion;
                        }
                        if (!DateTime.TryParse(normalized, out date))
                            date = info.CreationTime;
                    }

                    var diff = DateTime.Now - date;
                    var when = diff.TotalMinutes < 60
                        ? $"{(int)diff.TotalMinutes}m ago"
                        : diff.TotalHours < 24
                        ? $"{(int)diff.TotalHours}h ago"
                        : diff.TotalDays < 7
                        ? $"{(int)diff.TotalDays}d ago"
                        : date.ToString("MMM dd, yyyy");

                    list.Add(new BackupInfo
                    {
                        FilePath = file,
                        DisplayName = isManual
                            ? $"Manual backup — {date:MMM dd, yyyy · hh:mm tt}"
                            : $"Auto backup — {date:MMM dd, yyyy}",
                        TimeAgo = when,
                        SizeDisplay = FormatSize(info.Length),
                        IsManual = isManual,
                        CreatedAt = date
                    });
                }
            }
            catch { }

            return list;
        }

        // ─── Restore ──────────────────────────────────────────────

        public static bool RestoreBackup(string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath)) return false;

                // Current db ని temp గా backup చేయండి before restore
                var tempPath = DbPath + ".restore_temp";
                File.Copy(DbPath, tempPath, overwrite: true);

                // Restore చేయండి
                File.Copy(backupPath, DbPath, overwrite: true);

                // Temp delete చేయండి
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ─── Clean Old Backups ────────────────────────────────────

        private static void CleanOldBackups(int keepDays)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-keepDays);
                var files = Directory.GetFiles(BackupFolder, "drivevault_202*.db");

                foreach (var file in files)
                {
                    // Manual backups తీసేయకండి
                    if (file.Contains("manual")) continue;

                    if (File.GetCreationTime(file) < cutoff)
                        File.Delete(file);
                }
            }
            catch { }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }
    }

    public class BackupInfo
    {
        public string FilePath { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string TimeAgo { get; set; } = "";
        public string SizeDisplay { get; set; } = "";
        public bool IsManual { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}