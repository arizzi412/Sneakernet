using System.Text.Json;

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

        public List<UpdateInstruction> AnalyzeForHome(string mainPath, string usbPath, ProgressReporter report)
        {
            if (!Directory.Exists(mainPath)) throw new DirectoryNotFoundException("Main drive path not found.");
            if (!Directory.Exists(usbPath)) throw new DirectoryNotFoundException("USB drive path not found.");

            string catalogPath = Path.Combine(usbPath, CATALOG_FILENAME);
            if (!File.Exists(catalogPath))
                throw new FileNotFoundException("Catalog not found on USB.\n\nPlease run 'Step 3: Initialize' at the Offsite location first.");

            report("Reading Offsite Catalog...", 10);
            var offsiteFiles = LoadCatalog(catalogPath);

            report("Scanning Main Drive...", 30);
            var mainFiles = ScanDrive(mainPath, offsiteFiles);

            report("Calculating Changes...", 80);
            var instructions = GenerateInstructions(mainFiles, offsiteFiles);

            report("Analysis Complete.", 100);
            return instructions;
        }

        public List<UpdateInstruction> AnalyzeForOffsite(string usbPath)
        {
            if (!Directory.Exists(usbPath)) throw new DirectoryNotFoundException("USB drive path not found.");

            string instrPath = Path.Combine(usbPath, INSTRUCTIONS_FILENAME);
            if (!File.Exists(instrPath)) return new List<UpdateInstruction>(); // Return empty if no instructions

            return JsonSerializer.Deserialize<List<UpdateInstruction>>(File.ReadAllText(instrPath));
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
                int pct = (int)((current / (float)total) * 100);

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

        // Inside SyncEngine.cs

        public SyncResult ExecuteOffsiteUpdate(string offsitePath, string usbPath, List<UpdateInstruction> instructions, ProgressReporter report)
        {
            var result = new SyncResult();
            string usbDataRoot = Path.Combine(usbPath, DATA_FOLDER_NAME);

            // Separate instructions by type
            var moves = instructions.Where(i => i.Action == "MOVE").ToList();
            var deletes = instructions.Where(i => i.Action == "DELETE").ToList();
            var copies = instructions.Where(i => i.Action == "COPY").ToList();

            int total = instructions.Count;
            int current = 0;

            // ---------------------------------------------------------
            // STEP 1: SAFE MOVE STAGING (Resolve Swaps)
            // Rename all move sources to temporary names first.
            // ---------------------------------------------------------
            var tempMoveMap = new Dictionary<UpdateInstruction, string>(); // Maps instruction -> TempFilePath

            foreach (var move in moves)
            {
                string sourceAbs = Path.Combine(offsitePath, move.Source);
                if (File.Exists(sourceAbs))
                {
                    try
                    {
                        // Create a unique temp filename in the SAME folder (to ensure atomic move within same volume)
                        string tempName = $"{move.Source}.{Guid.NewGuid()}.sneakertemp";
                        string tempAbs = Path.Combine(offsitePath, tempName);

                        File.Move(sourceAbs, tempAbs);
                        tempMoveMap[move] = tempAbs; // Remember where we put it
                    }
                    catch (Exception ex)
                    {
                        result.Errors++;
                        report($"Error Staging Move {move.Source}: {ex.Message}", 0);
                    }
                }
                else
                {
                    report($"Warning: Move Source Missing {move.Source}", 0);
                }
            }

            // ---------------------------------------------------------
            // STEP 2: DELETES
            // ---------------------------------------------------------
            foreach (var del in deletes)
            {
                current++;
                int pct = (int)((current / (float)total) * 100);
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

            // --- CRITICAL FIX: CLEANUP DIRS BEFORE COPY/MOVE ---
            // This removes empty folders so they don't block new files with the same name.
            report("Cleaning empty directories...", 0);
            CleanupEmptyDirs(offsitePath);
            // ---------------------------------------------------------
            // STEP 3: FINALIZE MOVES (Temp -> Destination)
            // ---------------------------------------------------------
            foreach (var move in moves)
            {
                current++;
                int pct = (int)((current / (float)total) * 100);

                // Did we successfully stage this file?
                if (tempMoveMap.TryGetValue(move, out string tempPath) && File.Exists(tempPath))
                {
                    string destAbs = Path.Combine(offsitePath, move.Destination);
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(destAbs));

                        // If destination exists now (rare, but possible if user has dupes), delete it
                        if (File.Exists(destAbs)) File.Delete(destAbs);

                        File.Move(tempPath, destAbs);
                        result.FilesMoved++;
                        report($"Moved: {move.Source} -> {move.Destination}", pct);
                    }
                    catch (Exception ex)
                    {
                        result.Errors++;
                        report($"Error Finishing Move {move.Source}: {ex.Message}", pct);

                        // Attempt to restore file to original name if possible
                        try
                        {
                            string originalAbs = Path.Combine(offsitePath, move.Source);
                            if (!File.Exists(originalAbs)) File.Move(tempPath, originalAbs);
                        }
                        catch { }
                    }
                }
            }

            // ---------------------------------------------------------
            // STEP 4: COPIES (New Files from USB)
            // ---------------------------------------------------------
            foreach (var copy in copies)
            {
                current++;
                int pct = (int)((current / (float)total) * 100);

                string destAbs = Path.Combine(offsitePath, copy.Source); // Destination is same as Source path relative to root
                string usbSrc = Path.Combine(usbDataRoot, copy.Source);

                try
                {
                    if (File.Exists(usbSrc))
                    {
                        // 1. Handle "File Replacing Folder" Collision
                        // If a folder exists where this file needs to go, nuke the folder.
                        if (Directory.Exists(destAbs))
                        {
                            // Recursive delete in case untracked files are inside
                            Directory.Delete(destAbs, true);
                        }

                        Directory.CreateDirectory(Path.GetDirectoryName(destAbs));
                        File.Copy(usbSrc, destAbs, true); // Overwrite allowed here
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

            // Cleanup
            CleanupEmptyDirs(offsitePath);
            try { File.Delete(Path.Combine(usbPath, INSTRUCTIONS_FILENAME)); } catch { }
            try { if (Directory.Exists(usbDataRoot)) Directory.Delete(usbDataRoot, true); } catch { }

            report("Generating new catalog...", 100);
            GenerateCatalog(offsitePath, usbPath);

            return result;
        }

        public void GenerateCatalog(string drivePath, string usbPath)
        {
            // 1. Scan and Save Catalog
            var files = ScanDrive(drivePath, null);
            string json = JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(usbPath, CATALOG_FILENAME), json);

            // 2. SAFETY: Delete the instructions file.
            // Since we just defined the "Fresh State", any old pending instructions 
            // are now obsolete and dangerous. Nuke them.
            string instrPath = Path.Combine(usbPath, INSTRUCTIONS_FILENAME);
            if (File.Exists(instrPath))
            {
                try { File.Delete(instrPath); } catch { }
            }

            // 3. CLEANUP: Also delete the Data folder on USB if it exists.
            // If we are re-indexing, we shouldn't have loose data waiting to be copied.
            string dataPath = Path.Combine(usbPath, DATA_FOLDER_NAME);
            if (Directory.Exists(dataPath))
            {
                try { Directory.Delete(dataPath, true); } catch { }
            }
        }

        // --- OPTIMIZED CORE LOGIC (DICT/HASHSET) ---

        private List<UpdateInstruction> GenerateInstructions(List<FileEntry> mainFiles, List<FileEntry> offsiteFiles)
        {
            var instructions = new List<UpdateInstruction>();

            // 1. Index Offsite files
            var offsitePathMap = offsiteFiles.ToDictionary(x => x.RelativePath, x => x, StringComparer.OrdinalIgnoreCase);
            var matchedOffsitePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unmatchedMainFiles = new List<FileEntry>();

            // 2. PASS 1: Check for Exact Matches
            foreach (var main in mainFiles)
            {
                if (offsitePathMap.TryGetValue(main.RelativePath, out var existing))
                {
                    // Allow 2.1s tolerance for FAT32
                    bool isSame = (main.Size == existing.Size) &&
                                  (Math.Abs((main.LastWriteTime - existing.LastWriteTime).TotalSeconds) < 2.1);

                    if (isSame)
                    {
                        // Exact Match: Synced.
                        matchedOffsitePaths.Add(existing.RelativePath);
                    }
                    else
                    {
                        // Mismatch!
                        // CRITICAL CHANGE: Do NOT auto-queue COPY yet. 
                        // Treat as Unmatched. This allows "Swap" logic to see if 'main' came from another file.
                        unmatchedMainFiles.Add(main);
                    }
                }
                else
                {
                    // New File
                    unmatchedMainFiles.Add(main);
                }
            }

            // 3. Index potential Move Sources (Offsite files not exactly matched yet)
            var potentialMoveSources = offsiteFiles
                .Where(x => !matchedOffsitePaths.Contains(x.RelativePath))
                .GroupBy(x => x.Size)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 4. PASS 2: Detect Moves vs Copies vs Overwrites
            foreach (var main in unmatchedMainFiles)
            {
                FileEntry moveSource = null;

                // Try to find a Move Source (Same Size + Time)
                if (potentialMoveSources.TryGetValue(main.Size, out var candidates))
                {
                    var matchIndex = candidates.FindIndex(x => Math.Abs((x.LastWriteTime - main.LastWriteTime).TotalSeconds) < 2.1);
                    if (matchIndex != -1)
                    {
                        moveSource = candidates[matchIndex];
                        candidates.RemoveAt(matchIndex);
                        if (candidates.Count == 0) potentialMoveSources.Remove(main.Size);
                    }
                }

                if (moveSource != null)
                {
                    // It is a MOVE
                    instructions.Add(new UpdateInstruction
                    {
                        Action = "MOVE",
                        Source = moveSource.RelativePath,
                        Destination = main.RelativePath,
                        SizeInfo = "-"
                    });
                }
                else
                {
                    // It is NOT a Move. It is either NEW or an OVERWRITE.
                    // Check if it's an overwrite (did the old file exist at this path?)
                    // We reuse offsitePathMap to check the original location.
                    if (offsitePathMap.TryGetValue(main.RelativePath, out var oldVersion) &&
                        !matchedOffsitePaths.Contains(main.RelativePath))
                    {
                        // It's an in-place edit (Overwrite).
                        // To keep logic clean, we handle this as "COPY".
                        // We must also manually "consume" the old file so it doesn't get marked as DELETE later.

                        // Remove the old version from the "Deletes" pool (potentialMoveSources)
                        // We have to hunt for it because potentialMoveSources is grouped by Size
                        if (potentialMoveSources.TryGetValue(oldVersion.Size, out var oldCandidates))
                        {
                            var selfRef = oldCandidates.FirstOrDefault(x => x.RelativePath == oldVersion.RelativePath);
                            if (selfRef != null)
                            {
                                oldCandidates.Remove(selfRef);
                                if (oldCandidates.Count == 0) potentialMoveSources.Remove(oldVersion.Size);
                            }
                        }
                    }

                    // Queue the Copy
                    instructions.Add(new UpdateInstruction
                    {
                        Action = "COPY",
                        Source = main.RelativePath,
                        Destination = main.RelativePath,
                        RawSizeBytes = main.Size,
                        SizeInfo = BytesToMb(main.Size)
                    });
                }
            }

            // 5. PASS 3: Detect Deletes
            foreach (var kvp in potentialMoveSources)
            {
                foreach (var item in kvp.Value)
                {
                    instructions.Add(new UpdateInstruction
                    {
                        Action = "DELETE",
                        Source = item.RelativePath,
                        SizeInfo = "-"
                    });
                }
            }

            return instructions;
        }

        private List<FileEntry> ScanDrive(string root, List<FileEntry> prev)
        {
            var results = new List<FileEntry>();
            var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint };
            try
            {
                foreach (var f in Directory.EnumerateFiles(root, "*", opts))
                {
                    try
                    {
                        var info = new FileInfo(f);
                        results.Add(new FileEntry { RelativePath = Path.GetRelativePath(root, f), Size = info.Length, LastWriteTime = info.LastWriteTimeUtc });
                    }
                    catch { }
                }
            }
            catch { }
            return results;
        }

        private List<FileEntry> LoadCatalog(string p) => File.Exists(p) ? JsonSerializer.Deserialize<List<FileEntry>>(File.ReadAllText(p)) : new List<FileEntry>();
        private long GetFreeSpaceMb(string p) => new DriveInfo(Path.GetPathRoot(p)).AvailableFreeSpace / 1024 / 1024;
        private string BytesToMb(long b) => (b / 1024.0 / 1024.0).ToString("0.00") + " MB";
        private void CleanupEmptyDirs(string p) { try { foreach (var d in Directory.GetDirectories(p)) { CleanupEmptyDirs(d); if (!Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d); } } catch { } }
    }
}