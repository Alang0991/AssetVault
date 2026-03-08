using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using AssetVault.Models;

namespace AssetVault.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public string StoragePath { get; }

        public DatabaseService()
        {
            var appFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AssetVault");

            Directory.CreateDirectory(appFolder);

            StoragePath = Path.Combine(appFolder, "Assets");
            Directory.CreateDirectory(StoragePath);

            CreateAssetFolders();

            _dbPath = Path.Combine(appFolder, "assetvault.db");

            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = true
            }.ToString();

            Initialize();
            EnsureColumns();
            EnsureIndexes();
        }

        private SqliteConnection CreateConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var pragma = connection.CreateCommand();
            pragma.CommandText = @"
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA temp_store=MEMORY;
PRAGMA foreign_keys=ON;";
            pragma.ExecuteNonQuery();

            return connection;
        }

        private void CreateAssetFolders()
        {
            string[] folders =
            {
                "Avatars",
                "Clothing",
                "Accessories",
                "Hair",
                "Props",
                "World Assets",
                "Textures",
                "Shaders",
                "Animations",
                "Audio",
                "Unity Packages",
                "NSFW",
                "Other"
            };

            foreach (var folder in folders)
            {
                Directory.CreateDirectory(Path.Combine(StoragePath, folder));
            }
        }

        private void Initialize()
        {
            using var connection = CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Assets (
Id INTEGER PRIMARY KEY AUTOINCREMENT,
Name TEXT,
Category TEXT,
SubType TEXT,
FilePath TEXT,
Tags TEXT,
Creator TEXT,
Notes TEXT,
PreviewImagePath TEXT,
ThumbnailPath TEXT,
FileHash TEXT,
FileType TEXT,
FileSize INTEGER,
DateAdded TEXT,
IsFavorite INTEGER DEFAULT 0,
Rating INTEGER DEFAULT 0
);";

            command.ExecuteNonQuery();
        }

        private void EnsureColumns()
        {
            using var connection = CreateConnection();

            EnsureColumn(connection, "ThumbnailPath", "TEXT");
            EnsureColumn(connection, "FileHash", "TEXT");
            EnsureColumn(connection, "FileSize", "INTEGER");
        }

        private void EnsureColumn(SqliteConnection connection, string columnName, string definition)
        {
            using var check = connection.CreateCommand();
            check.CommandText = "PRAGMA table_info(Assets);";

            bool exists = false;

            using var reader = check.ExecuteReader();

            while (reader.Read())
            {
                if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE Assets ADD COLUMN {columnName} {definition};";
                alter.ExecuteNonQuery();
            }
        }

        private void EnsureIndexes()
        {
            using var connection = CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
CREATE INDEX IF NOT EXISTS IX_Category ON Assets(Category);
CREATE INDEX IF NOT EXISTS IX_Date ON Assets(DateAdded DESC);
CREATE INDEX IF NOT EXISTS IX_Favorite ON Assets(IsFavorite);
CREATE INDEX IF NOT EXISTS IX_Rating ON Assets(Rating);
CREATE INDEX IF NOT EXISTS IX_Hash ON Assets(FileHash);
CREATE INDEX IF NOT EXISTS IX_Name ON Assets(Name);";

            command.ExecuteNonQuery();
        }

        private AssetItem ReadAsset(SqliteDataReader reader)
        {
            DateTime dateAdded = DateTime.Now;

            if (reader["DateAdded"] != DBNull.Value)
                DateTime.TryParse(reader["DateAdded"].ToString(), out dateAdded);

            string category = reader["Category"]?.ToString() ?? "";

            if (string.IsNullOrWhiteSpace(category))
                category = "Unity Packages";

            return new AssetItem
            {
                Id = Convert.ToInt32(reader["Id"]),
                Name = reader["Name"]?.ToString() ?? "",
                Category = category,
                SubType = reader["SubType"]?.ToString() ?? "",
                FilePath = reader["FilePath"]?.ToString() ?? "",
                Tags = reader["Tags"]?.ToString() ?? "",
                Creator = reader["Creator"]?.ToString() ?? "",
                Notes = reader["Notes"]?.ToString() ?? "",
                PreviewImagePath = reader["PreviewImagePath"]?.ToString() ?? "",
                ThumbnailPath = reader["ThumbnailPath"]?.ToString() ?? "",
                FileHash = reader["FileHash"]?.ToString() ?? "",
                FileType = reader["FileType"]?.ToString() ?? "",
                FileSize = reader["FileSize"] != DBNull.Value ? Convert.ToInt64(reader["FileSize"]) : 0,
                DateAdded = dateAdded,
                IsFavorite = reader["IsFavorite"] != DBNull.Value && Convert.ToInt32(reader["IsFavorite"]) == 1,
                Rating = reader["Rating"] != DBNull.Value ? Convert.ToInt32(reader["Rating"]) : 0
            };
        }

        public List<AssetItem> GetAllAssets()
        {
            var assets = new List<AssetItem>();

            using var connection = CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = "SELECT * FROM Assets ORDER BY DateAdded DESC";

            using var reader = command.ExecuteReader();

            while (reader.Read())
                assets.Add(ReadAsset(reader));

            return assets;
        }

        public void InsertAsset(AssetItem asset)
        {
            using var connection = CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
INSERT INTO Assets
(Name,Category,SubType,FilePath,Tags,Creator,Notes,
PreviewImagePath,ThumbnailPath,FileHash,FileType,
FileSize,DateAdded,IsFavorite,Rating)
VALUES
($name,$cat,$sub,$path,$tags,$creator,$notes,
$preview,$thumb,$hash,$type,$size,$date,$fav,$rating);";

            command.Parameters.AddWithValue("$name", asset.Name ?? "");
            command.Parameters.AddWithValue("$cat", string.IsNullOrWhiteSpace(asset.Category) ? "Unity Packages" : asset.Category);
            command.Parameters.AddWithValue("$sub", asset.SubType ?? "");
            command.Parameters.AddWithValue("$path", asset.FilePath ?? "");
            command.Parameters.AddWithValue("$tags", asset.Tags ?? "");
            command.Parameters.AddWithValue("$creator", asset.Creator ?? "");
            command.Parameters.AddWithValue("$notes", asset.Notes ?? "");
            command.Parameters.AddWithValue("$preview", asset.PreviewImagePath ?? "");
            command.Parameters.AddWithValue("$thumb", asset.ThumbnailPath ?? "");
            command.Parameters.AddWithValue("$hash", asset.FileHash ?? "");
            command.Parameters.AddWithValue("$type", asset.FileType ?? "");
            command.Parameters.AddWithValue("$size", asset.FileSize);
            command.Parameters.AddWithValue("$date", DateTime.Now.ToString("o"));
            command.Parameters.AddWithValue("$fav", asset.IsFavorite ? 1 : 0);
            command.Parameters.AddWithValue("$rating", asset.Rating);

            command.ExecuteNonQuery();
        }

        public void UpdateAsset(AssetItem asset)
        {
            asset.ClearTagCache();

            using var connection = CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = @"
UPDATE Assets
SET
Name=$name,
Category=$cat,
SubType=$sub,
Tags=$tags,
Creator=$creator,
Notes=$notes,
PreviewImagePath=$preview,
ThumbnailPath=$thumb,
FileHash=$hash,
IsFavorite=$fav,
Rating=$rating
WHERE Id=$id;";

            command.Parameters.AddWithValue("$id", asset.Id);
            command.Parameters.AddWithValue("$name", asset.Name ?? "");
            command.Parameters.AddWithValue("$cat", string.IsNullOrWhiteSpace(asset.Category) ? "Unity Packages" : asset.Category);
            command.Parameters.AddWithValue("$sub", asset.SubType ?? "");
            command.Parameters.AddWithValue("$tags", asset.Tags ?? "");
            command.Parameters.AddWithValue("$creator", asset.Creator ?? "");
            command.Parameters.AddWithValue("$notes", asset.Notes ?? "");
            command.Parameters.AddWithValue("$preview", asset.PreviewImagePath ?? "");
            command.Parameters.AddWithValue("$thumb", asset.ThumbnailPath ?? "");
            command.Parameters.AddWithValue("$hash", asset.FileHash ?? "");
            command.Parameters.AddWithValue("$fav", asset.IsFavorite ? 1 : 0);
            command.Parameters.AddWithValue("$rating", asset.Rating);

            command.ExecuteNonQuery();
        }

        public void DeleteAsset(int id)
        {
            using var connection = CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = "DELETE FROM Assets WHERE Id=$id";
            command.Parameters.AddWithValue("$id", id);

            command.ExecuteNonQuery();
        }

        public void RefreshFileSizes()
        {
            var assets = GetAllAssets();

            using var connection = CreateConnection();

            foreach (var asset in assets)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(asset.FilePath))
                        continue;

                    if (!File.Exists(asset.FilePath))
                        continue;

                    var size = new FileInfo(asset.FilePath).Length;

                    using var command = connection.CreateCommand();
                    command.CommandText = "UPDATE Assets SET FileSize=$size WHERE Id=$id";
                    command.Parameters.AddWithValue("$size", size);
                    command.Parameters.AddWithValue("$id", asset.Id);
                    command.ExecuteNonQuery();
                }
                catch
                {
                }
            }
        }

        public void RebuildLibraryMetadata()
        {
            var assets = GetAllAssets();

            using var connection = CreateConnection();

            foreach (var asset in assets)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(asset.FilePath))
                        continue;

                    if (!File.Exists(asset.FilePath))
                        continue;

                    var file = new FileInfo(asset.FilePath);

                    using var command = connection.CreateCommand();
                    command.CommandText = @"
UPDATE Assets
SET
FileSize=$size,
FileType=$type
WHERE Id=$id";

                    command.Parameters.AddWithValue("$size", file.Length);
                    command.Parameters.AddWithValue("$type", file.Extension.ToLower());
                    command.Parameters.AddWithValue("$id", asset.Id);

                    command.ExecuteNonQuery();
                }
                catch
                {
                }
            }
        }

        public DashboardStats GetDashboardStats()
        {
            var all = GetAllAssets();

            long totalStorage = 0;

            foreach (var asset in all)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(asset.FilePath) && File.Exists(asset.FilePath))
                        totalStorage += new FileInfo(asset.FilePath).Length;
                }
                catch
                {
                }
            }

            return new DashboardStats
            {
                TotalAssets = all.Count,
                ClothingAssets = all.Count(x => x.Category == "Clothing"),
                AccessoriesAssets = all.Count(x => x.Category == "Accessories"),
                HairAssets = all.Count(x => x.Category == "Hair"),
                FavoriteAssets = all.Count(x => x.IsFavorite),
                TopRatedAssets = all.Count(x => x.Rating >= 4),
                TotalStorageBytes = totalStorage,
                RecentAssets = all.Take(8).ToList()
            };
        }
    }

    public class DashboardStats
    {
        public int TotalAssets { get; set; }
        public int ClothingAssets { get; set; }
        public int AccessoriesAssets { get; set; }
        public int HairAssets { get; set; }
        public int FavoriteAssets { get; set; }
        public int TopRatedAssets { get; set; }
        public long TotalStorageBytes { get; set; }
        public List<AssetItem> RecentAssets { get; set; } = new();
    }
}