using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using DotNet.Globbing; // Requires 'DotNet.Glob' NuGet package

namespace SneakerNetSync
{
    public delegate void ProgressReporter(string logMessage, int percent);

    public class SyncEngine
    {
        const string CATALOG_FILENAME = "offsite_catalog.json";
        const string INSTRUCTIONS_FILENAME = "instructions.json";
        const string DATA_FOLDER_NAME = "Data";
        const int MIN_FREE_SPACE_MB = 200;

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
            var offsitePathMap = offsiteFiles.ToDictionary(x => x.RelativePath, x => x, StringComparer.OrdinalIgnoreCase);
            var matchedOffsitePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unmatchedMainFiles = new List<FileEntry>();

            foreach (var main in mainFiles)
            {
                if (offsitePathMap.TryGetValue(main.RelativePath, out var existing))
                {
                    bool isSame = (main.Size == existing.Size) &&
                                  (Math.Abs((main.LastWriteTime - existing.LastWriteTime).TotalSeconds) < 0.1);

                    if (isSame)
                    {
                        if (!string.Equals(main.RelativePath, existing.RelativePath, StringComparison.Ordinal))
                        {
                            instructions.Add(new UpdateInstruction { Action = "MOVE", Source = existing.RelativePath, Destination = main.RelativePath, SizeInfo = "-" });
                        }
                        matchedOffsitePaths.Add(existing.RelativePath);
                    }
                    else
                    {
                        unmatchedMainFiles.Add(main);
                    }
                }
                else
                {
                    unmatchedMainFiles.Add(main);
                }
            }

            var moveSourceMap = offsiteFiles
                .Where(x => !matchedOffsitePaths.Contains(x.RelativePath))
                .GroupBy(x => x.Size)
                .ToDictionary(g => g.Key, g => g.ToList());

            var handledMainFiles = new HashSet<FileEntry>();

            foreach (var main in unmatchedMainFiles)
            {
                if (moveSourceMap.TryGetValue(main.Size, out var candidates))
                {
                    var matchIndex = candidates.FindIndex(x => Math.Abs((x.LastWriteTime - main.LastWriteTime).TotalSeconds) < 0.1);
                    if (matchIndex != -1)
                    {
                        var source = candidates[matchIndex];
                        instructions.Add(new UpdateInstruction { Action = "MOVE", Source = source.RelativePath, Destination = main.RelativePath, SizeInfo = "-" });
                        handledMainFiles.Add(main);
                        candidates.RemoveAt(matchIndex);
                        if (candidates.Count == 0) moveSourceMap.Remove(main.Size);
                    }
                }
            }

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

                if (offsitePathMap.TryGetValue(main.RelativePath, out var oldVersion))
                {
                    if (moveSourceMap.TryGetValue(oldVersion.Size, out var candidates))
                    {
                        var selfRef = candidates.FirstOrDefault(x => x.RelativePath == oldVersion.RelativePath);
                        if (selfRef != null)
                        {
                            candidates.Remove(selfRef);
                            if (candidates.Count == 0) moveSourceMap.Remove(oldVersion.Size);
                        }
                    }
                }
            }

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

            // Structure to hold glob and whether it requires a directory context
            var globRules = new List<(Glob Pattern, bool IsDirectoryOnly)>();
            var globOptions = new GlobOptions { Evaluation = { CaseInsensitive = true } };

            if (exclusions != null)
            {
                foreach (var pattern in exclusions.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    string p = pattern.Trim().Replace('\\', '/');
                    bool isDir = p.EndsWith("/");

                    if (isDir)
                    {
                        // Directory Only: "Build*/"
                        // Pattern becomes "Build*/**/*" to match content.
                        // We mark IsDirectoryOnly=true to strictly ignore root files like "Builder.exe"
                        globRules.Add((Glob.Parse(p + "**/*", globOptions), true));
                    }
                    else
                    {
                        // File or Folder: "bin"
                        // 1. Exact match (File or Folder name matches "bin")
                        globRules.Add((Glob.Parse(p, globOptions), false));
                        // 2. Recursive match (Contents of folder "bin/**/*")
                        //    This part acts as a directory-only match
                        globRules.Add((Glob.Parse(p + "/**/*", globOptions), true));
                    }
                }
            }

            var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint };

            foreach (var f in Directory.EnumerateFiles(root, "*", opts))
            {
                // Normalize path for Globbing (forward slashes)
                string relPath = Path.GetRelativePath(root, f).Replace('\\', '/');

                // Root files (no slashes) cannot be inside a relative directory
                bool hasDirectoryParts = relPath.Contains('/');

                bool isExcluded = false;
                foreach (var rule in globRules)
                {
                    if (rule.IsDirectoryOnly)
                    {
                        // If rule is "Folder-Only" (e.g. "Build*/" -> "Build*/**/*"), 
                        // it shouldn't match a file in the root (like "Builder.exe").
                        if (!hasDirectoryParts) continue;
                    }

                    if (rule.Pattern.IsMatch(relPath))
                    {
                        isExcluded = true;
                        break;
                    }
                }

                if (isExcluded) continue;

                try
                {
                    var info = new FileInfo(f);
                    results.Add(new FileEntry
                    {
                        // Return to system separators for the rest of the app
                        RelativePath = relPath.Replace('/', Path.DirectorySeparatorChar),
                        Size = info.Length,
                        LastWriteTime = info.LastWriteTimeUtc
                    });
                }
                catch { }
            }
            return results;
        }

        private List<FileEntry> LoadCatalog(string p)
        {
            try
            {
                if (File.Exists(p))
                    return JsonSerializer.Deserialize<List<FileEntry>>(File.ReadAllText(p));
            }
            catch { }
            return new List<FileEntry>();
        }

        private long GetFreeSpaceMb(string p) => new DriveInfo(Path.GetPathRoot(p)).AvailableFreeSpace / 1024 / 1024;
        private string BytesToMb(long b) => (b / 1024.0 / 1024.0).ToString("0.00") + " MB";
        private void CleanupEmptyDirs(string p) { try { foreach (var d in Directory.GetDirectories(p)) { CleanupEmptyDirs(d); if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } } catch { } }
    }
}