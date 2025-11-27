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
            Console.WriteLine("=== STARTING SNEAKERNET EXTENDED TEST SUITE ===\n");
            int passed = 0;
            int total = 0;

            void Run(string name, Action test)
            {
                total++;
                Console.Write($"{name.PadRight(65)} : ");
                Cleanup();
                SetupDirs();
                try
                {
                    test();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("PASS");
                    passed++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"FAIL");
                    Console.WriteLine($"   Error: {ex.Message}");
                    Console.WriteLine($"   Trace: {ex.StackTrace}");
                }
                Console.ResetColor();
            }

            // --- 1. BASIC CRUD ---
            Run("01. Base: Add New File", Test_Base_Add);
            Run("02. Base: Delete File", Test_Base_Delete);
            Run("03. Base: Update File Content", Test_Base_Update);
            Run("04. Base: No Changes Detected", Test_Base_NoChanges);
            Run("05. Base: Re-create File (Delete then New)", Test_Base_Recreate);

            // --- 2. MOVES & RENAMES ---
            Run("06. Move: Simple Rename", Test_Move_Simple);
            Run("07. Move: Move to Subfolder", Test_Move_ToSubfolder);
            Run("08. Move: Move to Parent", Test_Move_ToParent);
            Run("09. Move: Chain Move (A->B->C)", Test_Move_Chain);
            Run("10. Move: Swap Files (A<->B)", Test_Move_Swap);
            Run("11. Move: Case-Only Rename (file.txt -> FILE.TXT)", Test_Move_CaseOnly);
            Run("12. Move: Folder Rename (Implicit)", Test_Move_FolderRename);

            // --- 3. CONFLICTS & COMPLEX SYNC ---
            Run("13. Complex: Move A->B and Create New A", Test_Complex_MoveAndReplace);
            Run("14. Complex: Delete A and Create Folder A", Test_Complex_FileToFolder);
            Run("15. Complex: Delete Folder A and Create File A", Test_Complex_FolderToFile);
            Run("16. Complex: Update content and Move (A->B)", Test_Complex_EditAndMove);

            // --- 4. EXCLUSIONS (BASIC) ---
            Run("17. Exclude: Specific File (*.tmp)", Test_Exclude_FilePattern);
            Run("18. Exclude: Specific Folder (bin\\)", Test_Exclude_FolderPattern);
            Run("19. Exclude: Mixed Case Matches", Test_Exclude_CaseInsensitive);
            Run("20. Exclude: Retroactive (Existing excluded file deleted)", Test_Exclude_Retroactive);
            Run("21. Exclude: Anchoring (temp vs temporary)", Test_Exclude_Anchoring);
            Run("22. Exclude: Complex Wildcards (*test*.log)", Test_Exclude_ComplexWildcard);

            // --- 5. EXCLUSIONS (ADVANCED) ---
            Run("23. Exclude Adv: Multiple Rules Combined", Test_Exclude_Adv_Multiple);
            Run("24. Exclude Adv: Exact Relative Path (Sub/file.txt)", Test_Exclude_Adv_ExactPath);
            Run("25. Exclude Adv: Regex Special Chars (file[1].txt)", Test_Exclude_Adv_RegexChars);
            Run("26. Exclude Adv: Folder Wildcard vs File (Build*\\)", Test_Exclude_Adv_FolderWildcard);
            Run("27. Exclude Adv: Single Char Wildcard (?)", Test_Exclude_Adv_QuestionMark);
            Run("28. Exclude Adv: Deep Nested Folder (node_modules\\)", Test_Exclude_Adv_DeepFolder);
            Run("29. Exclude Adv: Alternative Separators (bin/)", Test_Exclude_Adv_AltSeparator);
            Run("30. Exclude Adv: Whitespace Handling", Test_Exclude_Adv_Whitespace);

            // --- 6. PATHS & ATTRIBUTES ---
            Run("31. Paths: Spaces in Filenames", Test_Paths_Spaces);
            Run("32. Paths: Unicode Characters (Emoji/Kanji)", Test_Paths_Unicode);
            Run("33. Paths: Deeply Nested Paths", Test_Paths_DeepRecursion);
            Run("34. Attr: Empty Files (Zero Bytes)", Test_Attr_ZeroByte);
            Run("35. Attr: Hidden Files (Should Sync)", Test_Attr_HiddenFile);
            Run("36. Attr: System Files (Should Skip)", Test_Attr_SystemFile);

            // --- 7. RESILIENCE & SAFETY ---
            Run("37. Safety: Missing Catalog (Throws)", Test_Safety_MissingCatalog);
            Run("38. Safety: Corrupt Catalog (Handled)", Test_Safety_CorruptCatalog);
            Run("39. Safety: Locked File Access", Test_Safety_LockedFile);
            Run("40. Safety: USB Instruction Cleanup", Test_Safety_InstructionCleanup);
            Run("41. Safety: Empty Directory Cleanup", Test_Safety_EmptyDirCleanup);

            // --- 8. INTEGRATION ---
            Run("42. Integration: Full Simulation", Test_Integration_FullScenario);

            Console.WriteLine($"\n=== COMPLETED: {passed}/{total} PASSED ===");
            Console.ReadLine();
        }

        // --- HELPER WRAPPERS ---
        static SyncEngine GetEngine() => new SyncEngine();
        static void MockReport(string m, int p) { }

        // --- 1. BASIC CRUD ---
        static void Test_Base_Add()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            CreateFile(MainPath, "new.txt", "content");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1 && inst[0].Action == "COPY", "Should be COPY");
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(File.Exists(Path.Combine(OffsitePath, "new.txt")), "File missing offsite");
        }
        static void Test_Base_Delete()
        {
            var engine = GetEngine();
            CreateFile(MainPath, "del.txt", "data");
            CreateFile(OffsitePath, "del.txt", "data");
            SyncTimestamps("del.txt");
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            File.Delete(Path.Combine(MainPath, "del.txt"));
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst[0].Action == "DELETE", "Should be DELETE");
        }
        static void Test_Base_Update()
        {
            var engine = GetEngine();
            CreateFile(MainPath, "doc.txt", "v2");
            CreateFile(OffsitePath, "doc.txt", "v1");
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "doc.txt"), DateTime.UtcNow.AddMinutes(10));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst[0].Action == "COPY", "Should be COPY");
        }
        static void Test_Base_NoChanges()
        {
            var engine = GetEngine();
            CreateFile(MainPath, "file.txt", "data");
            CreateFile(OffsitePath, "file.txt", "data");
            SyncTimestamps("file.txt");
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            Assert(engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport).Count == 0, "Should be 0 instructions");
        }
        static void Test_Base_Recreate()
        {
            var engine = GetEngine();
            CreateFile(OffsitePath, "data.txt", "old");
            CreateFile(MainPath, "data.txt", "new_longer");
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "data.txt"), DateTime.UtcNow.AddHours(1));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1 && inst[0].Action == "COPY", "Should be COPY overwrite");
        }

        // --- 2. MOVES & RENAMES ---
        static void Test_Move_Simple()
        {
            var engine = GetEngine();
            CreateFile(OffsitePath, "old.txt", "u"); CreateFile(MainPath, "new.txt", "u");
            SyncTimestampsTo(Path.Combine(MainPath, "new.txt"), Path.Combine(OffsitePath, "old.txt"));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1 && inst[0].Action == "MOVE" && inst[0].Destination == "new.txt", "Move failed");
        }
        static void Test_Move_ToSubfolder()
        {
            var engine = GetEngine();
            CreateFile(OffsitePath, "f.txt", "d"); Directory.CreateDirectory(Path.Combine(MainPath, "S")); CreateFile(Path.Combine(MainPath, "S"), "f.txt", "d");
            SyncTimestampsTo(Path.Combine(MainPath, "S", "f.txt"), Path.Combine(OffsitePath, "f.txt"));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst[0].Action == "MOVE" && inst[0].Destination == Path.Combine("S", "f.txt"), "Subfolder move failed");
        }
        static void Test_Move_ToParent()
        {
            var engine = GetEngine();
            Directory.CreateDirectory(Path.Combine(OffsitePath, "S")); CreateFile(Path.Combine(OffsitePath, "S"), "f.txt", "d"); CreateFile(MainPath, "f.txt", "d");
            SyncTimestampsTo(Path.Combine(MainPath, "f.txt"), Path.Combine(OffsitePath, "S", "f.txt"));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst[0].Action == "MOVE", "Parent move failed");
        }
        static void Test_Move_Chain()
        {
            var engine = GetEngine();
            CreateFile(OffsitePath, "A.txt", "d"); CreateFile(MainPath, "C.txt", "d");
            SyncTimestampsTo(Path.Combine(MainPath, "C.txt"), Path.Combine(OffsitePath, "A.txt"));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst[0].Action == "MOVE" && inst[0].Destination == "C.txt", "Chain move failed");
        }
        static void Test_Move_Swap()
        {
            var engine = GetEngine();
            // A has content "ContentA", B has content "ContentB"
            CreateFile(OffsitePath, "A.txt", "ContentA");
            CreateFile(OffsitePath, "B.txt", "ContentB");

            // Swap: Main B gets "ContentA", Main A gets "ContentB"
            CreateFile(MainPath, "B.txt", "ContentA");
            CreateFile(MainPath, "A.txt", "ContentB");

            var t1 = DateTime.UtcNow.AddMinutes(-10);
            var t2 = DateTime.UtcNow.AddMinutes(-5);

            // Track "ContentA" (Offsite A -> Main B)
            File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, "A.txt"), t1);
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "B.txt"), t1);

            // Track "ContentB" (Offsite B -> Main A)
            File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, "B.txt"), t2);
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "A.txt"), t2);

            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);

            Assert(inst.Count == 2 && inst.All(x => x.Action == "MOVE"), "Swap failed");
        }
        static void Test_Move_CaseOnly()
        {
            var engine = GetEngine();
            CreateFile(OffsitePath, "a.txt", "d"); CreateFile(MainPath, "A.txt", "d");
            SyncTimestampsTo(Path.Combine(MainPath, "A.txt"), Path.Combine(OffsitePath, "a.txt"));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst[0].Action == "MOVE", "Case move failed");
        }
        static void Test_Move_FolderRename()
        {
            var engine = GetEngine();
            Directory.CreateDirectory(Path.Combine(OffsitePath, "O")); CreateFile(Path.Combine(OffsitePath, "O"), "f.txt", "d");
            Directory.CreateDirectory(Path.Combine(MainPath, "N")); CreateFile(Path.Combine(MainPath, "N"), "f.txt", "d");
            SyncTimestampsTo(Path.Combine(MainPath, "N", "f.txt"), Path.Combine(OffsitePath, "O", "f.txt"));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            Assert(engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport)[0].Action == "MOVE", "Folder rename failed");
        }

        // --- 3. CONFLICTS ---
        static void Test_Complex_MoveAndReplace()
        {
            var engine = GetEngine();
            CreateFile(OffsitePath, "A.txt", "a");
            CreateFile(MainPath, "B.txt", "a"); CreateFile(MainPath, "A.txt", "new");
            SyncTimestampsTo(Path.Combine(MainPath, "B.txt"), Path.Combine(OffsitePath, "A.txt"));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Any(x => x.Action == "MOVE" && x.Source == "A.txt"), "Move A->B missing");
            Assert(inst.Any(x => x.Action == "COPY" && x.Source == "A.txt"), "Copy new A missing");
        }
        static void Test_Complex_FileToFolder()
        {
            var engine = GetEngine();
            CreateFile(OffsitePath, "D", "file_D_content");
            Directory.CreateDirectory(Path.Combine(MainPath, "D"));
            CreateFile(Path.Combine(MainPath, "D"), "c.txt", "child_content"); // Diff size/content
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Any(x => x.Action == "DELETE" && x.Source == "D"), "Delete file D missing");
            Assert(inst.Any(x => x.Action == "COPY" && x.Source.Contains("c.txt")), "Copy child missing");
        }
        static void Test_Complex_FolderToFile()
        {
            var engine = GetEngine();
            Directory.CreateDirectory(Path.Combine(OffsitePath, "D"));
            CreateFile(Path.Combine(OffsitePath, "D"), "c.txt", "child_content");
            if (Directory.Exists(Path.Combine(MainPath, "D"))) Directory.Delete(Path.Combine(MainPath, "D"), true);
            CreateFile(MainPath, "D", "file_D_content_longer"); // Diff size
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Any(x => x.Action == "DELETE"), "Delete child missing");
            Assert(inst.Any(x => x.Action == "COPY" && x.Source == "D"), "Copy file D missing");
        }
        static void Test_Complex_EditAndMove()
        {
            var engine = GetEngine();
            CreateFile(OffsitePath, "A.txt", "v1"); CreateFile(MainPath, "B.txt", "v1_edited_size_diff");
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Any(x => x.Action == "DELETE"), "Delete A missing");
            Assert(inst.Any(x => x.Action == "COPY"), "Copy B missing");
            Assert(!inst.Any(x => x.Action == "MOVE"), "Should not move");
        }

        // --- 4. EXCLUSIONS (BASIC) ---
        static void Test_Exclude_FilePattern()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            CreateFile(MainPath, "i.tmp", "x"); CreateFile(MainPath, "k.txt", "y");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, new List<string> { "*.tmp" }, MockReport);
            Assert(inst.Count == 1 && inst[0].Source == "k.txt", "Filter failed");
        }
        static void Test_Exclude_FolderPattern()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            Directory.CreateDirectory(Path.Combine(MainPath, "bin")); CreateFile(Path.Combine(MainPath, "bin"), "a.dll", "x");
            CreateFile(MainPath, "r.txt", "y");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, new List<string> { "bin\\" }, MockReport);
            Assert(inst.Count == 1 && inst[0].Source == "r.txt", "Folder exclude failed");
        }
        static void Test_Exclude_CaseInsensitive()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            CreateFile(MainPath, "I.LOG", "x");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, new List<string> { "*.log" }, MockReport);
            Assert(inst.Count == 0, "Case check failed");
        }
        static void Test_Exclude_Retroactive()
        {
            var engine = GetEngine();
            CreateFile(OffsitePath, "L.txt", "d"); CreateFile(MainPath, "L.txt", "d"); SyncTimestamps("L.txt");
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, new List<string> { "*.txt" }, MockReport);
            Assert(inst.Count == 1 && inst[0].Action == "DELETE", "Retroactive delete failed");
        }
        static void Test_Exclude_Anchoring()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            Directory.CreateDirectory(Path.Combine(MainPath, "temp")); CreateFile(Path.Combine(MainPath, "temp"), "j.txt", "x");
            Directory.CreateDirectory(Path.Combine(MainPath, "temporary")); CreateFile(Path.Combine(MainPath, "temporary"), "k.txt", "x");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, new List<string> { "temp\\" }, MockReport);
            Assert(inst.Any(x => x.Source.Contains("temporary")), "Anchoring failed (excluded 'temporary')");
            Assert(!inst.Any(x => x.Source.Contains("j.txt")), "Anchoring failed (included 'temp')");
        }
        static void Test_Exclude_ComplexWildcard()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            CreateFile(MainPath, "test_1.log", "x"); CreateFile(MainPath, "prod.log", "x");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, new List<string> { "*test*.log" }, MockReport);
            Assert(inst.Count == 1 && inst[0].Source == "prod.log", "Wildcard failed");
        }

        // --- 5. EXCLUSIONS (ADVANCED) ---

        static void Test_Exclude_Adv_Multiple()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            CreateFile(MainPath, "a.tmp", "x");
            CreateFile(MainPath, "b.log", "x");
            CreateFile(MainPath, "c.txt", "x");

            var rules = new List<string> { "*.tmp", "*.log" };
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, rules, MockReport);

            Assert(inst.Count == 1, "Should allow multiple exclusion rules");
            Assert(inst[0].Source == "c.txt", "Failed to keep c.txt or failed to exclude others");
        }

        static void Test_Exclude_Adv_ExactPath()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);

            Directory.CreateDirectory(Path.Combine(MainPath, "A"));
            Directory.CreateDirectory(Path.Combine(MainPath, "B"));

            CreateFile(Path.Combine(MainPath, "A"), "secret.txt", "x"); // Exclude
            CreateFile(Path.Combine(MainPath, "B"), "secret.txt", "x"); // Keep

            var rules = new List<string> { @"A\secret.txt" };
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, rules, MockReport);

            Assert(inst.Any(x => x.Source == @"B\secret.txt"), "Failed to keep B");
            Assert(!inst.Any(x => x.Source == @"A\secret.txt"), "Failed to exclude A");
        }

        static void Test_Exclude_Adv_RegexChars()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);

            CreateFile(MainPath, "file[1].txt", "x"); // Exclude this exact name
            CreateFile(MainPath, "file1.txt", "x");   // Keep this

            var rules = new List<string> { "file[1].txt" };
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, rules, MockReport);

            Assert(inst.Any(x => x.Source == "file1.txt"), "Should keep file1.txt");
            Assert(!inst.Any(x => x.Source == "file[1].txt"), "Should exclude file[1].txt");
        }

        static void Test_Exclude_Adv_FolderWildcard()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);

            // "Build*\" should exclude "BuildLogs\" folder but NOT "Builder.exe" file
            Directory.CreateDirectory(Path.Combine(MainPath, "BuildLogs"));
            CreateFile(Path.Combine(MainPath, "BuildLogs"), "log.txt", "x");
            CreateFile(MainPath, "Builder.exe", "x");

            var rules = new List<string> { "Build*\\" }; // Trailing slash = Directory Only
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, rules, MockReport);

            Assert(inst.Any(x => x.Source == "Builder.exe"), "Should keep Builder.exe");
            Assert(!inst.Any(x => x.Source.Contains("log.txt")), "Should exclude content of BuildLogs");
        }

        static void Test_Exclude_Adv_QuestionMark()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);

            CreateFile(MainPath, "file1.txt", "x");  // Matches file?.txt
            CreateFile(MainPath, "file12.txt", "x"); // Does not match

            var rules = new List<string> { "file?.txt" };
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, rules, MockReport);

            Assert(inst.Any(x => x.Source == "file12.txt"), "Should keep file12.txt");
            Assert(!inst.Any(x => x.Source == "file1.txt"), "Should exclude file1.txt");
        }

        static void Test_Exclude_Adv_DeepFolder()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);

            string deepPath = Path.Combine(MainPath, "Src", "App", "node_modules");
            Directory.CreateDirectory(deepPath);
            CreateFile(deepPath, "pkg.json", "x");

            string otherPath = Path.Combine(MainPath, "Src", "Other");
            Directory.CreateDirectory(otherPath);
            CreateFile(otherPath, "keep.txt", "x");

            var rules = new List<string> { "node_modules\\" };
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, rules, MockReport);

            Assert(inst.Any(x => x.Source.Contains("keep.txt")), "Should keep other files");
            Assert(!inst.Any(x => x.Source.Contains("pkg.json")), "Should exclude deep folder");
        }

        static void Test_Exclude_Adv_AltSeparator()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);

            Directory.CreateDirectory(Path.Combine(MainPath, "bin"));
            CreateFile(Path.Combine(MainPath, "bin"), "app.dll", "x");

            // User types "bin/" instead of "bin\"
            var rules = new List<string> { "bin/" };
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, rules, MockReport);

            Assert(inst.Count == 0, "Forward slash should still trigger Directory Exclusion logic");
        }

        static void Test_Exclude_Adv_Whitespace()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);

            CreateFile(MainPath, "temp.txt", "x");

            // User put spaces around the pattern
            var rules = new List<string> { "  *.txt  " };
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, rules, MockReport);

            Assert(inst.Count == 0, "Should exclude temp.txt (whitespace should be trimmed)");
        }

        // --- 6. PATHS & ATTRIBUTES ---
        static void Test_Paths_Spaces()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            CreateFile(MainPath, "File With Spaces.txt", "d");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(File.Exists(Path.Combine(OffsitePath, "File With Spaces.txt")), "Spaces failed");
        }
        static void Test_Paths_Unicode()
        {
            var engine = GetEngine();
            string n = "🚀.txt";
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            CreateFile(MainPath, n, "d");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(File.Exists(Path.Combine(OffsitePath, n)), "Unicode failed");
        }
        static void Test_Paths_DeepRecursion()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            string d = Path.Combine(MainPath, "A", "B", "C"); Directory.CreateDirectory(d); CreateFile(d, "f.txt", "x");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);
            Assert(File.Exists(Path.Combine(OffsitePath, "A", "B", "C", "f.txt")), "Deep path failed");
        }
        static void Test_Attr_ZeroByte()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            CreateFile(MainPath, "z.bin", "");
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1, "Zero byte failed");
        }
        static void Test_Attr_HiddenFile()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            string p = Path.Combine(MainPath, "h.txt"); CreateFile(MainPath, "h.txt", "x"); File.SetAttributes(p, FileAttributes.Hidden);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
            Assert(inst.Count == 1, "Hidden failed");
        }
        static void Test_Attr_SystemFile()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            string p = Path.Combine(MainPath, "s.sys"); CreateFile(MainPath, "s.sys", "x"); File.SetAttributes(p, FileAttributes.System);
            Assert(engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport).Count == 0, "System file failed");
        }

        // --- 7. SAFETY ---
        static void Test_Safety_MissingCatalog()
        {
            try { GetEngine().AnalyzeForHome(MainPath, UsbPath, null, MockReport); throw new Exception("X"); } catch (FileNotFoundException) { }
        }
        static void Test_Safety_CorruptCatalog()
        {
            var engine = GetEngine();
            File.WriteAllText(Path.Combine(UsbPath, "offsite_catalog.json"), "{bad}");
            CreateFile(MainPath, "f.txt", "x");
            Assert(engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport).Count > 0, "Corrupt catalog failed");
        }
        static void Test_Safety_LockedFile()
        {
            var engine = GetEngine();
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            string p = Path.Combine(MainPath, "l.txt"); CreateFile(MainPath, "l.txt", "x");
            using (var fs = File.Open(p, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);
                Assert(engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport).Errors > 0, "Locked file failed");
            }
        }
        static void Test_Safety_InstructionCleanup()
        {
            var engine = GetEngine();
            File.WriteAllText(Path.Combine(UsbPath, "instructions.json"), "x");
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            Assert(!File.Exists(Path.Combine(UsbPath, "instructions.json")), "Instruction cleanup failed");
        }
        static void Test_Safety_EmptyDirCleanup()
        {
            var engine = GetEngine();
            Directory.CreateDirectory(Path.Combine(OffsitePath, "E"));
            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, new List<UpdateInstruction>(), MockReport);
            Assert(!Directory.Exists(Path.Combine(OffsitePath, "E")), "Empty dir cleanup failed");
        }

        // --- 8. INTEGRATION ---
        static void Test_Integration_FullScenario()
        {
            var engine = GetEngine();
            // Use distinct content lengths to prevent ambiguous move matching.
            CreateFile(OffsitePath, "A.txt", "content_A_v1");
            CreateFile(OffsitePath, "B.txt", "content_B_delete_me_now"); // Distinct size (23 chars)
            CreateFile(Path.Combine(OffsitePath, "S"), "C.txt", "content_C_mov"); // Size 13 chars

            CreateFile(MainPath, "A.txt", "content_A_v2_UPDATED"); // 20 chars
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "A.txt"), DateTime.UtcNow.AddMinutes(1));

            // B deleted

            CreateFile(MainPath, "C_mv.txt", "content_C_mov"); // Matches Offsite C size/content
            var t = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(Path.Combine(MainPath, "C_mv.txt"), t);
            File.SetLastWriteTimeUtc(Path.Combine(OffsitePath, "S", "C.txt"), t);

            engine.GenerateCatalog(OffsitePath, UsbPath, null);
            var inst = engine.AnalyzeForHome(MainPath, UsbPath, null, MockReport);

            Assert(inst.Any(x => x.Action == "COPY" && x.Source == "A.txt"), "A missing");
            Assert(inst.Any(x => x.Action == "DELETE" && x.Source == "B.txt"), "B missing");
            Assert(inst.Any(x => x.Action == "MOVE" && x.Source == Path.Combine("S", "C.txt")), "C missing");

            engine.ExecuteHomeTransfer(MainPath, UsbPath, inst, MockReport);
            engine.ExecuteOffsiteUpdate(OffsitePath, UsbPath, engine.AnalyzeForOffsite(UsbPath), MockReport);

            Assert(File.ReadAllText(Path.Combine(OffsitePath, "A.txt")) == "content_A_v2_UPDATED", "A content");
            Assert(!File.Exists(Path.Combine(OffsitePath, "B.txt")), "B gone");
            Assert(File.Exists(Path.Combine(OffsitePath, "C_mv.txt")), "C moved");
            Assert(!Directory.Exists(Path.Combine(OffsitePath, "S")), "S gone");
        }

        // --- UTILS ---
        static void CreateFile(string d, string n, string c) { 
            if (!Directory.Exists(d)) Directory.CreateDirectory(d);
            File.WriteAllText(Path.Combine(d, n), c); 
        }
        static void SyncTimestamps(string p) 
        { 
            SyncTimestampsTo(Path.Combine(MainPath, p), Path.Combine(OffsitePath, p)); 
        }
        static void SyncTimestampsTo(string s, string d) 
        { 
            if (File.Exists(s) && File.Exists(d)) 
            { var t = DateTime.UtcNow; 
                File.SetLastWriteTimeUtc(s, t); 
                File.SetLastWriteTimeUtc(d, t); } 
        }
        static void SetupDirs() 
        { 
            if (!Directory.Exists(MainPath)) 
                Directory.CreateDirectory(MainPath); 
            if (!Directory.Exists(OffsitePath)) 
                Directory.CreateDirectory(OffsitePath); 
            if (!Directory.Exists(UsbPath))
                Directory.CreateDirectory(UsbPath); }

        static void Cleanup() 
        { 
            if (Directory.Exists(TestRoot)) 
            { 
                for (int i = 0; i < 3; i++) 
                { 
                    try 
                    {
                        Directory.Delete(TestRoot, true); 
                        break; 
                    } 
                    catch { System.Threading.Thread.Sleep(100); }
                } 
            } 
        }

        static void Assert(bool c, string m) 
        { 
            if (!c) 
                throw new Exception(m); 
        }
    }
}