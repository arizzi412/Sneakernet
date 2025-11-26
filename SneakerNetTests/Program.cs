using SneakerNetSync;

namespace SneakerNetTests
{
    class Program
    {
        // Test Configuration
        static string TestRoot = Path.Combine(Path.GetTempPath(), "SneakerNet_Tests");
        static string MainPath => Path.Combine(TestRoot, "Main");
        static string OffsitePath => Path.Combine(TestRoot, "Offsite");
        static string UsbPath => Path.Combine(TestRoot, "USB");

        static void Main(string[] args)
        {
            Console.WriteLine("=== STARTING SNEAKERNET EXTENDED TEST SUITE (NTFS) ===\n");

            try
            {
                // BASE FUNCTIONALITY
                RunTest("1. Base: Add New File", Test_Base_Add);
                RunTest("2. Base: Delete File", Test_Base_Delete);
                RunTest("3. Base: Update File Content", Test_Base_Update);
                RunTest("4. Base: Rename File", Test_Base_Rename);
                RunTest("5. Base: Deep Directory Structure", Test_Base_DeepRecursion);

                // SYNC LOGIC
                RunTest("6. Sync: Swap Conflict (A<->B)", Test_Sync_Swap);
                RunTest("7. Sync: File Replacing Folder", Test_Sync_FileReplacingFolder);
                RunTest("8. Sync: Zero Byte Files", Test_Sync_ZeroByte);

                // EXCLUSIONS
                RunTest("9. Exclude: New File Pattern (*.tmp)", Test_Exclude_NewFile);
                RunTest("10. Exclude: Folder Pattern (bin)", Test_Exclude_Folder);
                RunTest("11. Exclude: Retroactive (Exists on Remote -> Deleted)", Test_Exclude_Retroactive);

                // ROBUSTNESS
                RunTest("12. Robust: Locked File Access", Test_Robust_LockedFile);
                RunTest("13. Robust: Instruction File Cleanup on Init", Test_InstructionFile_Cleanup);

                RunTest("14. Exclude: Folder vs File Name Conflict", Test_Exclude_FolderVsFile);
            }
            finally
            {
                Cleanup();
            }

            Console.WriteLine("\n=== ALL TESTS COMPLETE ===");
            Console.ReadLine();
        }

        static void RunTest(string name, Action testMethod)
        {
            Console.Write($"{name.PadRight(55)} : ");
            Cleanup();
            SetupDirs();
            try
            {
                testMethod();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASS");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"FAIL - {ex.Message}");
                //Console.WriteLine(ex.StackTrace); // Uncomment for debug
            }
            Console.ResetColor();
        }

        // ========================================================================
        // BASE FUNCTIONALITY TESTS
        // ========================================================================

        static void Test_Base_Add()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, []); // Init

            // Action: Add file to Main
            CreateFile(MainPath, "newdoc.txt", "Important Data");

            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);
            Assert(instructions.Count == 1, "Should allow 1 copy");
            Assert(instructions[0].Action == "COPY", "Action is COPY");
            Assert(instructions[0].Source == "newdoc.txt", "File is newdoc.txt");

            // Execute
            engine.ExecuteHomeTransfer(MainPath, UsbPath, instructions, MockReport);
            var offsiteInstr = engine.AnalyzeForOffsite(UsbPath);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, offsiteInstr, MockReport);

            Assert(File.Exists(Path.Combine(OffsitePath, "newdoc.txt")), "File arrived at Offsite");
        }

        static void Test_Base_Delete()
        {
            var engine = new SyncEngine();
            // Setup: File exists on both
            CreateFile(MainPath, "todelete.txt", "data");
            CreateFile(OffsitePath, "todelete.txt", "data");
            SyncTimestamps("todelete.txt"); // Ensure synced

            engine.GenerateCatalog(OffsitePath, UsbPath, []);

            // Action: Delete from Main
            File.Delete(Path.Combine(MainPath, "todelete.txt"));

            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);
            Assert(instructions.Count == 1, "Should have 1 instruction");
            Assert(instructions[0].Action == "DELETE", "Action is DELETE");

            // Execute
            var json = System.Text.Json.JsonSerializer.Serialize(instructions);
            File.WriteAllText(Path.Combine(UsbPath, "instructions.json"), json);

            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, instructions, MockReport);
            Assert(!File.Exists(Path.Combine(OffsitePath, "todelete.txt")), "File deleted from Offsite");
        }

        static void Test_Base_Update()
        {
            var engine = new SyncEngine();
            // Setup: File exists on both
            CreateFile(OffsitePath, "report.doc", "Version 1");
            CreateFile(MainPath, "report.doc", "Version 2 (Larger)"); // Diff size
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "report.doc"), DateTime.UtcNow.AddMinutes(5));

            engine.GenerateCatalog(OffsitePath, UsbPath, []);

            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);
            Assert(instructions.Count == 1, "Should detect update");
            Assert(instructions[0].Action == "COPY", "Action is COPY (Overwrite)");

            // Cycle
            engine.ExecuteHomeTransfer(MainPath, UsbPath, instructions, MockReport);
            var offsiteInstr = engine.AnalyzeForOffsite(UsbPath);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, offsiteInstr, MockReport);

            var content = File.ReadAllText(Path.Combine(OffsitePath, "report.doc"));
            Assert(content == "Version 2 (Larger)", "Content updated on Offsite");
        }

        static void Test_Base_Rename()
        {
            var engine = new SyncEngine();
            CreateFile(OffsitePath, "cat.jpg", "image_data");
            CreateFile(MainPath, "dog.jpg", "image_data");

            // Sync timestamps exactly
            var t = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, "cat.jpg"), t);
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "dog.jpg"), t);

            engine.GenerateCatalog(OffsitePath, UsbPath, []);

            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);
            Assert(instructions.Count == 1, "Should detect move");
            Assert(instructions[0].Action == "MOVE", "Action is MOVE");
            Assert(instructions[0].Source == "cat.jpg", "Source correct");
            Assert(instructions[0].Destination == "dog.jpg", "Dest correct");
        }

        static void Test_Base_DeepRecursion()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, []);

            // Create deep structure
            string deepDir = Path.Combine(MainPath, "A", "B", "C");
            Directory.CreateDirectory(deepDir);
            CreateFile(deepDir, "deep.txt", "Hidden treasure");

            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);
            Assert(instructions.Count == 1, "Found deep file");
            Assert(instructions[0].Source.Contains("C"), "Path contains folders");

            // Round trip
            engine.ExecuteHomeTransfer(MainPath, UsbPath, instructions, MockReport);
            var offsiteInstr = engine.AnalyzeForOffsite(UsbPath);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, offsiteInstr, MockReport);

            Assert(File.Exists(Path.Combine(OffsitePath, "A", "B", "C", "deep.txt")), "Deep structure replicated");
        }

        // ========================================================================
        // SYNC LOGIC & EDGE CASES
        // ========================================================================

        static void Test_Sync_Swap()
        {
            var engine = new SyncEngine();

            // Setup Conflict: A->B, B->A
            CreateFile(OffsitePath, "A.txt", "ContentA");
            CreateFile(OffsitePath, "B.txt", "ContentB");

            CreateFile(MainPath, "B.txt", "ContentA"); // Moved A here
            CreateFile(MainPath, "A.txt", "ContentB"); // Moved B here

            // Explicit timestamps to differentiate files since sizes are same
            var t1 = DateTime.UtcNow.AddHours(-1);
            var t2 = DateTime.UtcNow;

            File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, "A.txt"), t1);
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "B.txt"), t1); // A moved to B

            File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, "B.txt"), t2);
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "A.txt"), t2); // B moved to A

            engine.GenerateCatalog(OffsitePath, UsbPath, []);
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            Assert(instructions.Count == 2, "2 Moves");
            Assert(instructions.All(x => x.Action == "MOVE"), "All moves");

            // Execute (this tests the temp file logic)
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, instructions, MockReport);

            Assert(File.ReadAllText(Path.Combine(OffsitePath, "A.txt")) == "ContentB", "A has B's content");
            Assert(File.ReadAllText(Path.Combine(OffsitePath, "B.txt")) == "ContentA", "B has A's content");
        }

        static void Test_Sync_FileReplacingFolder()
        {
            var engine = new SyncEngine();

            // Offsite: Folder "Data"
            Directory.CreateDirectory(Path.Combine(OffsitePath, "Data"));
            CreateFile(Path.Combine(OffsitePath, "Data"), "sub.txt", "hi");

            // Main: File "Data"
            if (Directory.Exists(Path.Combine(MainPath, "Data"))) Directory.Delete(Path.Combine(MainPath, "Data"), true);
            CreateFile(MainPath, "Data", "Im a file");

            engine.GenerateCatalog(OffsitePath, UsbPath, []);
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            // Should imply: Delete "Data/sub.txt", Copy "Data"
            // Note: The logic might generate a DELETE for sub.txt and a COPY for Data.

            engine.ExecuteHomeTransfer(MainPath, UsbPath, instructions, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, instructions, MockReport);

            Assert(File.Exists(Path.Combine(OffsitePath, "Data")), "Data is now a file");
            Assert(!Directory.Exists(Path.Combine(OffsitePath, "Data.dir")), "Folder is gone");
        }

        static void Test_Sync_ZeroByte()
        {
            var engine = new SyncEngine();
            CreateFile(MainPath, "zero.bin", "");
            engine.GenerateCatalog(OffsitePath, UsbPath, []);

            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);
            Assert(instructions.Count == 1, "Detects zero byte file");

            engine.ExecuteHomeTransfer(MainPath, UsbPath, instructions, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, instructions, MockReport);

            var fi = new FileInfo(Path.Combine(OffsitePath, "zero.bin"));
            Assert(fi.Exists && fi.Length == 0, "Zero byte transferred");
        }

        // ========================================================================
        // EXCLUSION TESTS
        // ========================================================================

        static void Test_Exclude_NewFile()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, []);

            CreateFile(MainPath, "good.txt", "keep");
            CreateFile(MainPath, "bad.tmp", "ignore");

            var exclusions = new List<string> { "*.tmp" };
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, exclusions, MockReport);

            Assert(instructions.Count == 1, "Should only see 1 file");
            Assert(instructions[0].Source == "good.txt", "Should be good.txt");
        }

        static void Test_Exclude_Folder()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, []);

            Directory.CreateDirectory(Path.Combine(MainPath, "bin"));
            CreateFile(Path.Combine(MainPath, "bin"), "exec.dll", "binary");
            CreateFile(MainPath, "src.cs", "code");

            var exclusions = new List<string> { "bin" };
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, exclusions, MockReport);

            Assert(instructions.Count == 1, "Ignores bin folder");
            Assert(instructions[0].Source == "src.cs", "Sees src.cs");
        }

        static void Test_Exclude_Retroactive()
        {
            var engine = new SyncEngine();

            // Setup: File DOES exist on Offsite (from a previous run)
            CreateFile(OffsitePath, "secret.log", "logs");
            CreateFile(MainPath, "secret.log", "logs");
            SyncTimestamps("secret.log");

            // Create catalog representing this state
            engine.GenerateCatalog(OffsitePath, UsbPath, []);

            // Now, User decides to exclude *.log
            var exclusions = new List<string> { "*.log" };

            // Analyze
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, exclusions, MockReport);

            // Expected: The scanner ignores "secret.log" on Main.
            // The comparator sees: Catalog has "secret.log", Main has "Nothing" (due to filter).
            // Result: DELETE "secret.log" from Offsite.

            Assert(instructions.Count == 1, "Should generate instruction");
            Assert(instructions[0].Action == "DELETE", "Should be DELETE");
            Assert(instructions[0].Source == "secret.log", "Target secret.log");
        }

        // ========================================================================
        // ROBUSTNESS
        // ========================================================================

        static void Test_Robust_LockedFile()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, []);

            string lockedPath = Path.Combine(MainPath, "locked.txt");
            CreateFile(MainPath, "locked.txt", "Can't touch this");

            // Lock the file
            using (FileStream fs = File.Open(lockedPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                // File is now locked by this process
                var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

                // Scanner might skip it or fail to read info. 
                // Since we use EnumerationOptions, we get basic info even if locked, usually.
                // But let's assume it gets queued for copy.

                if (instructions.Count > 0)
                {
                    // Attempt Copy - Should NOT crash
                    try
                    {
                        var res = engine.ExecuteHomeTransfer(MainPath, UsbPath, instructions, MockReport);
                        Assert(res.Errors > 0, "Should record an error");
                        Assert(res.FilesCopied == 0, "Should not copy locked file");
                    }
                    catch (Exception)
                    {
                        Assert(false, "Engine crashed on locked file!");
                    }
                }
            }
        }

        static void Test_InstructionFile_Cleanup()
        {
            var engine = new SyncEngine();

            // 1. Setup: Creates a dummy instructions.json on USB
            // This simulates a state where the user did an analysis at Home,
            // but then decided to Reset/Initialize at Offsite instead of applying updates.
            string instrPath = Path.Combine(UsbPath, "instructions.json");
            File.WriteAllText(instrPath, "[ { \"Action\": \"DELETE\", \"Source\": \"Critical.txt\" } ]");

            Assert(File.Exists(instrPath), "Instructions file setup failed");

            // 2. Action: Generate Catalog (Initialize)
            engine.GenerateCatalog(OffsitePath, UsbPath, null);

            // 3. Assert: Instructions file must be GONE. 
            // If it remains, the next "AnalyzeForOffsite" might execute it dangerously.
            Assert(!File.Exists(instrPath), "Instructions file was not deleted during Catalog generation!");
        }
        static void Test_Exclude_FolderVsFile()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);

            // 1. Setup:
            // Create a FOLDER named "Data" at the root. (This should be excluded)
            Directory.CreateDirectory(Path.Combine(MainPath, "Data"));
            CreateFile(Path.Combine(MainPath, "Data"), "ignore_me.txt", "garbage");

            // Create a FILE named "Data" inside a subfolder. (This should be kept)
            // We put it in a subfolder because we can't have a file and folder with the same name in the same root.
            Directory.CreateDirectory(Path.Combine(MainPath, "SafeZone"));
            CreateFile(Path.Combine(MainPath, "SafeZone"), "Data", "Keep me, I am a file");

            // 2. Define Exclusion: "Data\" 
            // The trailing slash means "Exclude folders named Data, but keep files named Data"
            var exclusions = new List<string> { "Data\\" };

            // 3. Analyze
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, exclusions, MockReport);

            // 4. Assert
            // We expect exactly 1 instruction: The COPY of "SafeZone\Data".
            // The folder "Main\Data" and its contents should be invisible to the engine.
            Assert(instructions.Count == 1, $"Expected 1 instruction, got {instructions.Count}");

            var item = instructions[0];
            Assert(item.Source.Contains("SafeZone") && item.Source.EndsWith("Data"),
                   $"Should have preserved the file 'SafeZone\\Data'. Instead found: {item.Source}");
        }


        // ========================================================================
        // HELPERS
        // ========================================================================

        static void CreateFile(string folder, string name, string content)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name), content);
        }

        static void SyncTimestamps(string relPath)
        {
            var t = DateTime.UtcNow;
            if (File.Exists(Path.Combine(MainPath, relPath)))
                File.SetLastWriteTimeUtc(Path.Combine(MainPath, relPath), t);
            if (File.Exists(Path.Combine(OffsitePath, relPath)))
                File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, relPath), t);
        }

        static void SetupDirs()
        {
            if (!Directory.Exists(MainPath)) Directory.CreateDirectory(MainPath);
            if (!Directory.Exists(OffsitePath)) Directory.CreateDirectory(OffsitePath);
            if (!Directory.Exists(UsbPath)) Directory.CreateDirectory(UsbPath);
        }

        static void Cleanup()
        {
            if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, true);
        }

        static void Assert(bool condition, string msg)
        {
            if (!condition) throw new Exception(msg);
        }

        static void MockReport(string msg, int pct) { }
    }
}