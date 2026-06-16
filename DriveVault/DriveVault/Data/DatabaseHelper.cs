using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using Windows.Storage;
using System.IO;

namespace DriveVault.Data
{
    public class DatabaseHelper
    {
        private static string DbPath =>
            Path.Combine(ApplicationData.Current.LocalFolder.Path, "drivevault.db");

        private static SqliteConnection GetConnection()
        {
            var conn = new SqliteConnection($"Data Source={DbPath};Pooling=False;");
            conn.Open();
            return conn;
        }

        public static void InitializeDatabase()
        {
            using var conn = GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            cmd.ExecuteNonQuery();
            cmd.CommandText = "PRAGMA busy_timeout=5000;";
            cmd.ExecuteNonQuery();

            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS Drives (" +
                "Id TEXT PRIMARY KEY," +
                "Label TEXT," +
                "MountPath TEXT," +
                "TotalBytes INTEGER," +
                "UsedBytes INTEGER," +
                "DriveType TEXT," +
                "SerialNumber TEXT," +
                "IsConnected INTEGER DEFAULT 0," +
                "HealthStatus TEXT DEFAULT 'Good'," +
                "HealthScore INTEGER DEFAULT 100," +
                "TemperatureCelsius REAL DEFAULT 0," +
                "IsFullyIndexed INTEGER DEFAULT 0," +
                "FirstSeen TEXT," +
                "LastSeen TEXT);";
            cmd.ExecuteNonQuery();

            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS Folders (" +
                "Id TEXT PRIMARY KEY," +
                "DriveId TEXT," +
                "FolderName TEXT," +
                "FolderPath TEXT," +
                "SizeBytes INTEGER," +
                "FileCount INTEGER," +
                "FileTypeSummary TEXT DEFAULT ''," +
                "IsTopLevel INTEGER DEFAULT 1," +
                "FirstSeen TEXT," +
                "LastSeen TEXT);";
            cmd.ExecuteNonQuery();

            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS ActivityLog (" +
                "Id TEXT PRIMARY KEY," +
                "EventType TEXT," +
                "DriveId TEXT," +
                "FolderId TEXT," +
                "FolderName TEXT," +
                "DriveName TEXT," +
                "Timestamp TEXT);";
            cmd.ExecuteNonQuery();

            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS DriveAlerts (" +
                "Id TEXT PRIMARY KEY," +
                "DriveId TEXT UNIQUE," +
                "SnoozedUntil TEXT," +
                "CreatedAt TEXT);";
            cmd.ExecuteNonQuery();

            cmd.CommandText =
                "CREATE TABLE IF NOT EXISTS AppSettings (" +
                "Key TEXT PRIMARY KEY," +
                "Value TEXT);";
            cmd.ExecuteNonQuery();

            // ─── Migrations ───────────────────────────────────────
            try { cmd.CommandText = "ALTER TABLE Folders ADD COLUMN FileTypeSummary TEXT DEFAULT ''"; cmd.ExecuteNonQuery(); } catch { }
            try { cmd.CommandText = "ALTER TABLE Folders ADD COLUMN IsTopLevel INTEGER DEFAULT 1"; cmd.ExecuteNonQuery(); } catch { }

            // ✅ ActivityLog new columns
            try { cmd.CommandText = "ALTER TABLE ActivityLog ADD COLUMN FileTypeSummary TEXT DEFAULT ''"; cmd.ExecuteNonQuery(); } catch { }
            try { cmd.CommandText = "ALTER TABLE ActivityLog ADD COLUMN FileCount INTEGER DEFAULT 0"; cmd.ExecuteNonQuery(); } catch { }
            try { cmd.CommandText = "ALTER TABLE ActivityLog ADD COLUMN SizeBytes INTEGER DEFAULT 0"; cmd.ExecuteNonQuery(); } catch { }

            // ✅ Duplicate drives cleanup
            try
            {
                cmd.CommandText =
                    "DELETE FROM Drives WHERE Id NOT IN (" +
                    "SELECT Id FROM Drives d1 " +
                    "WHERE LastSeen = (" +
                    "SELECT MAX(d2.LastSeen) FROM Drives d2 " +
                    "WHERE d2.Label     = d1.Label " +
                    "AND   d2.MountPath = d1.MountPath)" +
                    ")";
                cmd.ExecuteNonQuery();
            }
            catch { }

            // ✅ Orphan folders cleanup
            try
            {
                cmd.CommandText =
                    "DELETE FROM Folders WHERE DriveId NOT IN " +
                    "(SELECT Id FROM Drives)";
                cmd.ExecuteNonQuery();
            }
            catch { }

            // ✅ Existing subfolders fix
            try
            {
                cmd.CommandText =
                    "UPDATE Folders SET IsTopLevel = 0 " +
                    "WHERE Id IN (" +
                    "SELECT f.Id FROM Folders f " +
                    "JOIN Drives d ON f.DriveId = d.Id " +
                    "WHERE f.FolderPath != (d.MountPath || f.FolderName) " +
                    "AND   f.FolderPath != (d.MountPath || '\\' || f.FolderName)" +
                    ")";
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        // ─── Drives ───────────────────────────────────────────────
        public static List<Drive> GetAllDrives()
        {
            var list = new List<Drive>();
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM Drives ORDER BY LastSeen DESC";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(new Drive
                    {
                        Id = reader["Id"]?.ToString() ?? "",
                        Label = reader["Label"]?.ToString() ?? "",
                        MountPath = reader["MountPath"]?.ToString() ?? "",
                        TotalBytes = Convert.ToInt64(reader["TotalBytes"]),
                        UsedBytes = Convert.ToInt64(reader["UsedBytes"]),
                        DriveType = reader["DriveType"]?.ToString() ?? "Unknown",
                        SerialNumber = reader["SerialNumber"]?.ToString() ?? "",
                        IsConnected = Convert.ToInt32(reader["IsConnected"]) == 1,
                        HealthStatus = reader["HealthStatus"]?.ToString() ?? "Good",
                        HealthScore = Convert.ToInt32(reader["HealthScore"]),
                        IsFullyIndexed = Convert.ToInt32(reader["IsFullyIndexed"]) == 1,
                        LastSeen = DateTime.TryParse(reader["LastSeen"]?.ToString(),
                            out var ls) ? ls : DateTime.Now
                    });
            }
            catch { }
            return list;
        }

        public static void SaveDrive(Drive drive)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO Drives (Id,Label,MountPath,TotalBytes,UsedBytes,DriveType," +
                    "SerialNumber,IsConnected,HealthStatus,HealthScore,TemperatureCelsius," +
                    "IsFullyIndexed,FirstSeen,LastSeen) " +
                    "VALUES ($id,$label,$path,$total,$used,$type,$serial,$connected,$health," +
                    "$score,$temp,$indexed,$first,$last) " +
                    "ON CONFLICT(Id) DO UPDATE SET " +
                    "Label=$label,MountPath=$path,TotalBytes=$total,UsedBytes=$used," +
                    "IsConnected=$connected,HealthStatus=$health,HealthScore=$score," +
                    "TemperatureCelsius=$temp,IsFullyIndexed=$indexed,LastSeen=$last";

                cmd.Parameters.AddWithValue("$id", drive.Id);
                cmd.Parameters.AddWithValue("$label", drive.Label);
                cmd.Parameters.AddWithValue("$path", drive.MountPath);
                cmd.Parameters.AddWithValue("$total", drive.TotalBytes);
                cmd.Parameters.AddWithValue("$used", drive.UsedBytes);
                cmd.Parameters.AddWithValue("$type", drive.DriveType);
                cmd.Parameters.AddWithValue("$serial", drive.SerialNumber);
                cmd.Parameters.AddWithValue("$connected", drive.IsConnected ? 1 : 0);
                cmd.Parameters.AddWithValue("$health", drive.HealthStatus);
                cmd.Parameters.AddWithValue("$score", drive.HealthScore);
                cmd.Parameters.AddWithValue("$temp", drive.TemperatureCelsius);
                cmd.Parameters.AddWithValue("$indexed", drive.IsFullyIndexed ? 1 : 0);
                cmd.Parameters.AddWithValue("$first", drive.FirstSeen.ToString("o"));
                cmd.Parameters.AddWithValue("$last", drive.LastSeen.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static void SetAllDrivesOffline()
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "UPDATE Drives SET IsConnected = 0";
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static void RemoveDrive(string driveId)
        {
            try
            {
                using var conn = GetConnection();

                using var pragma = conn.CreateCommand();
                pragma.CommandText = "PRAGMA foreign_keys = OFF";
                pragma.ExecuteNonQuery();

                using var cmd1 = conn.CreateCommand();
                cmd1.CommandText = "DELETE FROM Folders WHERE DriveId=$id";
                cmd1.Parameters.AddWithValue("$id", driveId);
                cmd1.ExecuteNonQuery();

                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "DELETE FROM DriveAlerts WHERE DriveId=$id";
                cmd2.Parameters.AddWithValue("$id", driveId);
                cmd2.ExecuteNonQuery();

                using var cmd3 = conn.CreateCommand();
                cmd3.CommandText = "DELETE FROM Drives WHERE Id=$id";
                cmd3.Parameters.AddWithValue("$id", driveId);
                cmd3.ExecuteNonQuery();
            }
            catch { }
        }

        // ─── Folders ──────────────────────────────────────────────
        public static List<DriveFolder> GetAllFolders()
        {
            var list = new List<DriveFolder>();
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT * FROM Folders WHERE IsTopLevel=1 ORDER BY LastSeen DESC";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(ReadFolder(reader));
            }
            catch { }
            return list;
        }

        public static List<DriveFolder> GetFoldersByDrive(string driveId)
        {
            var list = new List<DriveFolder>();
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT * FROM Folders WHERE DriveId=$id ORDER BY FolderPath ASC";
                cmd.Parameters.AddWithValue("$id", driveId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(ReadFolder(reader));
            }
            catch { }
            return list;
        }

        private static DriveFolder ReadFolder(SqliteDataReader reader)
        {
            return new DriveFolder
            {
                Id = reader["Id"]?.ToString() ?? "",
                DriveId = reader["DriveId"]?.ToString() ?? "",
                FolderName = reader["FolderName"]?.ToString() ?? "",
                FolderPath = reader["FolderPath"]?.ToString() ?? "",
                SizeBytes = Convert.ToInt64(reader["SizeBytes"]),
                FileCount = Convert.ToInt32(reader["FileCount"]),
                FileTypeSummary = reader["FileTypeSummary"]?.ToString() ?? "",
                IsTopLevel = reader["IsTopLevel"] == DBNull.Value ||
                                  Convert.ToInt32(reader["IsTopLevel"]) == 1,
                FirstSeen = DateTime.TryParse(reader["FirstSeen"]?.ToString(),
                    out var fs) ? fs : DateTime.Now,
                LastSeen = DateTime.TryParse(reader["LastSeen"]?.ToString(),
                    out var ls) ? ls : DateTime.Now
            };
        }

        public static void SaveFolder(DriveFolder folder)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO Folders (Id,DriveId,FolderName,FolderPath,SizeBytes," +
                    "FileCount,FileTypeSummary,IsTopLevel,FirstSeen,LastSeen) " +
                    "VALUES ($id,$driveId,$name,$path,$size,$count,$types,$topLevel,$first,$last) " +
                    "ON CONFLICT(Id) DO UPDATE SET SizeBytes=$size,FileCount=$count," +
                    "FileTypeSummary=$types,IsTopLevel=$topLevel,LastSeen=$last";

                cmd.Parameters.AddWithValue("$id", folder.Id);
                cmd.Parameters.AddWithValue("$driveId", folder.DriveId);
                cmd.Parameters.AddWithValue("$name", folder.FolderName);
                cmd.Parameters.AddWithValue("$path", folder.FolderPath);
                cmd.Parameters.AddWithValue("$size", folder.SizeBytes);
                cmd.Parameters.AddWithValue("$count", folder.FileCount);
                cmd.Parameters.AddWithValue("$types", folder.FileTypeSummary);
                cmd.Parameters.AddWithValue("$topLevel", folder.IsTopLevel ? 1 : 0);
                cmd.Parameters.AddWithValue("$first", folder.FirstSeen.ToString("o"));
                cmd.Parameters.AddWithValue("$last", folder.LastSeen.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static void DeleteFolder(string folderId)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM Folders WHERE Id=$id";
                cmd.Parameters.AddWithValue("$id", folderId);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        // ─── Activity Log ─────────────────────────────────────────

        // ✅ Original 5 param version — unchanged
        public static void LogActivity(string eventType, string driveId,
            string folderId, string folderName, string driveName)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO ActivityLog (Id,EventType,DriveId,FolderId," +
                    "FolderName,DriveName,FileTypeSummary,FileCount,SizeBytes,Timestamp) " +
                    "VALUES ($id,$type,$driveId,$folderId,$folderName,$driveName," +
                    "'',0,0,$time)";
                cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("$type", eventType);
                cmd.Parameters.AddWithValue("$driveId", driveId);
                cmd.Parameters.AddWithValue("$folderId", folderId);
                cmd.Parameters.AddWithValue("$folderName", folderName);
                cmd.Parameters.AddWithValue("$driveName", driveName);
                cmd.Parameters.AddWithValue("$time", DateTime.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        // ✅ Extended 8 param version — FileTypeSummary, FileCount, SizeBytes తో
        public static void LogActivity(string eventType, string driveId,
            string folderId, string folderName, string driveName,
            string fileTypeSummary, int fileCount, long sizeBytes)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO ActivityLog (Id,EventType,DriveId,FolderId," +
                    "FolderName,DriveName,FileTypeSummary,FileCount,SizeBytes,Timestamp) " +
                    "VALUES ($id,$type,$driveId,$folderId,$folderName,$driveName," +
                    "$types,$count,$size,$time)";
                cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("$type", eventType);
                cmd.Parameters.AddWithValue("$driveId", driveId);
                cmd.Parameters.AddWithValue("$folderId", folderId);
                cmd.Parameters.AddWithValue("$folderName", folderName);
                cmd.Parameters.AddWithValue("$driveName", driveName);
                cmd.Parameters.AddWithValue("$types", fileTypeSummary);
                cmd.Parameters.AddWithValue("$count", fileCount);
                cmd.Parameters.AddWithValue("$size", sizeBytes);
                cmd.Parameters.AddWithValue("$time", DateTime.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        // ✅ Settings change log చేయడానికి
        public static void LogSettingChange(string settingName,
            string oldValue, string newValue)
        {
            LogActivity(
                "setting_changed", "", "",
                $"{settingName}: {oldValue} → {newValue}",
                "Settings Updated.");
        }

        public static List<ActivityLog> GetRecentActivity(int days = 7)
        {
            var list = new List<ActivityLog>();
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT * FROM ActivityLog " +
                    "WHERE Timestamp >= $since " +
                    "AND EventType IN (" +
                    "'drive_connected','drive_disconnected','drive_removed'," +
                    "'drive_reindexed','app_reset','log_reset','log_exported'" +
                    ") " +
                    "ORDER BY Timestamp DESC LIMIT 7";
                cmd.Parameters.AddWithValue("$since",
                    DateTime.Now.AddDays(-days).ToString("o"));
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(ReadActivity(reader));
            }
            catch { }
            return list;
        }

        public static List<ActivityLog> GetAllActivity()
        {
            var list = new List<ActivityLog>();
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM ActivityLog ORDER BY Timestamp DESC";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(ReadActivity(reader));
            }
            catch { }
            return list;
        }

        private static ActivityLog ReadActivity(SqliteDataReader reader)
        {
            return new ActivityLog
            {
                Id = reader["Id"]?.ToString() ?? "",
                EventType = reader["EventType"]?.ToString() ?? "",
                DriveId = reader["DriveId"]?.ToString() ?? "",
                FolderId = reader["FolderId"]?.ToString() ?? "",
                FolderName = reader["FolderName"]?.ToString() ?? "",
                DriveName = reader["DriveName"]?.ToString() ?? "",
                // ✅ New fields — DBNull safe
                FileTypeSummary = reader["FileTypeSummary"] == DBNull.Value ? ""
                                  : reader["FileTypeSummary"]?.ToString() ?? "",
                FileCount = reader["FileCount"] == DBNull.Value ? 0
                                  : Convert.ToInt32(reader["FileCount"]),
                SizeBytes = reader["SizeBytes"] == DBNull.Value ? 0
                                  : Convert.ToInt64(reader["SizeBytes"]),
                Timestamp = DateTime.TryParse(reader["Timestamp"]?.ToString(),
                                  out var ts) ? ts : DateTime.Now
            };
        }

        public static void ResetActivityLog()
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM ActivityLog";
                cmd.ExecuteNonQuery();
                LogActivity("log_reset", "", "", "Activity log reset", "System");
            }
            catch { }
        }

        // ─── Alerts ───────────────────────────────────────────────
        public static void SnoozeAlert(string driveId, int days)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO DriveAlerts (Id,DriveId,SnoozedUntil,CreatedAt) " +
                    "VALUES ($id,$driveId,$until,$created) " +
                    "ON CONFLICT(DriveId) DO UPDATE SET SnoozedUntil=$until";
                cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue("$driveId", driveId);
                cmd.Parameters.AddWithValue("$until",
                    DateTime.Now.AddDays(days).ToString("o"));
                cmd.Parameters.AddWithValue("$created", DateTime.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static bool IsDriveSnoozed(string driveId)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT SnoozedUntil FROM DriveAlerts WHERE DriveId=$id";
                cmd.Parameters.AddWithValue("$id", driveId);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var until = reader["SnoozedUntil"]?.ToString();
                    if (DateTime.TryParse(until, out var dt))
                        return DateTime.Now < dt;
                }
            }
            catch { }
            return false;
        }

        public static List<Drive> GetActiveAlerts()
        {
            var drives = GetAllDrives();
            var alerts = new List<Drive>();
            foreach (var drive in drives)
                if (drive.IsNearlyFull && !IsDriveSnoozed(drive.Id))
                    alerts.Add(drive);
            return alerts;
        }

        // ─── Settings ─────────────────────────────────────────────
        public static void SaveSetting(string key, string value)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO AppSettings (Key,Value) VALUES ($key,$value) " +
                    "ON CONFLICT(Key) DO UPDATE SET Value=$value";
                cmd.Parameters.AddWithValue("$key", key);
                cmd.Parameters.AddWithValue("$value", value);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static string GetSetting(string key, string defaultValue = "")
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key=$key";
                cmd.Parameters.AddWithValue("$key", key);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                    return reader["Value"]?.ToString() ?? defaultValue;
            }
            catch { }
            return defaultValue;
        }

        // ─── Workflow ─────────────────────────────────────────────
        public static List<ClientWorkflow> GetAllWorkflows()
        {
            var list = new List<ClientWorkflow>();
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT name FROM sqlite_master " +
                    "WHERE type='table' AND name='ClientWorkflows'";
                var exists = cmd.ExecuteScalar();
                if (exists == null) return list;

                cmd.CommandText = "SELECT * FROM ClientWorkflows";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    list.Add(ReadWorkflow(reader));
            }
            catch { }
            return list;
        }

        public static ClientWorkflow? GetWorkflow(string clientName)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT name FROM sqlite_master " +
                    "WHERE type='table' AND name='ClientWorkflows'";
                var exists = cmd.ExecuteScalar();
                if (exists == null) return null;

                cmd.CommandText = "SELECT * FROM ClientWorkflows WHERE ClientName=$name";
                cmd.Parameters.AddWithValue("$name", clientName);
                using var reader = cmd.ExecuteReader();
                if (reader.Read()) return ReadWorkflow(reader);
            }
            catch { }
            return null;
        }

        public static void SaveWorkflow(ClientWorkflow wf)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();

                cmd.CommandText =
                    "CREATE TABLE IF NOT EXISTS ClientWorkflows (" +
                    "ClientName TEXT PRIMARY KEY," +
                    "SelectionLinkStatus TEXT DEFAULT 'Not Shared'," +
                    "ClientHDDCopyStatus TEXT DEFAULT 'Not Shared'," +
                    "EditedPhotosStatus TEXT DEFAULT 'NA'," +
                    "CinematicVideoStatus TEXT DEFAULT 'NA'," +
                    "TraditionalVideoStatus TEXT DEFAULT 'NA'," +
                    "AlbumDesigningStatus TEXT DEFAULT 'NA'," +
                    "CompleteProjectStatus TEXT DEFAULT 'NA'," +
                    "Notes TEXT DEFAULT ''," +
                    "ProjectStartDate TEXT," +
                    "LastUpdatedAt TEXT)";
                cmd.ExecuteNonQuery();

                cmd.CommandText =
                    "INSERT INTO ClientWorkflows (" +
                    "ClientName,SelectionLinkStatus,ClientHDDCopyStatus," +
                    "EditedPhotosStatus,CinematicVideoStatus,TraditionalVideoStatus," +
                    "AlbumDesigningStatus,CompleteProjectStatus,Notes," +
                    "ProjectStartDate,LastUpdatedAt) " +
                    "VALUES ($name,$sel,$hdd,$photos,$cin,$trad,$album,$complete,$notes,$start,$updated) " +
                    "ON CONFLICT(ClientName) DO UPDATE SET " +
                    "SelectionLinkStatus=$sel,ClientHDDCopyStatus=$hdd," +
                    "EditedPhotosStatus=$photos,CinematicVideoStatus=$cin," +
                    "TraditionalVideoStatus=$trad,AlbumDesigningStatus=$album," +
                    "CompleteProjectStatus=$complete,Notes=$notes," +
                    "ProjectStartDate=$start,LastUpdatedAt=$updated";

                cmd.Parameters.AddWithValue("$name", wf.ClientName);
                cmd.Parameters.AddWithValue("$sel", wf.SelectionLinkStatus);
                cmd.Parameters.AddWithValue("$hdd", wf.ClientHDDCopyStatus);
                cmd.Parameters.AddWithValue("$photos", wf.EditedPhotosStatus);
                cmd.Parameters.AddWithValue("$cin", wf.CinematicVideoStatus);
                cmd.Parameters.AddWithValue("$trad", wf.TraditionalVideoStatus);
                cmd.Parameters.AddWithValue("$album", wf.AlbumDesigningStatus);
                cmd.Parameters.AddWithValue("$complete", wf.CompleteProjectStatus);
                cmd.Parameters.AddWithValue("$notes", wf.Notes);
                cmd.Parameters.AddWithValue("$start", wf.ProjectStartDate.ToString("o"));
                cmd.Parameters.AddWithValue("$updated", wf.LastUpdatedAt.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static void DeleteWorkflow(string clientName)
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT name FROM sqlite_master " +
                    "WHERE type='table' AND name='ClientWorkflows'";
                var exists = cmd.ExecuteScalar();
                if (exists == null) return;

                cmd.CommandText = "DELETE FROM ClientWorkflows WHERE ClientName=$name";
                cmd.Parameters.AddWithValue("$name", clientName);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private static ClientWorkflow ReadWorkflow(SqliteDataReader reader)
        {
            return new ClientWorkflow
            {
                ClientName = reader["ClientName"]?.ToString() ?? "",
                SelectionLinkStatus = reader["SelectionLinkStatus"]?.ToString() ?? "Not Shared",
                ClientHDDCopyStatus = reader["ClientHDDCopyStatus"]?.ToString() ?? "Not Shared",
                EditedPhotosStatus = reader["EditedPhotosStatus"]?.ToString() ?? "NA",
                CinematicVideoStatus = reader["CinematicVideoStatus"]?.ToString() ?? "NA",
                TraditionalVideoStatus = reader["TraditionalVideoStatus"]?.ToString() ?? "NA",
                AlbumDesigningStatus = reader["AlbumDesigningStatus"]?.ToString() ?? "NA",
                CompleteProjectStatus = reader["CompleteProjectStatus"]?.ToString() ?? "NA",
                Notes = reader["Notes"]?.ToString() ?? "",
                ProjectStartDate = DateTime.TryParse(
                    reader["ProjectStartDate"]?.ToString(), out var sd) ? sd : DateTime.Now,
                LastUpdatedAt = DateTime.TryParse(
                    reader["LastUpdatedAt"]?.ToString(), out var lu) ? lu : DateTime.Now
            };
        }
    }
}