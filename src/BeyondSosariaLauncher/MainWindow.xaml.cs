using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace BeyondSosariaLauncher;

public class PluginItem
{
    public string Name { get; set; } = "";
    public bool IsEnabled { get; set; }
}

public partial class MainWindow : Window
{
    private LauncherConfig _config = null!;
    private GitHubRelease? _latestRelease;
    private readonly ObservableCollection<PluginItem> _plugins = new();
    private CancellationTokenSource? _downloadCts;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _config = LauncherConfig.Load();

        LoadServers();
        LoadCredentials();
        LoadPlugins();
        UpdatePlayButton();

        await CheckForUpdateAsync();
    }

    // ── Servers ──────────────────────────────────────────────────────────────

    private void LoadServers()
    {
        ServerCombo.ItemsSource = _config.Servers;
        var idx = Math.Clamp(_config.LastServerIndex, 0, _config.Servers.Count - 1);
        ServerCombo.SelectedIndex = idx;
    }

    private void ServerCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _config.LastServerIndex = ServerCombo.SelectedIndex;
    }

    private void AddServerBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddServerDialog { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            _config.Servers.Add(dlg.Result);
            ServerCombo.ItemsSource = null;
            ServerCombo.ItemsSource = _config.Servers;
            ServerCombo.SelectedIndex = _config.Servers.Count - 1;
        }
    }

    // ── UO Path ──────────────────────────────────────────────────────────────

    private void BrowseUoPath_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Select your Ultima Online data folder",
            Multiselect = false
        };

        if (!string.IsNullOrEmpty(UoPathBox.Text) && Directory.Exists(UoPathBox.Text))
            dlg.InitialDirectory = UoPathBox.Text;

        if (dlg.ShowDialog(this) == true)
        {
            UoPathBox.Text = dlg.FolderName;
            _config.UoDataPath = dlg.FolderName;
        }
    }

    private void UoPathBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _config.UoDataPath = UoPathBox.Text;
        UpdatePlayButton();
    }

    private bool IsValidUoPath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        File.Exists(Path.Combine(path, "tiledata.mul"));

    // ── Credentials ──────────────────────────────────────────────────────────

    private void LoadCredentials()
    {
        UoPathBox.Text = _config.UoDataPath;
        UsernameBox.Text = _config.LastUsername;
        RememberPasswordCheck.IsChecked = _config.RememberPassword;

        if (_config.RememberPassword && !string.IsNullOrEmpty(_config.SavedPassword))
            PasswordBox.Password = DecodePassword(_config.SavedPassword);
    }

    private static string EncodePassword(string pw) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pw));

    private static string DecodePassword(string encoded)
    {
        try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
        catch { return encoded; }
    }

    // ── Plugins ──────────────────────────────────────────────────────────────

    private void LoadPlugins()
    {
        _plugins.Clear();

        var pluginDir = Path.Combine(AppContext.BaseDirectory, "Data", "Plugins");
        if (Directory.Exists(pluginDir))
        {
            foreach (var dll in Directory.EnumerateFiles(pluginDir, "*.dll").OrderBy(f => f))
            {
                var name = Path.GetFileName(dll);
                _plugins.Add(new PluginItem
                {
                    Name = name,
                    IsEnabled = _config.EnabledPlugins.Contains(name)
                });
            }
        }

        PluginsList.ItemsSource = _plugins;
    }

    // ── Update Check ─────────────────────────────────────────────────────────

    private async Task CheckForUpdateAsync()
    {
        var local = UpdateChecker.GetLocalVersion();
        VersionText.Text = string.IsNullOrEmpty(local) ? "Version unknown" : local;

        try
        {
            _latestRelease = await UpdateChecker.FetchLatestReleaseAsync();
            if (_latestRelease != null && UpdateChecker.IsNewerVersion(_latestRelease.TagName, local))
            {
                UpdateBannerText.Text = $"Update available: {_latestRelease.TagName}";
                UpdateBanner.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            // Offline or network failure — silently continue
        }
    }

    private async void DownloadUpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_latestRelease == null) return;

        DownloadUpdateBtn.IsEnabled = false;
        DownloadPanel.Visibility = Visibility.Visible;
        StatusText.Visibility = Visibility.Collapsed;

        _downloadCts = new CancellationTokenSource();

        var progress = new Progress<double>(p =>
        {
            DownloadProgress.Value = p * 100;
            DownloadPct.Text = $"{(int)(p * 100)}%";
        });

        try
        {
            await UpdateChecker.DownloadAndApplyUpdate(_latestRelease, progress, _downloadCts.Token);

            UpdateBanner.Visibility = Visibility.Collapsed;
            DownloadPanel.Visibility = Visibility.Collapsed;
            ShowStatus("Update complete — restart the launcher to apply.");

            var local = UpdateChecker.GetLocalVersion();
            VersionText.Text = string.IsNullOrEmpty(local) ? "Version unknown" : local;
        }
        catch (OperationCanceledException)
        {
            ShowStatus("Download cancelled.");
            DownloadPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ShowStatus($"Update failed: {ex.Message}");
            DownloadPanel.Visibility = Visibility.Collapsed;
            DownloadUpdateBtn.IsEnabled = true;
        }
    }

    // ── Play ─────────────────────────────────────────────────────────────────

    private void UpdatePlayButton()
    {
        PlayBtn.IsEnabled = IsValidUoPath(UoPathBox.Text);
    }

    private void PlayBtn_Click(object sender, RoutedEventArgs e)
    {
        var server = ServerCombo.SelectedItem as ServerEntry;
        if (server == null)
        {
            MessageBox.Show("Please select a server.", "Beyond Sosaria Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!IsValidUoPath(UoPathBox.Text))
        {
            MessageBox.Show("Please select a valid UO data folder (must contain tiledata.mul).",
                "Beyond Sosaria Launcher", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;
        var rememberPassword = RememberPasswordCheck.IsChecked == true;

        // Save config
        _config.LastUsername = username;
        _config.RememberPassword = rememberPassword;
        _config.SavedPassword = rememberPassword ? EncodePassword(password) : "";
        _config.UoDataPath = UoPathBox.Text;

        // Sync enabled plugins from checkboxes
        _config.EnabledPlugins = _plugins
            .Where(p => p.IsEnabled)
            .Select(p => p.Name)
            .ToList();

        _config.Save();

        // Build plugin paths
        var enabledPluginPaths = _plugins
            .Where(p => p.IsEnabled)
            .Select(p => Path.Combine("Data", "Plugins", p.Name))
            .ToList();

        // Build args
        var args = new List<string>
        {
            "-uopath",    QuoteArg(UoPathBox.Text),
            "-ip",        server.Host,
            "-port",      server.Port.ToString(),
            "-username",  QuoteArg(username),
            "-password",  QuoteArg(password),
            "-autologin", "true",
            "-saveaccount", rememberPassword ? "true" : "false"
        };

        if (enabledPluginPaths.Count > 0)
        {
            args.Add("-plugins");
            args.Add(string.Join(",", enabledPluginPaths));
        }

        var tazuoExe = Path.Combine(AppContext.BaseDirectory, "TazUO.exe");
        if (!File.Exists(tazuoExe))
        {
            MessageBox.Show("TazUO.exe not found in the launcher directory.",
                "Beyond Sosaria Launcher", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var psi = new ProcessStartInfo(tazuoExe, string.Join(" ", args))
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory
        };

        Process.Start(psi);
        Application.Current.Shutdown();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string QuoteArg(string value) =>
        value.Contains(' ') ? $"\"{value}\"" : value;

    private void ShowStatus(string msg)
    {
        StatusText.Text = msg;
        StatusText.Visibility = Visibility.Visible;
    }
}
