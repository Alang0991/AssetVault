using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetVault.Models
{
    public class AssetItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string SubType { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string Tags { get; set; } = "";
        public string Creator { get; set; } = "";
        public string Notes { get; set; } = "";

        public string PreviewImagePath { get; set; } = "";
        public string ThumbnailPath { get; set; } = "";
        public string FileHash { get; set; } = "";
        public string FileType { get; set; } = "";

        public DateTime DateAdded { get; set; }

        public bool IsFavorite { get; set; }

        public int Rating { get; set; }

        public long FileSize { get; set; }

        public string FileSizeReadable
        {
            get
            {
                double size = FileSize;

                if (size >= 1024d * 1024d * 1024d)
                    return $"{size / (1024d * 1024d * 1024d):0.##} GB";

                if (size >= 1024d * 1024d)
                    return $"{size / (1024d * 1024d):0.##} MB";

                if (size >= 1024d)
                    return $"{size / 1024d:0.##} KB";

                return $"{size:0} B";
            }
        }

        private List<string>? _tagCache;

        public List<string> TagList
        {
            get
            {
                if (_tagCache == null)
                {
                    _tagCache = (Tags ?? "")
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();
                }

                return _tagCache;
            }
        }

        public void ClearTagCache()
        {
            _tagCache = null;
        }
    }
}