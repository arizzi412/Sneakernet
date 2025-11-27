using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SneakerNetSync
{
    public delegate void ProgressReporter(string logMessage, int percent);

    public class SyncEngine
    {
        const string CATALOG_FILENAME = "offsite_catalog.json";
        const string INSTRUCTIONS_FILENAME = "instructions.json";
        const string DATA_FOLDER_NAME = "Data";
        const int MIN_FREE_SPACE_MB = 200;

        private class ExclusionRule
        {
            public Regex Pattern { get; set; }
            public bool DirectoryOnly { get; set; }
        }

        // --- PHASE 1: ANALYSIS ---

        public List<UpdateInstruction> AnalyzeForHome(string mainPath, string usbPath, List<string> exclusions, ProgressReporter report)
        {
            if (!Directory.Exists(mainPath)) throw new DirectoryNotFoundException("Main drive path not found.");
            if (!Directory.Exists(usbPath)) throw new DirectoryNotFoundException("USB drive path not found.");

            string catalogPath = Path.Combine(usbPath, CATALOG_FILENAME);
            if (!File.Exists(catalogPath))
                throw new FileNotFoundException("Catalog not found on USB.\n\nPlease run 'Step 3: Initialize' at the Offsite location first.");

            report("Reading Offsite Catalog...", 10);
            var offsiteFiles = LoadCatalog(catalogPath);

            report("Scanning Main Drive...", 30);
            var mainFiles = ScanDrive(mainPath, exclusions);

            report("Calculating Changes...", 80);
            var instructions = GenerateInstructions(mainFiles, offsiteFiles);

            report("Analysis Complete.", 100);
            return instructions;
        }

        public List<UpdateInstruction> AnalyzeForOffsite(string usbPath)
        {
            if (!Directory.Exists(usbPath)) throw new DirectoryNotFoundException("USB drive path not found.");

            string instrPath = Path.Combine(usbPath, INSTRUCTIONS_FILENAME);
            if (!File.Exists(instrPath)) return new List<UpdateInstruction>();

            try
            {
                return JsonSerializer.Deserialize<List<UpdateInstruction>>(File.ReadAllText(instrPath)) ?? new List<UpdateInstruction>();
            }
            catch
            {
                return new List<UpdateInstruction>();
            }
        }

        // --- PHASE 2: EXECUTION ---

        public SyncResult ExecuteHomeTransfer(string mainPath, string usbPath, List<UpdateInstruction> instructions, ProgressReporter report)
        {
            var result = new SyncResult();
            string usbDataRoot = Path.Combine(usbPath, DATA_FOLDER_NAME);
            Directory.CreateDirectory(usbDataRoot);

            var copies = instructions.Where(i => i.Action == "COPY").ToList();
            var instructionsToSave = instructions.Where(i => i.Action != "COPY").ToList();

            bool usbFull = false;
            int total = copies.Count;
            int current = 0;

            foreach (var copy in copies)
            {
                current++;
                int pct = total > 0 ? (int)((current / (float)total) * 100) : 0;

                if (usbFull)
                {
                    report($"Skipped (USB Full): {copy.Source}", pct);
                    continue;
                }

                if (GetFreeSpaceMb(usbPath) < MIN_FREE_SPACE_MB)
                {
                    report("USB Full! Stopping copies.", pct);
                    usbFull = true;
                    continue;
                }

                try
                {
                    string src = Path.Combine(mainPath, copy.Source);
                    string dest = Path.Combine(usbDataRoot, copy.Source);

                    if (!File.Exists(src))
                    {
                        report($"Skipped (Missing): {copy.Source}", pct);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(src, dest, true);

                    instructionsToSave.Add(copy);
                    result.FilesCopied++;
                    result.BytesTransferred += copy.RawSizeBytes;
                    report($"Copied: {copy.Source}", pct);
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    report($"Error: {ex.Message}", pct);
                }
            }

            string json = JsonSerializer.Serialize(instructionsToSave, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(usbPath, INSTRUCTIONS_FILENAME), json);

            return result;
        }

        public SyncResult ExecuteOffsiteUpdate(string offsitePath, string usbPath, List<UpdateInstruction> instructions, ProgressReporter report)
        {
            var result = new SyncResult();
            string usbDataRoot = Path.Combine(usbPath, DATA_FOLDER_NAME);

            var moves = instructions.Where(i => i.Action == "MOVE").ToList();
            var deletes = instructions.Where(i => i.Action == "DELETE").ToList();
            var copies = instructions.Where(i => i.Action == "COPY").ToList();

            int total = instructions.Count;
            int current = 0;

            // 1. STAGE MOVES (Resolve Swaps/Renames)
            var tempMoveMap = new Dictionary<UpdateInstruction, string>();

            foreach (var move in moves)
            {
                string sourceAbs = Path.Combine(offsitePath, move.Source);
                if (File.Exists(sourceAbs))
                {
                    try
                    {
                        string tempName = $"{move.Source}.{Guid.NewGuid()}.sneakertemp";
                        string tempAbs = Path.Combine(offsitePath, tempName);
                        File.Move(sourceAbs, tempAbs);
                        tempMoveMap[move] = tempAbs;
                    }
                    catch (Exception ex)
                    {
                        result.Errors++;
                        report($"Error Staging Move {move.Source}: {ex.Message}", 0);
                    }
                }
            }

            // 2. DELETES
            foreach (var del in deletes)
            {
                current++;
                int pct = total > 0 ? (int)((current / (float)total) * 100) : 0;
                string path = Path.Combine(offsitePath, del.Source);

                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                        result.FilesDeleted++;
                        report($"Deleted: {del.Source}", pct);
                    }
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    report($"Error Deleting {del.Source}: {ex.Message}", pct);
                }
            }

            report("Cleaning empty directories...", 0);
            CleanupEmptyDirs(offsitePath);

            // 3. FINISH MOVES
            foreach (var move in moves)
            {
                current++;
                int pct = total > 0 ? (int)((current / (float)total) * 100) : 0;

                if (tempMoveMap.TryGetValue(move, out string tempPath) && File.Exists(tempPath))
                {
                    string destAbs = Path.Combine(offsitePath, move.Destination);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destAbs));
                        if (File.Exists(destAbs)) File.Delete(destAbs);
                        File.Move(tempPath, destAbs);
                        result.FilesMoved++;
                        report($"Moved: {move.Source} -> {move.Destination}", pct);
                    }
                    catch (Exception ex)
                    {
                        result.Errors++;
                        report($"Error Finishing Move {move.Source}: {ex.Message}", pct);
                        try
                        {
                            string originalAbs = Path.Combine(offsitePath, move.Source);
                            if (!File.Exists(originalAbs)) File.Move(tempPath, originalAbs);
                        }
                        catch { }
                    }
                }
            }

            // 4. COPIES
            foreach (var copy in copies)
            {
                current++;
                int pct = total > 0 ? (int)((current / (float)total) * 100) : 0;

                string destAbs = Path.Combine(offsitePath, copy.Source);
                string usbSrc = Path.Combine(usbDataRoot, copy.Source);

                try
                {
                    if (File.Exists(usbSrc))
                    {
                        if (Directory.Exists(destAbs)) Directory.Delete(destAbs, true);
                        Directory.CreateDirectory(Path.GetDirectoryName(destAbs));
                        File.Copy(usbSrc, destAbs, true);
                        result.FilesCopied++;
                        result.BytesTransferred += copy.RawSizeBytes;
                        report($"Updated: {copy.Source}", pct);
                    }
                    else
                    {
                        report($"Skipped (Missing on USB): {copy.Source}", pct);
                    }
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    report($"Error Copying {copy.Source}: {ex.Message}", pct);
                }
            }

            CleanupEmptyDirs(offsitePath);
            try { File.Delete(Path.Combine(usbPath, INSTRUCTIONS_FILENAME)); } catch { }
            try { if (Directory.Exists(usbDataRoot)) Directory.Delete(usbDataRoot, true); } catch { }

            report("Generating new catalog...", 100);
            GenerateCatalog(offsitePath, usbPath);

            return result;
        }

        public void GenerateCatalog(string drivePath, string usbPath)
        {
            var files = ScanDrive(drivePath);
            string json = JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(usbPath, CATALOG_FILENAME), json);

            string instrPath = Path.Combine(usbPath, INSTRUCTIONS_FILENAME);
            if (File.Exists(instrPath)) try { File.Delete(instrPath); } catch { }

            string dataPath = Path.Combine(usbPath, DATA_FOLDER_NAME);
            if (Directory.Exists(dataPath)) try { Directory.Delete(dataPath, true); } catch { }
        }

        // --- CORE LOGIC ---

        private List<UpdateInstruction> GenerateInstructions(List<FileEntry> mainFiles, List<FileEntry> offsiteFiles)
        {
            var instructions = new List<UpdateInstruction>();
            // Lookup for exact offsite paths
            var offsitePathMap = offsiteFiles.ToDictionary(x => x.RelativePath, x => x, StringComparer.OrdinalIgnoreCase);

            // Track which offsite files are perfectly matched (and thus "claimed")
            var matchedOffsitePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unmatchedMainFiles = new List<FileEntry>();

            // 1. Identify Exact Matches (Update detection - same path, same content)
            foreach (var main in mainFiles)
            {
                if (offsitePathMap.TryGetValue(main.RelativePath, out var existing))
                {
                    // Strict equality check: Size must match, Time must be very close
                    bool isSame = (main.Size == existing.Size) &&
                                  (Math.Abs((main.LastWriteTime - existing.LastWriteTime).TotalSeconds) < 0.1);

                    if (isSame)
                    {
                        // Content matches. Check for Case-Only Rename.
                        if (!string.Equals(main.RelativePath, existing.RelativePath, StringComparison.Ordinal))
                        {
                            instructions.Add(new UpdateInstruction { Action = "MOVE", Source = existing.RelativePath, Destination = main.RelativePath, SizeInfo = "-" });
                        }
                        matchedOffsitePaths.Add(existing.RelativePath);
                    }
                    else
                    {
                        // Same path, but content changed (Update)
                        unmatchedMainFiles.Add(main);
                    }
                }
                else
                {
                    // New file locally
                    unmatchedMainFiles.Add(main);
                }
            }

            // 2. Prepare Potential Move Sources
            // Any offsite file NOT matched by path/content is a candidate to be moved or deleted.
            var moveSourceMap = offsiteFiles
                .Where(x => !matchedOffsitePaths.Contains(x.RelativePath))
                .GroupBy(x => x.Size) // Group by size for efficiency
                .ToDictionary(g => g.Key, g => g.ToList());

            // 3. PASS 1: Detect Moves
            // We prioritize Moves over Copies. If a file exists elsewhere with same metadata, it's a move.
            var handledMainFiles = new HashSet<FileEntry>();

            foreach (var main in unmatchedMainFiles)
            {
                if (moveSourceMap.TryGetValue(main.Size, out var candidates))
                {
                    // Find candidate with matching timestamp
                    var matchIndex = candidates.FindIndex(x => Math.Abs((x.LastWriteTime - main.LastWriteTime).TotalSeconds) < 0.1);
                    if (matchIndex != -1)
                    {
                        var source = candidates[matchIndex];

                        // Create MOVE instruction
                        instructions.Add(new UpdateInstruction { Action = "MOVE", Source = source.RelativePath, Destination = main.RelativePath, SizeInfo = "-" });

                        // Mark Main file as handled
                        handledMainFiles.Add(main);

                        // Remove from candidates (Source is "consumed")
                        candidates.RemoveAt(matchIndex);
                        if (candidates.Count == 0) moveSourceMap.Remove(main.Size);
                    }
                }
            }

            // 4. PASS 2: Generate Copies (Updates & New Files)
            foreach (var main in unmatchedMainFiles)
            {
                if (handledMainFiles.Contains(main)) continue;

                instructions.Add(new UpdateInstruction
                {
                    Action = "COPY",
                    Source = main.RelativePath,
                    Destination = main.RelativePath,
                    RawSizeBytes = main.Size,
                    SizeInfo = BytesToMb(main.Size)
                });

                // Prevent implicit Delete of the file we are overwriting.
                // If we are doing a COPY (Update) of 'A.txt', and 'A.txt' exists offsite but wasn't moved,
                // then 'A.txt' (Offsite) is effectively replaced. We should remove it from moveSourceMap
                // so we don't generate a "DELETE A.txt" instruction later.
                if (offsitePathMap.TryGetValue(main.RelativePath, out var oldVersion))
                {
                    if (moveSourceMap.TryGetValue(oldVersion.Size, out var candidates))
                    {
                        // Find and remove the exact old version from candidates
                        var selfRef = candidates.FirstOrDefault(x => x.RelativePath == oldVersion.RelativePath);
                        if (selfRef != null)
                        {
                            candidates.Remove(selfRef);
                            if (candidates.Count == 0) moveSourceMap.Remove(oldVersion.Size);
                        }
                    }
                }
            }

            // 5. Generate Deletes
            // Any offsite file still in moveSourceMap was neither matched, moved, nor updated.
            foreach (var kvp in moveSourceMap)
            {
                foreach (var item in kvp.Value)
                {
                    instructions.Add(new UpdateInstruction { Action = "DELETE", Source = item.RelativePath, SizeInfo = "-" });
                }
            }

            return instructions;
        }

        private List<FileEntry> ScanDrive(string root, List<string>? exclusions = null)
        {
            var results = new List<FileEntry>();
            if (!Directory.Exists(root)) return results;

            var rules = new List<ExclusionRule>();
            if (exclusions != null)
            {
                foreach (var rawPattern in exclusions)
                {
                    if (string.IsNullOrWhiteSpace(rawPattern)) continue;
                    string pattern = rawPattern.Trim();
                    bool isDirOnly = false;

                    // 1. Handle Folder-Only syntax (trailing slash)
                    if (pattern.EndsWith(Path.DirectorySeparatorChar) || pattern.EndsWith(Path.AltDirectorySeparatorChar))
                    {
                        isDirOnly = true;
                        pattern = pattern.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    }

                    // 2. Convert Wildcards to Regex
                    // Escape special regex characters (like dots, brackets)
                    string regexPattern = Regex.Escape(pattern)
                                               .Replace("\\*", ".*")  // Convert * to .*
                                               .Replace("\\?", ".");  // Convert ? to .

                    // 3. STRICT ANCHORING: 
                    // Always wrap in ^ and $ so "temp" does not match "template"
                    regexPattern = "^" + regexPattern + "$";

                    rules.Add(new ExclusionRule
                    {
                        Pattern = new Regex(regexPattern, RegexOptions.IgnoreCase),
                        DirectoryOnly = isDirOnly
                    });
                }
            }

            var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint };

            try
            {
                foreach (var f in Directory.EnumerateFiles(root, "*", opts))
                {
                    string relPath = Path.GetRelativePath(root, f);
                    if (IsExcluded(relPath, rules)) continue;

                    try
                    {
                        var info = new FileInfo(f);
                        results.Add(new FileEntry { RelativePath = relPath, Size = info.Length, LastWriteTime = info.LastWriteTimeUtc });
                    }
                    catch { }
                }
            }
            catch { }
            return results;
        }

        private bool IsExcluded(string relPath, List<ExclusionRule> rules)
        {
            if (rules == null || rules.Count == 0) return false;
            string fileName = Path.GetFileName(relPath);
            var segments = Path.GetDirectoryName(relPath)?.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rule in rules)
            {
                if (segments != null)
                {
                    foreach (var part in segments) if (rule.Pattern.IsMatch(part)) return true;
                }
                if (!rule.DirectoryOnly)
                {
                    if (rule.Pattern.IsMatch(fileName)) return true;
                    if (rule.Pattern.IsMatch(relPath)) return true;
                }
            }
            return false;
        }

        private List<FileEntry> LoadCatalog(string p)
        {
            try
            {
                if (File.Exists(p))
                    return JsonSerializer.Deserialize<List<FileEntry>>(File.ReadAllText(p));
            }
            catch { /* Ignore corruption and treat as empty */ }
            return new List<FileEntry>();
        }

        private long GetFreeSpaceMb(string p) => new DriveInfo(Path.GetPathRoot(p)).AvailableFreeSpace / 1024 / 1024;
        private string BytesToMb(long b) => (b / 1024.0 / 1024.0).ToString("0.00") + " MB";
        private void CleanupEmptyDirs(string p) { try { foreach (var d in Directory.GetDirectories(p)) { CleanupEmptyDirs(d); if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } } catch { } }
    }
}