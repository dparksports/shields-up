#nullable enable
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

    /// <summary>
    /// Integrity report for the running installation: version vs the latest
    /// release, the original zip's SHA256 vs the release checksum, and every
    /// release file present in the install folder with the expected size.
    /// </summary>
    public sealed class InstallVerifyReport
    {
        public string RunningVersion { get; init; } = "";
        public string? LatestTag { get; init; }
        public bool VersionMatchesLatest { get; init; }
        public bool UpdateAvailable { get; init; }
        public bool Online { get; init; } = true;        // false = GitHub unreachable, local checks only
        public string NetworkNote { get; init; } = "";   // why we went local-only, if we did
        public string? ZipPath { get; init; }            // null = zip not found (UI may offer a picker)
        public bool? ZipHashMatches { get; init; }       // null = no checksum published to compare
        public string? ZipSha256 { get; init; }
        public int FilesExpected { get; init; }
        public int FilesMatching { get; init; }
        public List<string> Problems { get; init; } = new(); // missing / size-mismatch, capped
        public string Error { get; init; } = "";

        public bool FilesOk => FilesExpected > 0 && FilesMatching == FilesExpected;
        public bool OverallOk => string.IsNullOrEmpty(Error)
                                 && Online
                                 && VersionMatchesLatest
                                 && ZipHashMatches != false
                                 && FilesOk;
    }

    /// <summary>
    /// Verifies the running installation against the latest GitHub release.
    /// Finds the release zip in Downloads (or accepts an explicit path), hashes
    /// it against the release's SHA256SUMS.txt, and compares every entry in
    /// the zip (name + size) against the installed folder. Network calls are
    /// capped at 10s each and report progress through <paramref name="phase"/>;
    /// hashing and the archive walk run on the worker pool so the caller's UI
    /// thread stays free; if GitHub is unreachable, the local checks (zip hash
    /// + file comparison) still run against whatever release zip is on disk.
    /// </summary>
    public async Task<InstallVerifyReport> VerifyInstallAsync(string? zipOverride = null, IProgress<string>? phase = null, CancellationToken ct = default)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        long stepMs = clock.ElapsedMilliseconds;
        AppLogger.LogVerify($"run started — app v{CleanVersion(GetCurrentVersion())}, exe={Environment.ProcessPath}, " +
                            $"zipOverride={(zipOverride ?? "none")}");

        phase?.Report("Contacting GitHub…");
        AppLogger.LogVerify("step 1/5: GitHub metadata check…");
        var info = await CheckAsync(ct);
        AppLogger.LogVerify($"step 1/5 done in {clock.ElapsedMilliseconds - stepMs}ms — " +
                            (string.IsNullOrEmpty(info.Error)
                                ? $"online, tag={info.LatestTag}, zip={info.ZipName ?? "none"}, checksums={(info.ChecksumsUrl != null)}"
                                : $"OFFLINE: {info.Error}"));
        string running = CleanVersion(GetCurrentVersion());
        bool online = string.IsNullOrEmpty(info.Error);

        phase?.Report("Looking for the release zip in Downloads…");
        AppLogger.LogVerify("step 2/5: Downloads lookup…");
        string? zip = zipOverride;
        if (string.IsNullOrEmpty(zip) && online && !string.IsNullOrEmpty(info.ZipName))
            zip = FindZipInDownloads(info.ZipName);
        // Offline: fall back to the newest release zip on disk so local checks still run
        if (string.IsNullOrEmpty(zip) && !online)
            zip = FindZipInDownloads(null);
        AppLogger.LogVerify($"step 2/5 done — {(zip ?? "no usable zip found")}");
        if (string.IsNullOrEmpty(zip) && online)
        {
            AppLogger.LogVerify($"run stopped after {clock.ElapsedMilliseconds}ms — no zip; caller offers the file dialog");
            // ZipPath stays null — caller offers a file picker
            return new InstallVerifyReport { RunningVersion = running, Online = true };
        }
        if (string.IsNullOrEmpty(zip))
        {
            AppLogger.LogVerify($"run stopped after {clock.ElapsedMilliseconds}ms — offline and no zip");
            return new InstallVerifyReport
            {
                RunningVersion = running,
                Online = false,
                NetworkNote = info.Error,
                Error = "GitHub is unreachable and no ShieldsUp release zip was found in Downloads. " +
                        "Try again when online, or pick the zip manually."
            };
        }
        if (!File.Exists(zip))
        {
            AppLogger.LogVerify($"run stopped after {clock.ElapsedMilliseconds}ms — zip vanished: {zip}");
            return new InstallVerifyReport { RunningVersion = running, Error = "Selected zip no longer exists." };
        }

        string zipPath = zip;
        try
        {
            phase?.Report($"Hashing {Path.GetFileName(zipPath)}…");
            AppLogger.LogVerify($"step 3/5: hashing {Path.GetFileName(zipPath)} ({new FileInfo(zipPath).Length / 1_000_000.0:F0} MB)…");
            stepMs = clock.ElapsedMilliseconds;
            string actual = await Task.Run(() => ComputeSha256Async(zipPath, ct), ct);
            AppLogger.LogVerify($"step 3/5 done in {clock.ElapsedMilliseconds - stepMs}ms — sha256={actual}");

            string? expected = null;
            if (online && !string.IsNullOrEmpty(info.ChecksumsUrl))
            {
                phase?.Report("Fetching the release checksum…");
                AppLogger.LogVerify("step 4/5: fetching SHA256SUMS.txt…");
                stepMs = clock.ElapsedMilliseconds;
                expected = await FetchExpectedChecksumAsync(info.ChecksumsUrl, info.ZipName, ct);
                AppLogger.LogVerify($"step 4/5 done in {clock.ElapsedMilliseconds - stepMs}ms — " +
                                    (expected != null ? $"expected={expected}" : "no checksum for this zip in SHA256SUMS.txt"));
            }
            else
            {
                AppLogger.LogVerify("step 4/5 skipped — offline or no SHA256SUMS.txt on the release");
            }

            phase?.Report("Comparing install files…");
            AppLogger.LogVerify("step 5/5: comparing zip entries against the install folder…");
            stepMs = clock.ElapsedMilliseconds;
            var problems = new List<string>();
            int expected2 = 0, matching = 0;
            try
            {
                await Task.Run(() =>
                {
                    string baseDir = AppContext.BaseDirectory;
                    using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
                    foreach (var entry in archive.Entries)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (string.IsNullOrEmpty(entry.Name)) continue; // directory stub
                        expected2++;
                        string local = Path.Combine(baseDir, entry.FullName);
                        if (!File.Exists(local))
                        {
                            problems.Add($"missing: {entry.FullName}");
                        }
                        else if (new FileInfo(local).Length != entry.Length)
                        {
                            problems.Add($"altered size: {entry.FullName}");
                        }
                        else
                        {
                            matching++;
                        }
                        if (problems.Count >= 10) { /* cap the list; keep counting */ }
                    }
                }, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                AppLogger.LogVerify($"step 5/5 FAILED after {clock.ElapsedMilliseconds - stepMs}ms — could not read zip contents: {ex.Message}");
                return new InstallVerifyReport
                {
                    RunningVersion = running, LatestTag = online ? info.LatestTag : null,
                    Online = online, NetworkNote = online ? "" : info.Error,
                    ZipPath = zipPath, ZipSha256 = actual,
                    Error = $"Could not read the zip contents: {ex.Message}"
                };
            }
            AppLogger.LogVerify($"step 5/5 done in {clock.ElapsedMilliseconds - stepMs}ms — {matching}/{expected2} files match" +
                                (problems.Count > 0 ? $"; problems: {string.Join("; ", problems.Take(10))}" : ""));

            AppLogger.Log("verify", $"install check ({(online ? "online" : "OFFLINE, local-only")}): " +
                                    $"version {running} vs {info.LatestTag ?? "?"}, " +
                                    $"zip {(expected != null && string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase) ? "hash OK" : "hash unknown/mismatch")}, " +
                                    $"{matching}/{expected2} files match");

            var report = new InstallVerifyReport
            {
                RunningVersion = running,
                LatestTag = online ? (info.ParseFailed ? info.LatestTag : info.LatestVersion?.ToString()) : null,
                VersionMatchesLatest = online && !info.ParseFailed && !info.UpdateAvailable,
                UpdateAvailable = info.UpdateAvailable,
                Online = online,
                NetworkNote = online ? "" : info.Error,
                ZipPath = zipPath,
                ZipSha256 = actual,
                ZipHashMatches = expected == null ? null
                    : string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase),
                FilesExpected = expected2,
                FilesMatching = matching,
                Problems = problems.Take(10).ToList()
            };
            AppLogger.LogVerify($"run finished in {clock.ElapsedMilliseconds}ms — zipHash={(report.ZipHashMatches == null ? "unknown" : report.ZipHashMatches == true ? "MATCH" : "MISMATCH")}, " +
                                $"filesOk={report.FilesOk}, overallOk={report.OverallOk}");
            return report;
        }
        catch (OperationCanceledException)
        {
            AppLogger.LogVerify($"run CANCELLED after {clock.ElapsedMilliseconds}ms");
            return new InstallVerifyReport
            {
                RunningVersion = running, Online = online,
                NetworkNote = online ? "" : info.Error, Error = "Verification cancelled."
            };
        }
    }

    /// <summary>
    /// Locates a release zip in Downloads: the exact release filename first,
    /// then the browser's "name (1).zip" duplicate of that same release (an
    /// exact-name check misses those); with no name to match (offline), the
    /// newest ShieldsUp zip on disk. Online, a different release's zip is
    /// never substituted — it would hash-mismatch the published checksum.
    /// </summary>
    private static string? FindZipInDownloads(string? exactName)
    {
        try
        {
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            if (!Directory.Exists(downloads)) return null;

            var newestFirst = new DirectoryInfo(downloads)
                .EnumerateFiles("ShieldsUp*.zip")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            if (string.IsNullOrEmpty(exactName))
                return newestFirst.FirstOrDefault()?.FullName;

            string? exact = newestFirst
                .FirstOrDefault(f => string.Equals(f.Name, exactName, StringComparison.OrdinalIgnoreCase))
                ?.FullName;
            if (exact != null)
            {
                AppLogger.LogVerify($"lookup: exact release name → {exact}");
                return exact;
            }

            string stem = Path.GetFileNameWithoutExtension(exactName);
            string? duplicate = newestFirst
                .FirstOrDefault(f => f.Name.StartsWith(stem, StringComparison.OrdinalIgnoreCase))
                ?.FullName;
            if (duplicate != null)
            {
                AppLogger.LogVerify($"lookup: no exact name, duplicate of the same release → {duplicate}");
                return duplicate;
            }
            AppLogger.LogVerify($"lookup: nothing in Downloads matches {exactName} (stem {stem})");
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Version string for display, without any build metadata.</summary>
    public static string CleanVersion(Version? v) => v == null ? "unknown" : $"v{v.Major}.{v.Minor}.{v.Build}";

    public static Version? GetCurrentVersion() => typeof(UpdateCheckService).Assembly.GetName().Version;

    public async Task<UpdateInfo> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            // Metadata calls fail fast: a stalled route to GitHub must not hang
            // the caller for the full 30s HttpClient timeout.
            using var capped = CancellationTokenSource.CreateLinkedTokenSource(ct);
            capped.CancelAfter(TimeSpan.FromSeconds(10));

            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/repos/{Repo}/releases/latest");
            request.Headers.UserAgent.ParseAdd($"ShieldsUp/{CleanVersion(GetCurrentVersion())} (update-check)");

            using var response = await Http.SendAsync(request, capped.Token);
            if (!response.IsSuccessStatusCode)
                return new UpdateInfo { Error = $"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}." };

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(capped.Token));
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
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            AppLogger.Log("update check", "GitHub metadata call timed out after 10s (stalled route?)");
            return new UpdateInfo { Error = "GitHub did not respond within 10 seconds — the network route may be stalled. Try again in a moment." };
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

    private static async Task<string?> FetchExpectedChecksumAsync(string? sumsUrl, string? zipName, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(sumsUrl) || string.IsNullOrEmpty(zipName)) return null;
        try
        {
            using var capped = CancellationTokenSource.CreateLinkedTokenSource(ct);
            capped.CancelAfter(TimeSpan.FromSeconds(10));

            using var request = new HttpRequestMessage(HttpMethod.Get, sumsUrl);
            request.Headers.UserAgent.ParseAdd("ShieldsUp updater");
            using var response = await Http.SendAsync(request, capped.Token);
            if (!response.IsSuccessStatusCode) return null;
            var text = await response.Content.ReadAsStringAsync(capped.Token);
            foreach (var line in text.Split('\n'))
            {
                // "<hash>  <name>" or "<hash> <name>"
                var parts = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
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
