using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SneakerNetSync; // Ensure this matches your namespace

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
            Console.WriteLine("=== STARTING SNEAKERNET TEST SUITE ===\n");

            try
            {
                RunTest("1. Basic Copy (New File)", Test_BasicCopy);
                RunTest("2. Detect Deletion", Test_Deletion);
                RunTest("3. Detect Modification (Timestamp/Size)", Test_Modification);
                RunTest("4. Detect Rename (Move within folder)", Test_Rename);
                RunTest("5. Detect Move (Different folder)", Test_Move_DiffFolder);
                RunTest("6. Swap Conflict (A->B, B->A)", Test_Swap_Conflict);
                RunTest("7. USB Full / Partial Copy Recovery", Test_PartialCopy_Recovery);
                RunTest("8. 3-Way Move Cycle (A->B, B->C, C->A)", Test_3Way_Cycle);
                RunTest("9. File Replacing Folder", Test_File_Replacing_Folder);
                RunTest("10. Zero Byte File Handling", Test_ZeroByte_File);
                RunTest("11. Corrupt Instructions Handling", Test_Corrupt_Instructions);
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
            Console.Write($"{name.PadRight(50)} : ");
            Cleanup(); // Start fresh every time
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
            }
            Console.ResetColor();
        }

        // ========================================================================
        // TEST CASES
        // ========================================================================

        static void Test_BasicCopy()
        {
            var engine = new SyncEngine();

            // 1. Setup: File on Main, Empty Offsite
            CreateFile(MainPath, "hello.txt", "Content");

            // 2. Init Offsite (Produces empty catalog)
            engine.GenerateCatalog(OffsitePath, UsbPath);

            // 3. Analyze at Home
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            // Assert
            Assert(instructions.Count == 1, "Should have 1 instruction");
            Assert(instructions[0].Action == "COPY", "Action should be COPY");
            Assert(instructions[0].Source == "hello.txt", "Source should be hello.txt");
        }

        static void Test_Deletion()
        {
            var engine = new SyncEngine();

            // 1. Setup: File exists on Offsite, but deleted from Main
            CreateFile(OffsitePath, "old.txt", "Data");

            // 2. Init Offsite
            engine.GenerateCatalog(OffsitePath, UsbPath);

            // 3. Analyze at Home (Main is empty)
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            // Assert
            Assert(instructions.Count == 1, "Should have 1 instruction");
            Assert(instructions[0].Action == "DELETE", "Action should be DELETE");
            Assert(instructions[0].Source == "old.txt", "Source should be old.txt");
        }

        static void Test_Modification()
        {
            var engine = new SyncEngine();

            // 1. Setup: File exists on both, but Main is newer/larger
            CreateFile(OffsitePath, "doc.txt", "Old Data");
            // Sleep to ensure timestamp difference (FAT32 needs 2s, we force it manually)
            CreateFile(MainPath, "doc.txt", "New Data is longer");
            File.SetLastWriteTime(MainPath + "\\doc.txt", DateTime.Now.AddMinutes(10));

            // 2. Init Offsite
            engine.GenerateCatalog(OffsitePath, UsbPath);

            // 3. Analyze
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            // Assert
            Assert(instructions.Count == 1, "Should have 1 instruction");
            Assert(instructions[0].Action == "COPY", "Should be COPY (Overwrite)");
        }

        static void Test_Rename()
        {
            var engine = new SyncEngine();

            // 1. Setup: Offsite has 'Cat.jpg', Main has 'Dog.jpg' (Same content/size/date)
            CreateFile(OffsitePath, "Cat.jpg", "ImageContent");
            CreateFile(MainPath, "Dog.jpg", "ImageContent");

            // Sync timestamps exactly to simulate a rename
            var time = DateTime.Now;
            File.SetLastWriteTime(OffsitePath + "\\Cat.jpg", time);
            File.SetLastWriteTime(MainPath + "\\Dog.jpg", time);

            engine.GenerateCatalog(OffsitePath, UsbPath);
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            Assert(instructions.Count == 1, "Should be 1 instruction (Move)");
            Assert(instructions[0].Action == "MOVE", "Action should be MOVE");
            Assert(instructions[0].Source == "Cat.jpg", "Source should be Cat");
            Assert(instructions[0].Destination == "Dog.jpg", "Dest should be Dog");
        }

        static void Test_Move_DiffFolder()
        {
            var engine = new SyncEngine();

            Directory.CreateDirectory(Path.Combine(OffsitePath, "Photos"));
            CreateFile(Path.Combine(OffsitePath, "Photos"), "img.jpg", "Binary");

            Directory.CreateDirectory(Path.Combine(MainPath, "Archive"));
            CreateFile(Path.Combine(MainPath, "Archive"), "img.jpg", "Binary");

            // Sync Timestamps
            var t = DateTime.Now;
            File.SetLastWriteTime(Path.Combine(OffsitePath, "Photos", "img.jpg"), t);
            File.SetLastWriteTime(Path.Combine(MainPath, "Archive", "img.jpg"), t);

            engine.GenerateCatalog(OffsitePath, UsbPath);
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            Assert(instructions[0].Action == "MOVE", "Should detect MOVE across folders");
            // Note: Path separators might differ on OS, checking endsWith
            Assert(instructions[0].Source.EndsWith("img.jpg"), "Source check");
            Assert(instructions[0].Destination.Contains("Archive"), "Dest check");
        }

        static void Test_Swap_Conflict()
        {
            var engine = new SyncEngine();

            // Setup:
            // Offsite: A (Time 1), B (Time 2)
            // Main:    A (Time 2 - was B), B (Time 1 - was A)

            CreateFile(OffsitePath, "A.txt", "File A Content");
            CreateFile(OffsitePath, "B.txt", "File B Content");

            CreateFile(MainPath, "B.txt", "File A Content"); // Old A moved to B
            CreateFile(MainPath, "A.txt", "File B Content"); // Old B moved to A

            // CRITICAL: Use different timestamps so Fast Mode can tell them apart!
            var time1 = DateTime.Now.AddHours(-1);
            var time2 = DateTime.Now;

            // A was Time1, B was Time2.
            // Now: B is Time1, A is Time2.
            File.SetLastWriteTime(OffsitePath + "\\A.txt", time1);
            File.SetLastWriteTime(MainPath + "\\B.txt", time1); // A -> B (Carries Time1)

            File.SetLastWriteTime(OffsitePath + "\\B.txt", time2);
            File.SetLastWriteTime(MainPath + "\\A.txt", time2); // B -> A (Carries Time2)

            engine.GenerateCatalog(OffsitePath, UsbPath);
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            // Now we expect 2 moves
            Assert(instructions.Count == 2, $"Should have 2 instructions, got {instructions.Count}");
            Assert(instructions.All(i => i.Action == "MOVE"), "Both should be MOVE");

            // ... rest of the test (Execution check) remains the same ...
            // Save instructions to USB
            var json = System.Text.Json.JsonSerializer.Serialize(instructions);
            File.WriteAllText(Path.Combine(UsbPath, "instructions.json"), json);

            // Execute Apply
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, instructions, MockReport);

            // Verify Results
            string contentA = File.ReadAllText(Path.Combine(OffsitePath, "A.txt"));
            string contentB = File.ReadAllText(Path.Combine(OffsitePath, "B.txt"));

            Assert(contentA == "File B Content", "A.txt should contain B's old content");
            Assert(contentB == "File A Content", "B.txt should contain A's old content");
        }

        static void Test_PartialCopy_Recovery()
        {
            // This tests the "Disk Full" or "Lost File" scenario.
            // 1. Analyze says "Copy A and B".
            // 2. USB only successfully brings "A". "B" is missing/corrupt.
            // 3. Apply happens.
            // 4. New Catalog generated.
            // 5. Next Analysis should ask for "B" again.

            var engine = new SyncEngine();

            CreateFile(MainPath, "FileA.txt", "Data A");
            CreateFile(MainPath, "FileB.txt", "Data B");

            // Init Offsite (Empty)
            engine.GenerateCatalog(OffsitePath, UsbPath);

            // Analyze
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);
            Assert(instructions.Count == 2, "Should want to copy A and B");

            // EXECUTE HOME TRANSFER (Simulate normal copy)
            engine.ExecuteHomeTransfer(MainPath, UsbPath, instructions, MockReport);

            // SIMULATE DISASTER: Delete FileB from USB before applying to Offsite
            File.Delete(Path.Combine(UsbPath, "Data", "FileB.txt"));

            // EXECUTE OFFSITE UPDATE
            // This should copy A, fail on B (or skip it), and generate a catalog containing ONLY A.
            // We load instructions from file to mimic reality
            var instrsFromFile = engine.AnalyzeForOffsite(UsbPath);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, instrsFromFile, MockReport);

            // Verify Offsite State
            Assert(File.Exists(Path.Combine(OffsitePath, "FileA.txt")), "File A should exist");
            Assert(!File.Exists(Path.Combine(OffsitePath, "FileB.txt")), "File B should NOT exist");

            // NEXT TRIP: ANALYZE HOME AGAIN
            // The system should realize B is missing and ask for it again
            var nextInstructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            Assert(nextInstructions.Any(x => x.Source == "FileB.txt" && x.Action == "COPY"),
                "System should queue File B for copy again");
        }

        // --------------------------------------------------------
        // NEW ADVANCED TESTS
        // --------------------------------------------------------

        static void Test_3Way_Cycle()
        {
            // Scenario: A->B, B->C, C->A
            // Ensures the temp file logic handles >2 items without data loss.
            var engine = new SyncEngine();

            CreateFile(OffsitePath, "A.txt", "Content A");
            CreateFile(OffsitePath, "B.txt", "Content B");
            CreateFile(OffsitePath, "C.txt", "Content C");

            // Main has them rotated
            CreateFile(MainPath, "B.txt", "Content A"); // A moved to B
            CreateFile(MainPath, "C.txt", "Content B"); // B moved to C
            CreateFile(MainPath, "A.txt", "Content C"); // C moved to A

            // Set timestamps to force Move detection
            var t1 = DateTime.Now.AddHours(-1);
            var t2 = DateTime.Now.AddHours(-2);
            var t3 = DateTime.Now.AddHours(-3);

            // Offsite State
            File.SetLastWriteTime(OffsitePath + "\\A.txt", t1);
            File.SetLastWriteTime(OffsitePath + "\\B.txt", t2);
            File.SetLastWriteTime(OffsitePath + "\\C.txt", t3);

            // Main State (Rotated)
            File.SetLastWriteTime(MainPath + "\\B.txt", t1); // A->B
            File.SetLastWriteTime(MainPath + "\\C.txt", t2); // B->C
            File.SetLastWriteTime(MainPath + "\\A.txt", t3); // C->A

            engine.GenerateCatalog(OffsitePath, UsbPath);
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            Assert(instructions.Count == 3, "Should be 3 moves");
            Assert(instructions.All(i => i.Action == "MOVE"), "All should be MOVE");

            // Execute
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, instructions, MockReport);

            // Verify
            Assert(File.ReadAllText(Path.Combine(OffsitePath, "A.txt")) == "Content C", "A should have C content");
            Assert(File.ReadAllText(Path.Combine(OffsitePath, "B.txt")) == "Content A", "B should have A content");
            Assert(File.ReadAllText(Path.Combine(OffsitePath, "C.txt")) == "Content B", "C should have B content");
        }

        static void Test_File_Replacing_Folder()
        {
            // Scenario: Offsite has a FOLDER named "Data". 
            // Main has deleted that folder and created a FILE named "Data".
            // BAD SITUATION: File.Copy will fail if a Directory exists at the destination path.

            var engine = new SyncEngine();

            // Offsite: Folder "MyStuff" with file inside
            Directory.CreateDirectory(Path.Combine(OffsitePath, "MyStuff"));
            CreateFile(Path.Combine(OffsitePath, "MyStuff"), "inside.txt", "inner");

            // Main: File "MyStuff" (No extension, same name as folder)
            CreateFile(MainPath, "MyStuff", "I am a file now");

            // Init
            engine.GenerateCatalog(OffsitePath, UsbPath);
            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            // Expect: Delete "MyStuff/inside.txt" and Copy "MyStuff"
            engine.ExecuteHomeTransfer(MainPath, UsbPath, instructions, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, instructions, MockReport);

            // Verify
            Assert(File.Exists(Path.Combine(OffsitePath, "MyStuff")), "File 'MyStuff' should exist");
            Assert(!Directory.Exists(Path.Combine(OffsitePath, "MyStuff.dir")), "Directory should be gone"); // Checking if it's a file, not dir
        }

        static void Test_ZeroByte_File()
        {
            // Edge Case: 0 Byte files can sometimes be ignored by sloppy copy logic
            var engine = new SyncEngine();

            CreateFile(MainPath, "Zero.bin", ""); // Empty content
            engine.GenerateCatalog(OffsitePath, UsbPath);

            var instructions = engine.AnalyzeForHome(MainPath, UsbPath, [], MockReport);

            Assert(instructions.Count == 1, "Should detect new 0-byte file");

            engine.ExecuteHomeTransfer(MainPath, UsbPath, instructions, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, instructions, MockReport);

            var info = new FileInfo(Path.Combine(OffsitePath, "Zero.bin"));
            Assert(info.Exists && info.Length == 0, "Zero byte file should exist and be size 0");
        }

        static void Test_Corrupt_Instructions()
        {
            // Security/Stability: What if json is garbage?
            var engine = new SyncEngine();
            File.WriteAllText(Path.Combine(UsbPath, "instructions.json"), "{ BROKEN JSON ]");

            try
            {
                var list = engine.AnalyzeForOffsite(UsbPath);
                // Depending on implementation, might return null or empty, or throw.
                // If it throws, we catch it. If not, we assert it didn't crash app.
            }
            catch (System.Text.Json.JsonException)
            {
                // Expected behavior
                Console.WriteLine("caught expected json error");
                return;
            }
            catch (Exception ex)
            {
                Assert(false, $"Should not crash with unexpected error: {ex.GetType().Name}");
            }
        }

        // ========================================================================
        // HELPERS
        // ========================================================================

        static void CreateFile(string folder, string name, string content)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name), content);
        }

        static void SetupDirs()
        {
            Directory.CreateDirectory(MainPath);
            Directory.CreateDirectory(OffsitePath);
            Directory.CreateDirectory(UsbPath);
        }

        static void Cleanup()
        {
            if (Directory.Exists(TestRoot)) Directory.Delete(TestRoot, true);
        }

        static void Assert(bool condition, string msg)
        {
            if (!condition) throw new Exception(msg);
        }

        static void MockReport(string msg, int pct) { /* Silence is golden for tests */ }
    }
}