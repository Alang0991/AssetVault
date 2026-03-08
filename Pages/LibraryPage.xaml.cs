using AssetVault.Models;
using AssetVault.Services;

using Microsoft.Win32;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace AssetVault.Pages
{
    public partial class LibraryPage : Page
    {
        private readonly DatabaseService _databaseService;

        private List<AssetItem> _allAssets = new();

        public ObservableCollection<AssetRow> AssetRows { get; } = new();

        private AssetItem? _selectedAsset;

        private string _currentCategory = "";
        private string _currentSearch = "";

        private const int CardsPerRow = 4;

        public LibraryPage(DatabaseService databaseService)
        {
            InitializeComponent();

            _databaseService = databaseService;
            DataContext = this;

            _ = LoadAllAssets();
        }

        private async Task LoadAllAssets()
        {
            try
            {
                LoadingTextBlock.Visibility = Visibility.Visible;

                await Task.Run(() =>
                {
                    _databaseService.RebuildLibraryMetadata();

                    var existing = _databaseService.GetAllAssets()
                        .Select(x => Path.GetFileName(x.FilePath))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var unityFolder = Path.Combine(_databaseService.StoragePath, "Unity Packages");

                    if (!Directory.Exists(unityFolder))
                        return;

                    foreach (var file in Directory.EnumerateFiles(unityFolder))
                    {
                        try
                        {
                            var name = Path.GetFileName(file);

                            if (existing.Contains(name))
                                continue;

                            var info = new FileInfo(file);

                            var asset = new AssetItem
                            {
                                Name = Path.GetFileNameWithoutExtension(file),
                                Category = "Unity Packages",
                                FilePath = file,
                                FileType = info.Extension.ToLower(),
                                FileSize = info.Length,
                                DateAdded = DateTime.Now
                            };

                            _databaseService.InsertAsset(asset);
                        }
                        catch
                        {
                        }
                    }
                });

                var assets = await Task.Run(() => _databaseService.GetAllAssets());

                _allAssets = assets;

                await RefreshRows();
            }
            finally
            {
                LoadingTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        public async void LoadAssets(string category = "")
        {
            _currentCategory = category ?? "";
            await LoadAllAssets();
        }

        public void ApplySearch(string search)
        {
            _currentSearch = search ?? "";
            _ = RefreshRows();
        }

        private async Task RefreshRows()
        {
            var rows = await Task.Run(() =>
            {
                IEnumerable<AssetItem> query = _allAssets;

                if (!string.IsNullOrWhiteSpace(_currentCategory))
                {
                    query = query.Where(x =>
                        string.Equals(x.Category, _currentCategory, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(_currentSearch))
                {
                    string search = _currentSearch.Trim();

                    query = query.Where(x =>
                        (x.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (x.Tags?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (x.Creator?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (x.SubType?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (x.Notes?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (x.FileType?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                var filtered = query.ToList();

                var result = new List<AssetRow>();

                for (int i = 0; i < filtered.Count; i += CardsPerRow)
                {
                    var chunk = filtered.Skip(i).Take(CardsPerRow).ToList();

                    result.Add(new AssetRow
                    {
                        First = chunk.ElementAtOrDefault(0),
                        Second = chunk.ElementAtOrDefault(1),
                        Third = chunk.ElementAtOrDefault(2),
                        Fourth = chunk.ElementAtOrDefault(3)
                    });
                }

                return result;
            });

            AssetRows.Clear();

            foreach (var row in rows)
                AssetRows.Add(row);

            EmptyStateTextBlock.Visibility =
                AssetRows.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void AssetCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AssetItem asset)
            {
                _selectedAsset = asset;
                PopulateDetails(asset);
            }
        }

        private void PopulateDetails(AssetItem asset)
        {
            NameTextBox.Text = asset.Name;
            SubTypeTextBox.Text = asset.SubType;
            TagsTextBox.Text = asset.Tags;
            CreatorTextBox.Text = asset.Creator;
            NotesTextBox.Text = asset.Notes;

            PreviewImagePathTextBox.Text = asset.PreviewImagePath;
            FilePathTextBox.Text = asset.FilePath;

            CategoryComboBox.SelectedItem = asset.Category;
            FavoriteCheckBox.IsChecked = asset.IsFavorite;
            RatingComboBox.SelectedIndex = Math.Clamp(asset.Rating, 0, 5);

            SetPreviewImage(asset.PreviewImagePath);
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null)
            {
                MessageBox.Show("Select an asset first.");
                return;
            }

            string newCategory = CategoryComboBox.SelectedItem as string ?? "";
            string oldCategory = _selectedAsset.Category;

            _selectedAsset.Name = NameTextBox.Text.Trim();
            _selectedAsset.SubType = SubTypeTextBox.Text.Trim();
            _selectedAsset.Tags = TagsTextBox.Text.Trim();
            _selectedAsset.Creator = CreatorTextBox.Text.Trim();
            _selectedAsset.Notes = NotesTextBox.Text.Trim();
            _selectedAsset.PreviewImagePath = PreviewImagePathTextBox.Text.Trim();
            _selectedAsset.IsFavorite = FavoriteCheckBox.IsChecked == true;
            _selectedAsset.Rating = RatingComboBox.SelectedIndex;

            try
            {
                if (!string.Equals(oldCategory, newCategory, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(_selectedAsset.FilePath) && File.Exists(_selectedAsset.FilePath))
                    {
                        var fileName = Path.GetFileName(_selectedAsset.FilePath);

                        var newFolder = Path.Combine(_databaseService.StoragePath, newCategory);

                        Directory.CreateDirectory(newFolder);

                        var newPath = Path.Combine(newFolder, fileName);

                        File.Move(_selectedAsset.FilePath, newPath, true);

                        _selectedAsset.FilePath = newPath;
                    }

                    _selectedAsset.Category = newCategory;
                }

                await Task.Run(() => _databaseService.UpdateAsset(_selectedAsset));

                await LoadAllAssets();

                SetPreviewImage(_selectedAsset.PreviewImagePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save asset.\n\n{ex.Message}");
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null)
                return;

            var confirm = MessageBox.Show(
                "Delete this asset?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                await Task.Run(() => _databaseService.DeleteAsset(_selectedAsset.Id));

                ClearDetails();

                await LoadAllAssets();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete asset.\n\n{ex.Message}");
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null)
                return;

            if (string.IsNullOrWhiteSpace(_selectedAsset.FilePath) || !File.Exists(_selectedAsset.FilePath))
            {
                MessageBox.Show("File not found.");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_selectedAsset.FilePath}\"",
                UseShellExecute = true
            });
        }

        private void ClearDetails()
        {
            _selectedAsset = null;

            NameTextBox.Text = "";
            SubTypeTextBox.Text = "";
            TagsTextBox.Text = "";
            CreatorTextBox.Text = "";
            NotesTextBox.Text = "";

            PreviewImagePathTextBox.Text = "";
            FilePathTextBox.Text = "";

            PreviewImage.Source = null;

            CategoryComboBox.SelectedIndex = 0;
            FavoriteCheckBox.IsChecked = false;
            RatingComboBox.SelectedIndex = 0;
        }

        private void SetPreviewImage(string? path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    var image = new BitmapImage();

                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = new Uri(path);
                    image.EndInit();

                    image.Freeze();

                    PreviewImage.Source = image;
                }
                else
                {
                    PreviewImage.Source = null;
                }
            }
            catch
            {
                PreviewImage.Source = null;
            }
        }

        private void ChoosePreviewButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.webp;*.bmp"
            };

            if (dialog.ShowDialog() == true)
            {
                PreviewImagePathTextBox.Text = dialog.FileName;
                SetPreviewImage(dialog.FileName);
            }
        }

        public class AssetRow
        {
            public AssetItem? First { get; set; }
            public AssetItem? Second { get; set; }
            public AssetItem? Third { get; set; }
            public AssetItem? Fourth { get; set; }
        }
    }
}