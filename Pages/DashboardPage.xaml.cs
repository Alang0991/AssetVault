using System.Windows.Controls;
using AssetVault.Services;

namespace AssetVault.Pages;

public partial class DashboardPage : Page
{
    private readonly DatabaseService _databaseService;

    public DashboardPage(DatabaseService databaseService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        Refresh();
    }

    public void Refresh()
    {
        var stats = _databaseService.GetDashboardStats();

        TotalAssetsText.Text = stats.TotalAssets.ToString();
        ClothingAssetsText.Text = stats.ClothingAssets.ToString();
        AccessoriesAssetsText.Text = stats.AccessoriesAssets.ToString();
        HairAssetsText.Text = stats.HairAssets.ToString();

        RecentAssetsList.ItemsSource = stats.RecentAssets;
    }
}