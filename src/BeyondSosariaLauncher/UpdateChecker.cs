using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BeyondSosariaLauncher;

public class UpdateChecker
{
    private const string RepoApiUrl = "https://api.github.com/repos/belittledcral/BeyondSosariaTazClient/releases/latest";
    private static readonly string VersionFilePath = Path.Combine(AppContext.BaseDirectory, "v.txt");

    private static readonly string[] SkipPaths =
    {
        "settings.json",
        "launcher-config.json",
        "Data/Profiles/",
        "Data\\Profiles\\"
    };

    public static string GetLocalVersion()
    {
        try
        {
            if (File.Exists(VersionFilePath))
                return File.ReadAllText(VersionFilePath).Trim();
        }
        catch { }
        return "";
    }

    public static async Task<GitHubRelease?> FetchLatestReleaseAsync()
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BeyondSosariaLauncher");
        http.Timeout = TimeSpan.FromSeconds(10);

        var release = await http.GetFromJsonAsync(RepoApiUrl, LauncherJsonContext.Default.GitHubRelease);
        return release;
    }

    public static bool IsNewerVersion(string remoteTag, string localTag)
    {
        // Tags are formatted as vYYYY.MM.DD — compare as strings after stripping 'v'
        var remote = remoteTag.TrimStart('v');
        var local = localTag.TrimStart('v');
        return string.Compare(remote, local, StringComparison.Ordinal) > 0;
    }

    public static async Task DownloadAndApplyUpdate(
        GitHubRelease release,
        IProgress<double> progress,
        CancellationToken cancellationToken = default)
    {
        var zipAsset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No ZIP asset found in release.");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("BeyondSosariaLauncher");

        var tempZip = Path.Combine(Path.GetTempPath(), $"BeyondSosaria_{Guid.NewGuid():N}.zip");
        var tempExtract = Path.Combine(Path.GetTempPath(), $"BeyondSosaria_{Guid.NewGuid():N}");

        try
        {
            // Download with progress
            using (var response = await http.GetAsync(zipAsset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? -1L;

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = File.Create(tempZip);

                var buffer = new byte[81920];
                long downloaded = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloaded += read;
                    if (total > 0)
                        progress.Report((double)downloaded / total);
                }
            }

            progress.Report(1.0);

            // Extract to temp dir
            ZipFile.ExtractToDirectory(tempZip, tempExtract, overwriteFiles: true);

            // Move files into app directory, skipping protected paths
            var appDir = AppContext.BaseDirectory;
            foreach (var file in Directory.EnumerateFiles(tempExtract, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(tempExtract, file);
                if (ShouldSkip(relative))
                    continue;

                var dest = Path.Combine(appDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }

            // Write new version tag
            File.WriteAllText(VersionFilePath, release.TagName);
        }
        finally
        {
            TryDelete(tempZip);
            TryDeleteDir(tempExtract);
        }
    }

    private static bool ShouldSkip(string relativePath)
    {
        foreach (var skip in SkipPaths)
        {
            if (relativePath.Equals(skip, StringComparison.OrdinalIgnoreCase) ||
                relativePath.StartsWith(skip, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDir(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }
}
