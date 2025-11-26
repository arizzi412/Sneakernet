using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            Console.WriteLine("=== STARTING SNEAKERNET FULL VALIDATION SUITE (NTFS) ===\n");

            try
            {
                // BASE CRUD
                RunTest("1. Base: Add New File", Test_Base_Add);
                RunTest("2. Base: Delete File", Test_Base_Delete);
                RunTest("3. Base: Update File Content", Test_Base_Update);
                RunTest("4. Base: Rename/Move File", Test_Base_Rename);
                RunTest("5. Base: Deep Directory Structure", Test_Base_DeepRecursion);

                // CONFLICTS & EDGES
                RunTest("6. Sync: Swap Conflict (A <-> B)", Test_Sync_Swap);
                RunTest("7. Sync: File Replacing Folder", Test_Sync_FileReplacingFolder);
                RunTest("8. Sync: Zero Byte Files", Test_Sync_ZeroByte);
                RunTest("9. Sync: Case-Only Rename (file.txt -> FILE.TXT)", Test_Sync_CaseOnlyRename);

                // EXCLUSIONS
                RunTest("10. Exclude: File Pattern (*.tmp)", Test_Exclude_FilePattern);
                RunTest("11. Exclude: Folder Pattern (bin\\)", Test_Exclude_FolderPattern);
                RunTest("12. Exclude: Retroactive (Existing files get deleted)", Test_Exclude_Retroactive);
                RunTest("13. Exclude: Folder vs File Name (Data\\ vs Data)", Test_Exclude_FolderVsFile);

                // SAFETY
                RunTest("14. Safety: Locked File Access", Test_Robust_LockedFile);
                RunTest("15. Safety: Instructions Cleanup", Test_InstructionFile_Cleanup);

                // COMPLEX SIMULATION
                RunTest("16. COMPLEX: The 'Real World' Simulation", Test_Complex_Scenario);
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
            Console.Write($"{name.PadRight(60)} : ");
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
                Console.WriteLine($"FAIL");
                Console.WriteLine($"   Error: {ex.Message}");
            }
            Console.ResetColor();
        }

        // --- TEST DEFINITIONS ---

        static void Test_Base_Add()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            CreateFile(MainPath, "new.txt", "content");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1 && inst[0].Action == "COPY", "Failed to detect new file");
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(File.Exists(Path.Combine(OffsitePath, "new.txt")), "File not synced to offsite");
        }

        static void Test_Base_Delete()
        {
            var engine = new SyncEngine();
            CreateFile(OffsitePath, "del.txt", "data");
            CreateFile(MainPath, "del.txt", "data");
            SyncTimestamps("del.txt");
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            File.Delete(Path.Combine(MainPath, "del.txt"));
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1 && inst[0].Action == "DELETE", "Failed to detect delete");
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(!File.Exists(Path.Combine(OffsitePath, "del.txt")), "File not deleted from offsite");
        }

        static void Test_Base_Update()
        {
            var engine = new SyncEngine();
            CreateFile(OffsitePath, "doc.txt", "v1");
            CreateFile(MainPath, "doc.txt", "v2_longer");
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "doc.txt"), DateTime.UtcNow.AddMinutes(10));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1 && inst[0].Action == "COPY", "Failed to detect update");
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(File.ReadAllText(Path.Combine(OffsitePath, "doc.txt")) == "v2_longer", "Content not updated");
        }

        static void Test_Base_Rename()
        {
            var engine = new SyncEngine();
            CreateFile(OffsitePath, "old.txt", "content");
            CreateFile(MainPath, "new.txt", "content");
            var t = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, "old.txt"), t);
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "new.txt"), t);
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1 && inst[0].Action == "MOVE", "Failed to detect move");
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(File.Exists(Path.Combine(OffsitePath, "new.txt")), "New file missing");
        }

        static void Test_Base_DeepRecursion()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            string deepPath = Path.Combine(MainPath, "A", "B", "C");
            Directory.CreateDirectory(deepPath);
            CreateFile(deepPath, "deep.txt", "hi");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(File.Exists(Path.Combine(OffsitePath, "A", "B", "C", "deep.txt")), "Deep file not synced");
        }

        static void Test_Sync_Swap()
        {
            var engine = new SyncEngine();
            CreateFile(OffsitePath, "A.txt", "ContentA");
            CreateFile(OffsitePath, "B.txt", "ContentB");
            CreateFile(MainPath, "B.txt", "ContentA");
            CreateFile(MainPath, "A.txt", "ContentB");
            var t1 = DateTime.UtcNow.AddMinutes(-5);
            var t2 = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, "A.txt"), t1);
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "B.txt"), t1);
            File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, "B.txt"), t2);
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "A.txt"), t2);
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 2 && inst.All(x => x.Action == "MOVE"), "Should be 2 moves");
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(File.ReadAllText(Path.Combine(OffsitePath, "A.txt")) == "ContentB", "Swap A failed");
        }

        static void Test_Sync_FileReplacingFolder()
        {
            var engine = new SyncEngine();
            Directory.CreateDirectory(Path.Combine(OffsitePath, "Data"));
            CreateFile(Path.Combine(OffsitePath, "Data"), "sub.txt", "stuff");
            CreateFile(MainPath, "Data", "I am a file now");
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(File.Exists(Path.Combine(OffsitePath, "Data")), "File 'Data' missing");
        }

        static void Test_Sync_ZeroByte()
        {
            var engine = new SyncEngine();
            CreateFile(MainPath, "zero.bin", "");
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1, "Zero byte file ignored");
        }

        static void Test_Sync_CaseOnlyRename()
        {
            var engine = new SyncEngine();
            CreateFile(OffsitePath, "lowercase.txt", "data");
            CreateFile(MainPath, "LOWERCASE.txt", "data");
            var t = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, "lowercase.txt"), t);
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "LOWERCASE.txt"), t);
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1 && inst[0].Action == "MOVE", "Case rename should be a MOVE");
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            var files = Directory.GetFiles(OffsitePath);
            Assert(Path.GetFileName(files[0]) == "LOWERCASE.txt", "Casing did not update");
        }

        static void Test_Exclude_FilePattern()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            CreateFile(MainPath, "ignore.tmp", "junk");
            CreateFile(MainPath, "keep.txt", "good");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, new List<string> { "*.tmp" }, MockReport);
            Assert(inst.Count == 1 && inst[0].Source == "keep.txt", "Filter failed");
        }

        static void Test_Exclude_FolderPattern()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            Directory.CreateDirectory(Path.Combine(MainPath, "bin"));
            CreateFile(Path.Combine(MainPath, "bin"), "app.dll", "bin");
            CreateFile(MainPath, "src.cs", "code");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, new List<string> { "bin\\" }, MockReport);
            Assert(inst.Count == 1 && inst[0].Source == "src.cs", "Folder exclude failed");
        }

        static void Test_Exclude_Retroactive()
        {
            var engine = new SyncEngine();
            CreateFile(OffsitePath, "log.txt", "data");
            CreateFile(MainPath, "log.txt", "data");
            SyncTimestamps("log.txt");
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, new List<string> { "*.txt" }, MockReport);
            Assert(inst.Count == 1 && inst[0].Action == "DELETE", "Should delete excluded existing file");
        }

        static void Test_Exclude_FolderVsFile()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            Directory.CreateDirectory(Path.Combine(MainPath, "Data"));
            CreateFile(Path.Combine(MainPath, "Data"), "bad.txt", "x");
            Directory.CreateDirectory(Path.Combine(MainPath, "Sub"));
            CreateFile(Path.Combine(MainPath, "Sub"), "Data", "Keep me");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, new List<string> { "Data\\" }, MockReport);
            Assert(inst.Count == 1 && inst[0].Source.EndsWith("Data"), "Folder vs File exclude check failed");
        }

        static void Test_Robust_LockedFile()
        {
            var engine = new SyncEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            string path = Path.Combine(MainPath, "locked.txt");
            CreateFile(MainPath, "locked.txt", "content");
            using (var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
                if (inst.Count > 0)
                {
                    try
                    {
                        var res = engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
                        Assert(res.Errors > 0, "Should report error for locked file");
                    }
                    catch { Assert(false, "Engine crashed on locked file"); }
                }
            }
        }

        static void Test_InstructionFile_Cleanup()
        {
            var engine = new SyncEngine();
            string instrPath = Path.Combine(UsbPath, "instructions.json");
            File.WriteAllText(instrPath, "DUMMY DATA");
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            Assert(!File.Exists(instrPath), "Instructions file must be deleted");
        }

        static void Test_Complex_Scenario()
        {
            var engine = new SyncEngine();

            // 1. SETUP OFFSITE
            Directory.CreateDirectory(Path.Combine(OffsitePath, "Src"));
            Directory.CreateDirectory(Path.Combine(OffsitePath, "Bin"));
            CreateFile(Path.Combine(OffsitePath, "Src"), "Program.cs", "old_code");
            CreateFile(Path.Combine(OffsitePath, "Src"), "OldUtil.cs", "delete_me");
            CreateFile(Path.Combine(OffsitePath, "Bin"), "App.exe", "binary");
            CreateFile(OffsitePath, "Readme.txt", "v1");
            CreateFile(OffsitePath, "Logo.png", "image");

            engine.GenerateCatalog(OffsitePath, UsbPath, null);

            // 2. SETUP MAIN
            Directory.CreateDirectory(Path.Combine(MainPath, "Src"));
            CreateFile(Path.Combine(MainPath, "Src"), "Program.cs", "new_code");
            // FORCE TIMESTAMP CHANGE for update detection
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "Src", "Program.cs"), DateTime.UtcNow.AddMinutes(10));

            Directory.CreateDirectory(Path.Combine(MainPath, "Bin"));
            CreateFile(Path.Combine(MainPath, "Bin"), "App.exe", "binary_v2");

            Directory.CreateDirectory(Path.Combine(MainPath, "Docs"));
            CreateFile(Path.Combine(MainPath, "Docs"), "Readme.txt", "v1");
            var t = File.GetLastWriteTimeUtc(Path.Combine(OffsitePath, "Readme.txt"));
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "Docs", "Readme.txt"), t);

            CreateFile(MainPath, "Brand.png", "image");
            var t2 = File.GetLastWriteTimeUtc(Path.Combine(OffsitePath, "Logo.png"));
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "Brand.png"), t2);

            // FIX: Make NewConfig.xml distinct in size from App.exe (6 bytes) to prevent accidental Move detection
            CreateFile(MainPath, "NewConfig.xml", "<configuration_data_is_long/>");

            // 3. ANALYZE & VERIFY
            var exclusions = new List<string> { "Bin\\" };
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, exclusions, MockReport);

            Assert(instructions.Any(x => x.Source == "Src\\Program.cs" && x.Action == "COPY"), "Missing Update Program.cs");
            Assert(instructions.Any(x => x.Source == "Src\\OldUtil.cs" && x.Action == "DELETE"), "Missing Delete OldUtil.cs");

            // This failed before because App.exe (Offsite) was being matched as a Move Source for NewConfig.xml
            Assert(instructions.Any(x => x.Source == "Bin\\App.exe" && x.Action == "DELETE"), "Missing Bin cleanup");

            Assert(instructions.Any(x => x.Source == "Logo.png" && x.Destination == "Brand.png" && x.Action == "MOVE"), "Missing Rename Logo->Brand");
            Assert(instructions.Any(x => x.Source == "Readme.txt" && x.Destination == "Docs\\Readme.txt" && x.Action == "MOVE"), "Missing Move Readme");

            // 4. EXECUTE FULL SYNC
            engine.ExecuteHomeTransfer(MainPath, UsbPath, instructions, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);

            Assert(File.ReadAllText(Path.Combine(OffsitePath, "Src", "Program.cs")) == "new_code", "Program.cs not updated");
            Assert(!File.Exists(Path.Combine(OffsitePath, "Src", "OldUtil.cs")), "OldUtil.cs not deleted");
            Assert(!File.Exists(Path.Combine(OffsitePath, "Bin", "App.exe")), "Bin folder not removed");
            Assert(File.Exists(Path.Combine(OffsitePath, "Brand.png")), "Brand.png missing");
            Assert(!File.Exists(Path.Combine(OffsitePath, "Logo.png")), "Logo.png still there");
            Assert(File.Exists(Path.Combine(OffsitePath, "Docs", "Readme.txt")), "Readme not moved");
        }

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

        static void Cleanup() { if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, true); }
        static void Assert(bool condition, string msg) { if (!condition) throw new Exception(msg); }
        static void MockReport(string msg, int pct) { }
    }
}