using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShieldsUp.Core;
using ShieldsUp.Core.Helpers;
using Microsoft.UI.Xaml.Media;

namespace ShieldsUp.Pages;

public sealed partial class DbxSafetyPage : Page
{
    private string _efiDriveLetter = "Z:";
    private string _downloadedDbxPath;
    private readonly StringBuilder _log = new();

    public DbxSafetyPage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        Log("DBX Safety Check ready. Click 'Run Full Check' to begin.");
    }

    private void Log(string message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _log.AppendLine(message);
            LogOutput.Text = _log.ToString();
        });
    }

    // ═══════════════════════════════════════
    //  FULL CHECK PIPELINE
    // ═══════════════════════════════════════
    private async void RunCheckBtn_Click(object sender, RoutedEventArgs e)
    {
        RunCheckBtn.IsEnabled = false;
        _log.Clear();
        LogOutput.Text = "";
        Log("Starting full DBX safety check...\n");

        try
        {
            // Step 1: Mount EFI
            bool mounted = await MountEfiPartition();
            if (!mounted)
            {
                SetStatus(EfiStatusText, "✗ Failed to mount EFI partition", false);
                return;
            }
            SetStatus(EfiStatusText, $"✓ EFI partition mounted at {_efiDriveLetter}", true);

            // Step 2: Check baseline
            await CheckEfiBaseline();

            // Step 3: Sigcheck (if available)
            await AnalyzeBootloader();

            // Step 4: DBX revocation check
            await CheckDbxSafety();
        }
        catch (Exception ex)
        {
            Log($"\n[ERROR] {ex.Message}");
        }
        finally
        {
            // Unmount EFI
            await UnmountEfiPartition();
            RunCheckBtn.IsEnabled = true;
        }
    }

    // ═══════════════════════════════════════
    //  EFI PARTITION MOUNT
    // ═══════════════════════════════════════
    private async Task<bool> MountEfiPartition()
    {
        Log("[1/4] Mounting EFI partition...");

        // Try to unmount first (in case it's stale)
        await ProcessRunner.RunAsync("mountvol", $"{_efiDriveLetter} /D");
        await Task.Delay(500);

        var result = await ProcessRunner.RunWithDetailsAsync("mountvol", $"{_efiDriveLetter} /S");

        if (result.ExitCode != 0)
        {
            Log($"  mountvol failed: {result.Error}");
            // Try alternate drive letter
            _efiDriveLetter = "Y:";
            await ProcessRunner.RunAsync("mountvol", $"{_efiDriveLetter} /D");
            await Task.Delay(500);
            result = await ProcessRunner.RunWithDetailsAsync("mountvol", $"{_efiDriveLetter} /S");
            if (result.ExitCode != 0)
            {
                Log($"  Alternate mount also failed: {result.Error}");
                return false;
            }
        }

        Log($"  Mounted EFI System Partition at {_efiDriveLetter}");
        return true;
    }

    private async Task UnmountEfiPartition()
    {
        await ProcessRunner.RunAsync("mountvol", $"{_efiDriveLetter} /D");
        Log("\nEFI partition unmounted.");
    }

    // ═══════════════════════════════════════
    //  INTEGRITY BASELINE
    // ═══════════════════════════════════════
    private async Task CheckEfiBaseline()
    {
        Log("\n[2/4] Checking EFI integrity baseline...");
        string baselinePath = AppPaths.EfiBaselinePath;

        string efiBootDir = Path.Combine(_efiDriveLetter + "\\", "EFI", "Microsoft", "Boot");
        if (!Directory.Exists(efiBootDir))
        {
            efiBootDir = Path.Combine(_efiDriveLetter + "\\", "EFI", "Boot");
        }

        if (!Directory.Exists(efiBootDir))
        {
            SetStatus(BaselineStatusText, "✗ EFI boot directory not found", false);
            Log("  Boot directory not found.");
            return;
        }

        // Compute hashes of critical files
        var currentHashes = new Dictionary<string, string>();
        await Task.Run(() =>
        {
            try
            {
                var files = Directory.GetFiles(efiBootDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => f.EndsWith(".efi", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));

                using var sha = SHA256.Create();
                foreach (var file in files)
                {
                    try
                    {
                        var bytes = File.ReadAllBytes(file);
                        var hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
                        string relPath = file.Substring(_efiDriveLetter.Length);
                        currentHashes[relPath] = hash;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Log($"  Error scanning: {ex.Message}");
            }
        });

        Log($"  Scanned {currentHashes.Count} EFI files.");

        // Load or create baseline
        if (File.Exists(baselinePath))
        {
            try
            {
                string json = File.ReadAllText(baselinePath);
                var baseline = JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                int changed = 0, added = 0, removed = 0;
                foreach (var kvp in currentHashes)
                {
                    if (!baseline.ContainsKey(kvp.Key)) { added++; Log($"  [NEW] {kvp.Key}"); }
                    else if (baseline[kvp.Key] != kvp.Value) { changed++; Log($"  [CHANGED] {kvp.Key}"); }
                }
                foreach (var kvp in baseline)
                {
                    if (!currentHashes.ContainsKey(kvp.Key)) { removed++; Log($"  [REMOVED] {kvp.Key}"); }
                }

                if (changed == 0 && added == 0 && removed == 0)
                {
                    SetStatus(BaselineStatusText, $"✓ All {currentHashes.Count} files match baseline — no tampering detected", true);
                }
                else
                {
                    SetStatus(BaselineStatusText, $"⚠ Drift detected: {changed} changed, {added} new, {removed} removed", false);
                }
            }
            catch
            {
                SetStatus(BaselineStatusText, "✗ Failed to read baseline", false);
            }
        }
        else
        {
            // Create baseline
            try
            {
                string dir = Path.GetDirectoryName(baselinePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(baselinePath, JsonSerializer.Serialize(currentHashes, new JsonSerializerOptions { WriteIndented = true }));
                SetStatus(BaselineStatusText, $"✓ Baseline created with {currentHashes.Count} files. Re-run to verify.", true);
                Log("  New baseline saved.");
            }
            catch (Exception ex)
            {
                SetStatus(BaselineStatusText, $"✗ Failed to save baseline: {ex.Message}", false);
            }
        }
    }

    // ═══════════════════════════════════════
    //  SIGCHECK ANALYSIS
    // ═══════════════════════════════════════
    private async Task AnalyzeBootloader()
    {
        Log("\n[3/4] Analyzing bootloader signature...");

        // Look for sigcheck64.exe in common locations or download it
        string sigcheckPath = await FindSigcheckAsync();
        if (sigcheckPath == null)
        {
            SetStatus(SigcheckStatusText, "⚠ sigcheck64.exe not found — skipping Authenticode verification", false);
            Log("  sigcheck64.exe not found. Place it in the ShieldsUp directory or system PATH.");
            return;
        }

        string bootmgfwPath = Path.Combine(_efiDriveLetter + "\\", "EFI", "Microsoft", "Boot", "bootmgfw.efi");
        if (!File.Exists(bootmgfwPath))
        {
            bootmgfwPath = Path.Combine(_efiDriveLetter + "\\", "EFI", "Boot", "bootx64.efi");
        }

        if (!File.Exists(bootmgfwPath))
        {
            SetStatus(SigcheckStatusText, "✗ Boot manager EFI file not found", false);
            return;
        }

        // Sysinternals tools emit UTF-16LE on redirected stdout — decode accordingly,
        // otherwise the verdict text arrives as interleaved-null mojibake and every
        // check reads as "could not be verified".
        string output = await ProcessRunner.RunAsync(sigcheckPath,
            $"-accepteula -nobanner -h \"{bootmgfwPath}\"", Encoding.Unicode);
        Log($"  Sigcheck output:\n{output}");

        // Parse field-by-field instead of blanket substring matching
        string verified = GetFieldValue(output, "Verified");
        string publisher = GetFieldValue(output, "Publisher");

        if (string.Equals(verified, "Signed", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(SigcheckStatusText,
                "✓ Bootloader signature is valid (Authenticode verified)"
                + (string.IsNullOrEmpty(publisher) ? "" : $" — {publisher}"),
                true);
        }
        else if (string.Equals(verified, "Unsigned", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(SigcheckStatusText, "✗ Bootloader is NOT signed — possible tampering", false);
        }
        else
        {
            SetStatus(SigcheckStatusText, "⚠ Bootloader signature could not be verified (sigcheck produced no parseable verdict)", false);
        }

        // Extract PE hash for DBX comparison (-h makes sigcheck print these)
        string peHash = ExtractHash(output, "PE256");
        string sha256Hash = ExtractHash(output, "SHA256");
        if (!string.IsNullOrEmpty(peHash))
            Log($"  PE256 Hash: {peHash}");
        if (!string.IsNullOrEmpty(sha256Hash))
            Log($"  SHA256 Hash: {sha256Hash}");
    }

    private async Task<string> FindSigcheckAsync()
    {
        // Existing copies are only used if they pass Microsoft Authenticode verification
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "sigcheck64.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sysinternals", "sigcheck64.exe")
        };

        foreach (var p in candidates)
        {
            if (!File.Exists(p)) continue;
            if (AuthenticodeVerifier.IsMicrosoftSigned(p, out string detail))
            {
                Log($"  Using sigcheck64.exe at {p}");
                Log($"  Integrity: {detail}");
                return p;
            }
            Log($"  Found {p} but it FAILED integrity verification — ignoring. ({detail})");
        }

        try
        {
            var result = ProcessRunner.RunWithDetails("where", "sigcheck64.exe");
            if (result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output))
            {
                string pathCandidate = result.Output.Trim().Split('\n')[0].Trim();
                if (AuthenticodeVerifier.IsMicrosoftSigned(pathCandidate, out string pathDetail))
                {
                    Log($"  Using sigcheck64.exe from PATH: {pathCandidate}");
                    Log($"  Integrity: {pathDetail}");
                    return pathCandidate;
                }
                Log($"  PATH copy failed integrity verification — ignoring. ({pathDetail})");
            }
        }
        catch { }

        // Not found — download the latest build from Sysinternals Live
        string dest = Path.Combine(AppContext.BaseDirectory, "sigcheck64.exe");
        try
        {
            Log("  Downloading sigcheck64.exe from Sysinternals Live...");
            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "ShieldsUp/1.0");
                var data = await client.GetByteArrayAsync("https://live.sysinternals.com/sigcheck64.exe");

                // Integrity gate 1 — record the exact SHA256 we received
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "");
                    Log($"  Downloaded SHA256: {hash}");
                }

                File.WriteAllBytes(dest, data);
            }

            // Integrity gate 2 — the binary must carry a valid Microsoft
            // Authenticode signature (chain-validated, signer = Microsoft)
            if (!AuthenticodeVerifier.IsMicrosoftSigned(dest, out string verifyDetail))
            {
                File.Delete(dest);
                Log($"  ✗ Downloaded sigcheck64.exe FAILED integrity verification and was deleted. ({verifyDetail})");
                return null;
            }
            Log($"  Downloaded binary verified: {verifyDetail}");
            return dest;
        }
        catch (Exception ex)
        {
            Log($"  Download failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Reads "Field: value" lines from sigcheck output (case-insensitive,
    /// whitespace-tolerant). Returns null when the field is absent.
    /// </summary>
    private static string GetFieldValue(string output, string field)
    {
        if (string.IsNullOrEmpty(output)) return null;
        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith(field + ":", StringComparison.OrdinalIgnoreCase))
                return line.Substring(field.Length + 1).Trim();
        }
        return null;
    }

    private static string ExtractHash(string output, string hashType)
    {
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(hashType + ":", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Substring(hashType.Length + 1).Trim();
            }
        }
        return null;
    }

    // ═══════════════════════════════════════
    //  DBX REVOCATION CHECK
    // ═══════════════════════════════════════
    private async Task CheckDbxSafety()
    {
        Log("\n[4/4] Checking DBX revocation list...");

        string dbxPath = Path.Combine(AppContext.BaseDirectory, "DBXUpdate.bin");
        if (!File.Exists(dbxPath))
        {
            dbxPath = Path.Combine(Path.GetTempPath(), "DBXUpdate.bin");
        }

        if (!File.Exists(dbxPath))
        {
            SetStatus(DbxStatusText, "⚠ DBXUpdate.bin not found — download it first", false);
            Log("  No DBX update file found. Click 'Download' to get the latest.");
            return;
        }

        await Task.Run(() =>
        {
            try
            {
                byte[] dbxData = File.ReadAllBytes(dbxPath);
                Log($"  DBXUpdate.bin loaded: {dbxData.Length} bytes");

                // Parse EFI_SIGNATURE_LIST headers
                int offset = 0;
                int sigCount = 0;
                int listCount = 0;
                var sha256Guid = new Guid("c1c41626-504c-4092-aca9-41f936934328");

                while (offset + 28 <= dbxData.Length)
                {
                    // Read SignatureType GUID (16 bytes)
                    byte[] guidBytes = new byte[16];
                    Array.Copy(dbxData, offset, guidBytes, 0, 16);
                    Guid sigType = new Guid(guidBytes);

                    // Read ListSize (4 bytes at offset+16)
                    uint listSize = BitConverter.ToUInt32(dbxData, offset + 16);
                    // Read HeaderSize (4 bytes at offset+20)
                    uint headerSize = BitConverter.ToUInt32(dbxData, offset + 20);
                    // Read SignatureSize (4 bytes at offset+24)
                    uint sigSize = BitConverter.ToUInt32(dbxData, offset + 24);

                    if (listSize == 0 || listSize > dbxData.Length) break;

                    listCount++;
                    if (sigSize > 0)
                    {
                        uint dataSize = listSize - 28 - headerSize;
                        int sigs = (int)(dataSize / sigSize);
                        sigCount += sigs;

                        string typeStr = sigType == sha256Guid ? "SHA256" : sigType.ToString();
                        Log($"  List #{listCount}: {typeStr} — {sigs} signatures (entry size: {sigSize} bytes)");
                    }

                    offset += (int)listSize;
                }

                Log($"\n  Total: {listCount} signature lists, {sigCount} revocation entries");

                DispatcherQueue.TryEnqueue(() =>
                {
                    SetStatus(DbxStatusText,
                        $"✓ DBX parsed: {sigCount} revocation signatures across {listCount} lists",
                        true);
                });
            }
            catch (Exception ex)
            {
                Log($"  [ERROR] DBX parse failed: {ex.Message}");
                DispatcherQueue.TryEnqueue(() =>
                {
                    SetStatus(DbxStatusText, $"✗ DBX parse error: {ex.Message}", false);
                });
            }
        });
    }

    // ═══════════════════════════════════════
    //  DBX UPDATE DOWNLOAD / INSTALL
    // ═══════════════════════════════════════
    private async void DownloadDbxBtn_Click(object sender, RoutedEventArgs e)
    {
        DownloadDbxBtn.IsEnabled = false;
        DbxUpdateStatusText.Text = "Downloading...";
        Log("\nDownloading latest DBX update from Microsoft...");

        var result = await DbxRemediator.DownloadUpdateAsync();
        if (result.Error != null)
        {
            DbxUpdateStatusText.Text = $"✗ Download failed: {result.Error}";
            Log($"  Download failed: {result.Error}");
        }
        else
        {
            _downloadedDbxPath = result.Path;
            DbxUpdateStatusText.Text = $"✓ Downloaded. SHA256: {result.Checksum.Substring(0, 16)}...";
            Log($"  Downloaded to: {result.Path}");
            Log($"  SHA256: {result.Checksum}");
            InstallDbxBtn.IsEnabled = true;
        }
        DownloadDbxBtn.IsEnabled = true;
    }

    private async void InstallDbxBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_downloadedDbxPath))
        {
            await Helpers.Dialogs.Error("Download the update first.", "Error");
            return;
        }

        if (!await Helpers.Dialogs.Confirm(
                "Apply DBX update to UEFI firmware?\n\nThis modifies the Secure Boot revocation database. " +
                "Make sure your bootloader is not in the revocation list, or your system may not boot.",
                "CRITICAL WARNING", defaultIsPrimary: false))
            return;

        Log("\nApplying DBX update...");
        DbxUpdateStatusText.Text = "Installing...";
        var result = await DbxRemediator.InstallUpdateAsync(_downloadedDbxPath);
        if (result.Success)
        {
            DbxUpdateStatusText.Text = "✓ DBX update applied successfully. Reboot required.";
            Log("  DBX update applied. Please reboot.");
        }
        else
        {
            DbxUpdateStatusText.Text = $"✗ Install failed: {result.Error}";
            Log($"  Install failed: {result.Error}");
        }
    }

    private async void RepairBootBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!await Helpers.Dialogs.Confirm(
                "Repair bootloader? This will run:\n  bcdboot C:\\Windows /s Z: /f UEFI\n\nUse if bootloader is corrupted.",
                "Confirm Boot Repair"))
            return;

        Log("\nRepairing bootloader...");

        // Need EFI mounted
        bool mounted = await MountEfiPartition();
        if (!mounted)
        {
            Log("  Failed to mount EFI for boot repair.");
            return;
        }

        var result = await ProcessRunner.RunWithDetailsAsync("bcdboot",
            $@"C:\Windows /s {_efiDriveLetter} /f UEFI");

        if (result.ExitCode == 0)
        {
            Log("  Boot repair completed successfully.");
            await Helpers.Dialogs.Inform("Boot repair completed.", "Success");
        }
        else
        {
            Log($"  Boot repair failed: {result.Error}");
            await Helpers.Dialogs.Error($"Boot repair failed:\n{result.Error}", "Error");
        }

        await UnmountEfiPartition();
    }

    // ═══════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════
    private void SetStatus(TextBlock target, string text, bool isGood)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            target.Text = text;
            target.Foreground = isGood
                ? new SolidColorBrush(Microsoft.UI.Colors.ForestGreen)
                : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB2, 0x6A, 0x00));
        });
    }
}
