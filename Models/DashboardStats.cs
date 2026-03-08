using System.Collections.Generic;

namespace AssetVault.Models
{
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