using AssetVault.Models;
using AssetVault.Services;

using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AssetVault.Pages;

public partial class ImportPage : Page
{
    private readonly DatabaseService _databaseService;
    private readonly Action _onAssetsImported;

    private readonly List<string> _pendingFiles = new();

    private DispatcherTimer? _scanTimer;
    private DateTime _scanStartTime;

    public ImportPage(DatabaseService databaseService, Action onAssetsImported)
    {
        InitializeComponent();

        _databaseService = databaseService;
        _onAssetsImported = onAssetsImported;

        SetupTimer();
    }

    private void SetupTimer()
    {
        _scanTimer = new DispatcherTimer();
        _scanTimer.Interval = TimeSpan.FromSeconds(1);
        _scanTimer.Tick += ScanTimer_Tick;
    }

    private void ScanTimer_Tick(object? sender, EventArgs e)
    {
        var elapsed = DateTime.Now - _scanStartTime;

        ScanTimerText.Text = $"Elapsed: {elapsed:mm\\:ss}";
        FileCountText.Text = $"Files Found: {_pendingFiles.Count}";
    }

    private void RemovePendingFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string file)
        {
            _pendingFiles.Remove(file);
            RefreshPendingList();
        }
    }

    private void ChooseFilesButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
                AddPendingFile(file);

            RefreshPendingList();
        }
    }

    private void ScanFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new CommonOpenFileDialog
        {
            IsFolderPicker = true
        };

        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            StartScan(dialog.FileName);
        }
    }

    private void StartScan(string folder)
    {
        _scanStartTime = DateTime.Now;
        _scanTimer?.Start();

        ScanFolder(folder);

        _scanTimer?.Stop();

        RefreshPendingList();
    }

    private void ScanFolder(string rootFolder)
    {
        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".unitypackage",
            ".fbx",
            ".obj",
            ".prefab",
            ".anim",
            ".shader",
            ".mat",
            ".wav",
            ".mp3",
            ".ogg"
        };

        var existing = _databaseService.GetAllAssets()
            .Select(x => Path.GetFileName(x.FilePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var file in Directory.EnumerateFiles(rootFolder))
            {
                try
                {
                    var ext = Path.GetExtension(file);

                    if (!supportedExtensions.Contains(ext))
                        continue;

                    var name = Path.GetFileName(file);

                    if (existing.Contains(name))
                        continue;

                    AddPendingFile(file);
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;

        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);

        foreach (var file in files)
        {
            if (File.Exists(file))
                AddPendingFile(file);
        }

        RefreshPendingList();
    }

    private void AddPendingFile(string file)
    {
        if (_pendingFiles.Contains(file, StringComparer.OrdinalIgnoreCase))
            return;

        _pendingFiles.Add(file);
    }

    private void RefreshPendingList()
    {
        PendingFilesListBox.ItemsSource = null;
        PendingFilesListBox.ItemsSource = _pendingFiles;

        FileCountText.Text = $"Files Found: {_pendingFiles.Count}";
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _pendingFiles.Clear();
        RefreshPendingList();
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingFiles.Count == 0)
            return;

        ImportProgressBar.Maximum = _pendingFiles.Count;
        ImportProgressBar.Value = 0;

        int index = 0;
        int total = _pendingFiles.Count;

        foreach (var file in _pendingFiles.ToList())
        {
            index++;

            ImportStatusText.Text =
                $"Importing {Path.GetFileName(file)} ({index}/{total})";

            _ = await Task.Run(() => ImportFile(file));

            ImportProgressBar.Value = index;

            await Dispatcher.InvokeAsync(() => { });
        }

        _pendingFiles.Clear();
        RefreshPendingList();

        ImportStatusText.Text = "✔ Import Complete";

        _onAssetsImported?.Invoke();
    }

    private bool ImportFile(string file)
    {
        try
        {
            if (!File.Exists(file))
                return false;

            var extension = Path.GetExtension(file).ToLower();
            var fileName = Path.GetFileName(file);

            string category = DetectCategory(extension, fileName);

            var categoryFolder = Path.Combine(_databaseService.StoragePath, category);

            Directory.CreateDirectory(categoryFolder);

            var destination = Path.Combine(categoryFolder, fileName);

            if (File.Exists(destination))
                return false;

            File.Copy(file, destination, true);

            var info = new FileInfo(destination);

            var asset = new AssetItem
            {
                Name = Path.GetFileNameWithoutExtension(file),
                Category = category,
                FilePath = destination,
                FileType = extension,
                FileSize = info.Length,
                DateAdded = DateTime.Now
            };

            _databaseService.InsertAsset(asset);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private string DetectCategory(string extension, string fileName)
    {
        if (fileName.Contains("nsfw", StringComparison.OrdinalIgnoreCase))
            return "NSFW";

        if (extension == ".unitypackage")
            return "Unity Packages";

        if (extension == ".fbx" || extension == ".obj")
            return "Props";

        if (extension == ".wav" || extension == ".mp3" || extension == ".ogg")
            return "Audio";

        if (extension == ".anim")
            return "Animations";

        if (extension == ".shader")
            return "Shaders";

        return "Other";
    }
}