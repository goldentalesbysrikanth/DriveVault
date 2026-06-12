using System;

namespace DriveVault.Data
{
    public class Drive
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Label { get; set; } = "";
        public string MountPath { get; set; } = "";
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public string DriveType { get; set; } = "Unknown";
        public string SerialNumber { get; set; } = "";
        public bool IsConnected { get; set; } = false;
        public string HealthStatus { get; set; } = "Good";
        public int HealthScore { get; set; } = 100;
        public double TemperatureCelsius { get; set; } = 0;
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public DateTime FirstSeen { get; set; } = DateTime.Now;
        public bool IsFullyIndexed { get; set; } = false;

        public double UsedPercent => TotalBytes > 0
            ? (UsedBytes * 100.0 / TotalBytes) : 0;
        public long FreeBytes => TotalBytes - UsedBytes;
        public bool IsNearlyFull => UsedPercent >= 85;
    }

    public class DriveFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DriveId { get; set; } = "";
        public string FolderName { get; set; } = "";
        public string FolderPath { get; set; } = "";
        public long SizeBytes { get; set; }
        public int FileCount { get; set; }
        public string FileTypeSummary { get; set; } = "";
        public bool IsTopLevel { get; set; } = true;
        public DateTime FirstSeen { get; set; } = DateTime.Now;
        public DateTime LastSeen { get; set; } = DateTime.Now;

        public string SizeDisplay => FormatSize(SizeBytes);
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

    public class ActivityLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string EventType { get; set; } = "";
        public string DriveId { get; set; } = "";
        public string FolderId { get; set; } = "";
        public string FolderName { get; set; } = "";
        public string DriveName { get; set; } = "";
        public string FileTypeSummary { get; set; } = "";
        public int FileCount { get; set; } = 0;
        public long SizeBytes { get; set; } = 0;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public string SizeDisplay => FormatSize(SizeBytes);
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

    public class ClientWorkflow
    {
        public string ClientName { get; set; } = "";
        public string SelectionLinkStatus { get; set; } = "Not Shared";
        public string ClientHDDCopyStatus { get; set; } = "Not Shared";
        public string EditedPhotosStatus { get; set; } = "NA";
        public string CinematicVideoStatus { get; set; } = "NA";
        public string TraditionalVideoStatus { get; set; } = "NA";
        public string AlbumDesigningStatus { get; set; } = "NA";
        public string CompleteProjectStatus { get; set; } = "NA";
        public string Notes { get; set; } = "";
        public DateTime ProjectStartDate { get; set; } = DateTime.Now;
        public DateTime LastUpdatedAt { get; set; } = DateTime.Now;

        public double ProgressPercent
        {
            get
            {
                var fields = new string[]
                {
            SelectionLinkStatus,
            ClientHDDCopyStatus,
            EditedPhotosStatus,
            CinematicVideoStatus,
            TraditionalVideoStatus,
            AlbumDesigningStatus,
            CompleteProjectStatus
                };

                int done = 0;
                foreach (var f in fields)
                {
                    if (f == "Shared" ||
                        f == "Delivered" ||
                        f == "Completed")
                        done++;
                }

                return fields.Length > 0
                    ? (done * 100.0 / fields.Length)
                    : 0;
            }
        }

        public string ProgressDisplay => $"{ProgressPercent:F0}%";
    }
}