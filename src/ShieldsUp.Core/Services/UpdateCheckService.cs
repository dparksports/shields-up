using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ShieldsUp.Core.Services;

/// <summary>
/// GitHub-release update check for Shields Up. Talks to
/// api.github.com/repos/dparksports/shields-up/releases/latest, compares the
/// release tag against the running version, and can download the release zip
/// to the user's Downloads folder, verifying its SHA256 against the release's
/// SHA256SUMS.txt before declaring it valid.
/// </summary>
public partial class UpdateCheckService
{
    public const string Repo = "dparksports/shields-up";
    public const string ReleasePage = $"https://github.com/{Repo}/releases/latest";

    [GeneratedRegex(@"^v?(\d+\.\d+(\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex TagVersionRegex();

    public sealed class UpdateInfo
    {
        public string LatestTag { get; init; } = "";
        public Version? LatestVersion { get; init; }
        public string ReleaseUrl { get; init; } = "";
        public string Notes { get; init; } = "";
        public string? ZipUrl { get; init; }
        public string? ZipName { get; init; }
        public string? ChecksumsUrl { get; init; }
        public bool UpdateAvailable { get; init; }
        public bool ParseFailed { get; init; }
        public string Error { get; init; } = "";
    }

    public sealed class DownloadResult
    {
        public bool Success { get; init; }
        public string ZipPath { get; init; } = "";
        public string Message { get; init; } = "";
        public string? Sha256 { get; init; }
    }

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    /// <summary>Version string for display, without any build metadata.</summary>
    public static string CleanVersion(Version? v) => v == null ? "unknown" : $"v{v.Major}.{v.Minor}.{v.Build}";

    public static Version? GetCurrentVersion() => typeof(UpdateCheckService).Assembly.GetName().Version;

    public async Task<UpdateInfo> CheckAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{Repo}/releases/latest");
            request.Headers.UserAgent.ParseAdd($"ShieldsUp/{CleanVersion(GetCurrentVersion())} (update-check)");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
                return new UpdateInfo { Error = $"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}." };

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;

            string tag = root.TryGetProperty("tag_name", out var tagEl) ? tagEl.GetString() ?? "" : "";
            string url = root.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() ?? "" : ReleasePage;
            string notes = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";

            string? zipUrl = null, zipName = null, sumsUrl = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    string dl = asset.TryGetProperty("browser_download_url", out var b) ? b.GetString() ?? "" : "";
                    if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                        name.StartsWith("ShieldsUp", StringComparison.OrdinalIgnoreCase))
                    {
                        zipName = name; zipUrl = dl;
                    }
                    else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        sumsUrl = dl;
                    }
                }
            }

            var match = TagVersionRegex().Match(tag);
            if (!match.Success)
                return new UpdateInfo { LatestTag = tag, ReleaseUrl = url, Notes = notes, ParseFailed = true, ZipUrl = zipUrl, ZipName = zipName, ChecksumsUrl = sumsUrl };

            var latest = Version.Parse(match.Groups[1].Value);
            var current = GetCurrentVersion();
            bool available = current == null || latest > new Version(current.Major, current.Minor, Math.Max(current.Build, 0));

            return new UpdateInfo
            {
                LatestTag = CleanVersion(latest),
                LatestVersion = latest,
                ReleaseUrl = url,
                Notes = notes,
                UpdateAvailable = available,
                ZipUrl = zipUrl,
                ZipName = zipName,
                ChecksumsUrl = sumsUrl
            };
        }
        catch (Exception ex)
        {
            AppLogger.LogException("update check", ex);
            return new UpdateInfo { Error = ex.Message };
        }
    }

    /// <summary>
    /// Downloads the release zip into the user's Downloads folder, then
    /// downloads SHA256SUMS.txt and verifies the archive against it.
    /// </summary>
    public async Task<DownloadResult> DownloadAndVerifyAsync(UpdateInfo info, IProgress<double>? percent = null, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(info.ZipUrl) || string.IsNullOrEmpty(info.ZipName))
            return new DownloadResult { Success = false, Message = "The latest release has no zip asset to download." };

        try
        {
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(downloads);
            string target = Path.Combine(downloads, info.ZipName);
            int attempt = 1;
            while (File.Exists(target))
            {
                if (attempt == 1) target = Path.Combine(downloads, Path.GetFileNameWithoutExtension(info.ZipName) + " (1).zip");
                else target = Path.Combine(downloads, Path.GetFileNameWithoutExtension(info.ZipName) + $" ({attempt}).zip");
                attempt++;
            }

            percent?.Report(0);
            using (var request = new HttpRequestMessage(HttpMethod.Get, info.ZipUrl))
            {
                request.Headers.UserAgent.ParseAdd("ShieldsUp updater");
                using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? -1;
                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var file = File.Create(target);

                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await source.ReadAsync(buffer, ct)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, n), ct);
                    read += n;
                    if (total > 0) percent?.Report(Math.Min(100.0, 100.0 * read / total));
                }
            }
            percent?.Report(100);

            string? expected = await FetchExpectedChecksumAsync(info.ChecksumsUrl, info.ZipName, ct);
            string actual = await ComputeSha256Async(target, ct);

            if (expected != null && !string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(target);
                AppLogger.Log("update", $"downloaded zip FAILED SHA256 verification and was deleted (expected {expected}, got {actual})");
                return new DownloadResult { Success = false, Message = "SHA256 mismatch — the downloaded file does not match the release checksum and was deleted.", Sha256 = actual };
            }

            AppLogger.Log("update", $"downloaded {Path.GetFileName(target)} verified={expected != null} sha256={actual}");
            return new DownloadResult
            {
                Success = true,
                ZipPath = target,
                Sha256 = actual,
                Message = expected != null
                    ? "Downloaded and verified against the release SHA256SUMS."
                    : "Downloaded (the release carried no SHA256SUMS.txt to verify against)."
            };
        }
        catch (OperationCanceledException)
        {
            return new DownloadResult { Success = false, Message = "Download cancelled." };
        }
        catch (Exception ex)
        {
            AppLogger.LogException("update download", ex);
            return new DownloadResult { Success = false, Message = $"Download failed: {ex.Message}" };
        }
    }

    private static async Task<string?> FetchExpectedChecksumAsync(string? sumsUrl, string zipName, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sumsUrl)) return null;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, sumsUrl);
            request.Headers.UserAgent.ParseAdd("ShieldsUp updater");
            using var response = await Http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;
            var text = await response.Content.ReadAsStringAsync(ct);
            foreach (var line in text.Split('\n'))
            {
                // "<hash>  <name>" or "<hash> <name>"
                var parts = line.Trim().Split(' ', (char)StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2 && parts[1].Equals(zipName, StringComparison.OrdinalIgnoreCase))
                    return parts[0].Trim();
            }
        }
        catch { }
        return null;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash);
    }
}
