using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SneakerNetSync;

namespace GUI
{
    public partial class Form1 : Form
    {
        private readonly SyncEngine _engine;
        private List<UpdateInstruction> _pendingInstructions;
        private AppSettings _settings;

        private int _sortColumnIndex = -1;
        private SortOrder _sortOrder = SortOrder.None;

        public Form1()
        {
            InitializeComponent();
            _engine = new SyncEngine();
            _settings = AppSettings.Load();
            ApplySettingsToUi();
            SetupGrid();
            GenerateHelpText(); // Generate nicely formatted text

            // Hook up FormClosing to save settings
            this.FormClosing += (s, e) => SaveSettingsFromUi();
            this.tabControls.SelectedIndexChanged += TabControls_SelectedIndexChanged;
            UpdateLayoutForTab();
        }

        private void GenerateHelpText()
        {
            rtbHelp.Clear();

            void AddSection(string header, string desc, Dictionary<string, string> examples)
            {
                // Header
                rtbHelp.SelectionFont = new Font("Segoe UI", 11, FontStyle.Bold);
                rtbHelp.SelectionColor = Color.DarkSlateBlue;
                rtbHelp.AppendText(header.ToUpper() + Environment.NewLine);

                // Description
                rtbHelp.SelectionFont = new Font("Segoe UI", 9, FontStyle.Regular);
                rtbHelp.SelectionColor = Color.Black;
                rtbHelp.AppendText(desc + Environment.NewLine + Environment.NewLine);

                // Examples
                foreach (var kvp in examples)
                {
                    rtbHelp.SelectionFont = new Font("Consolas", 9.5f, FontStyle.Bold);
                    rtbHelp.SelectionColor = Color.FromArgb(40, 40, 40); // Dark Gray
                    rtbHelp.AppendText("  " + kvp.Key.PadRight(18));

                    rtbHelp.SelectionFont = new Font("Segoe UI", 9, FontStyle.Italic);
                    rtbHelp.SelectionColor = Color.DimGray;
                    rtbHelp.AppendText("// " + kvp.Value + Environment.NewLine);
                }
                rtbHelp.AppendText(Environment.NewLine);
            }

            AddSection("1. Recursive Matches (Everywhere)",
                "Use ** to match files or folders at any depth (subfolders included).",
                new Dictionary<string, string> {
                    { "**/*.tmp", "Excludes .tmp files in ANY folder" },
                    { "**/*.log", "Excludes .log files anywhere" },
                    { "**/thumbs.db", "Excludes specific file anywhere" },
                    { "**/node_modules/", "Excludes 'node_modules' folders anywhere" }
                });

            AddSection("2. Root Only (Top Level)",
                "Patterns starting with / or lacking ** match only in the main folder.",
                new Dictionary<string, string> {
                    { "/Build/", "Excludes 'Build' folder in root only" },
                    { "*.iso", "Excludes .iso files in root only" },
                    { "config.ini", "Excludes 'config.ini' in root only" }
                });

            AddSection("3. Specific Folders",
                "End pattern with / to ensure it only matches directories.",
                new Dictionary<string, string> {
                    { "**/bin/", "Excludes 'bin' folders and contents" },
                    { "**/obj/", "Excludes 'obj' folders and contents" },
                    { "**/.git/", "Excludes git history folder" }
                });

            AddSection("4. Wildcards",
                "Special characters you can use:",
                new Dictionary<string, string> {
                    { "*", "Matches zero or more characters" },
                    { "**", "Matches any directory depth" },
                    { "?", "Matches exactly one character" }
                });
        }

        private void TabControls_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateLayoutForTab();
        }

        private void UpdateLayoutForTab()
        {
            int statusHeight = progressBar1.Height + lblStatus.Height + 10;
            int compactTabHeight = 220;

            if (tabControls.SelectedTab == tabSettings)
            {
                // Settings Mode: Hide Grid, Maximize Tab Area
                gridChanges.Visible = false;
                tabControls.Dock = DockStyle.Fill;
                tabControls.BringToFront();
            }
            else
            {
                // Operation Mode: Show Grid, Shrink Tab Area
                tabControls.Dock = DockStyle.Top;
                tabControls.Height = compactTabHeight;

                gridChanges.Visible = true;
                gridChanges.Top = compactTabHeight + 5;
                gridChanges.Left = 0;
                gridChanges.Width = this.ClientSize.Width;
                gridChanges.Height = this.ClientSize.Height - gridChanges.Top - statusHeight;
                gridChanges.BringToFront();
            }
        }

        private void ApplySettingsToUi()
        {
            txtHomeMain.Text = _settings.HomeMainPath;
            txtHomeUsb.Text = _settings.HomeUsbPath;
            txtOffsiteTarget.Text = _settings.OffsiteTargetPath;
            txtOffsiteUsb.Text = _settings.OffsiteUsbPath;
            txtExclusions.Text = string.Join(Environment.NewLine, _settings.ExclusionPatterns);
        }

        private void SaveSettingsFromUi()
        {
            _settings.HomeMainPath = txtHomeMain.Text;
            _settings.HomeUsbPath = txtHomeUsb.Text;
            _settings.OffsiteTargetPath = txtOffsiteTarget.Text;
            _settings.OffsiteUsbPath = txtOffsiteUsb.Text;

            // This reads the lines from the left box, cleans them, and saves to JSON
            _settings.ExclusionPatterns = txtExclusions.Lines
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
            _settings.Save();
        }

        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            SaveSettingsFromUi();
            MessageBox.Show("Settings saved.", "SneakerNet", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ... [Rest of the file remains exactly as provided in the previous step] ...
        // (SetupGrid, Grid events, Home/Offsite logic, RunTask, etc)

        private void SetupGrid()
        {
            gridChanges.AutoGenerateColumns = false;
            gridChanges.Columns.Clear();
            gridChanges.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Action", DataPropertyName = "Action", Width = 70, SortMode = DataGridViewColumnSortMode.Programmatic });
            gridChanges.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File / Source", DataPropertyName = "Source", Width = 280, SortMode = DataGridViewColumnSortMode.Programmatic });
            gridChanges.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Destination", DataPropertyName = "Destination", Width = 280, SortMode = DataGridViewColumnSortMode.Programmatic });
            gridChanges.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", DataPropertyName = "SizeInfo", Width = 80, SortMode = DataGridViewColumnSortMode.Programmatic });

            gridChanges.CellFormatting += (s, e) => {
                if (e.RowIndex < 0 || e.RowIndex >= gridChanges.Rows.Count) return;
                var item = gridChanges.Rows[e.RowIndex].DataBoundItem as UpdateInstruction;
                if (item == null) return;
                if (item.Action == "DELETE") e.CellStyle.BackColor = Color.FromArgb(255, 235, 235);
                else if (item.Action == "COPY") e.CellStyle.BackColor = Color.FromArgb(235, 255, 235);
                else if (item.Action == "MOVE") e.CellStyle.BackColor = Color.FromArgb(235, 245, 255);
            };
            gridChanges.ColumnHeaderMouseClick += GridChanges_ColumnHeaderMouseClick;
        }

        private void GridChanges_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (_pendingInstructions == null || !_pendingInstructions.Any()) return;
            if (_sortColumnIndex == e.ColumnIndex) _sortOrder = _sortOrder == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            else { _sortColumnIndex = e.ColumnIndex; _sortOrder = SortOrder.Ascending; }

            var propName = gridChanges.Columns[e.ColumnIndex].DataPropertyName;
            IEnumerable<UpdateInstruction> sorted = null;
            Func<UpdateInstruction, object> keySelector = propName switch
            {
                "Action" => x => x.Action,
                "Source" => x => x.Source,
                "Destination" => x => x.Destination,
                "SizeInfo" => x => x.RawSizeBytes,
                _ => x => x.Source
            };

            if (_sortOrder == SortOrder.Ascending) sorted = _pendingInstructions.OrderBy(keySelector);
            else sorted = _pendingInstructions.OrderByDescending(keySelector);

            gridChanges.DataSource = sorted.ToList();
            foreach (DataGridViewColumn col in gridChanges.Columns) col.HeaderCell.SortGlyphDirection = SortOrder.None;
            gridChanges.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection = _sortOrder;
        }

        private async void btnHomeAnalyze_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtHomeMain.Text) || string.IsNullOrWhiteSpace(txtHomeUsb.Text)) { MessageBox.Show("Please select both Main and USB paths."); return; }
            SaveSettingsFromUi();
            _pendingInstructions = null;
            BindGrid();
            await RunTask("Analysing drives...", reporter => { _pendingInstructions = _engine.AnalyzeForHome(txtHomeMain.Text, txtHomeUsb.Text, _settings.ExclusionPatterns, reporter); });
            BindGrid();
            if (_pendingInstructions != null && _pendingInstructions.Any())
            {
                btnHomeExecute.Enabled = true;
                lblStatus.Text = $"Ready: {_pendingInstructions.Count(x => x.Action == "COPY")} copies, {_pendingInstructions.Count(x => x.Action == "MOVE")} moves, {_pendingInstructions.Count(x => x.Action == "DELETE")} deletes.";
                MessageBox.Show($"Analysis Complete.\n\nChanges Found: {_pendingInstructions.Count}\n{lblStats.Text}\n\nReview the list, then click 'Step 2' to copy files to USB.", "Analysis Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                lblStatus.Text = "All synced! No changes detected.";
                MessageBox.Show("Your drives are already in sync.", "Synced", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void btnHomeExecute_Click(object sender, EventArgs e)
        {
            if (_pendingInstructions == null || !_pendingInstructions.Any()) return;
            SyncResult result = null;
            await RunTask("Syncing files to USB...", reporter => { var listToProcess = (List<UpdateInstruction>)gridChanges.DataSource ?? _pendingInstructions; result = _engine.ExecuteHomeTransfer(txtHomeMain.Text, txtHomeUsb.Text, listToProcess, reporter); });
            btnHomeExecute.Enabled = false;
            _pendingInstructions = null;
            BindGrid();
            ShowSummary(result, "USB Transfer Complete");
            lblStatus.Text = "Transfer finished. Safe to eject USB.";
        }

        private async void btnOffsiteAnalyze_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOffsiteUsb.Text)) { MessageBox.Show("Please select the USB drive."); return; }
            _pendingInstructions = null;
            btnOffsiteExecute.Enabled = false;
            BindGrid();
            await RunTask("Reading USB instructions...", reporter => { _pendingInstructions = _engine.AnalyzeForOffsite(txtOffsiteUsb.Text); reporter("Done.", 100); });
            BindGrid();
            if (_pendingInstructions != null && _pendingInstructions.Any()) { btnOffsiteExecute.Enabled = true; lblStatus.Text = $"Pending: {_pendingInstructions.Count} updates from USB."; }
            else { lblStatus.Text = "No pending instructions found on USB."; }
        }

        private async void btnOffsiteExecute_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOffsiteTarget.Text)) { MessageBox.Show("Please select the Offsite Backup folder."); return; }
            SyncResult result = null;
            await RunTask("Applying updates to Backup Drive...", reporter => { var listToProcess = (List<UpdateInstruction>)gridChanges.DataSource ?? _pendingInstructions; result = _engine.ExecuteOffsiteUpdate(txtOffsiteTarget.Text, txtOffsiteUsb.Text, listToProcess, reporter); });
            btnOffsiteExecute.Enabled = false;
            _pendingInstructions = null;
            BindGrid();
            ShowSummary(result, "Offsite Update Complete");
            lblStatus.Text = "Success! Catalog refreshed. USB ready to go home.";
        }

        private async void btnInit_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOffsiteTarget.Text) || string.IsNullOrWhiteSpace(txtOffsiteUsb.Text)) return;
            if (MessageBox.Show("This will scan the Backup drive and create a fresh catalog on the USB.\nDo this if it's your first run or if things seem out of sync.\n\nContinue?", "Initialize", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                await RunTask("Generating Catalog...", reporter => _engine.GenerateCatalog(txtOffsiteTarget.Text, txtOffsiteUsb.Text));
                lblStatus.Text = "Initialization Complete. USB is ready to go Home.";
                MessageBox.Show("Catalog created successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BindGrid()
        {
            gridChanges.DataSource = null;
            _sortColumnIndex = -1;
            if (_pendingInstructions == null) { lblStats.Text = ""; return; }
            gridChanges.DataSource = _pendingInstructions;
            if (_pendingInstructions.Any()) { long totalSize = _pendingInstructions.Sum(x => x.RawSizeBytes); lblStats.Text = $"Data to Transfer: {totalSize / 1024.0 / 1024.0:F2} MB"; }
            else { lblStats.Text = "No changes."; }
        }

        private void ShowSummary(SyncResult res, string title)
        {
            if (res == null) return;
            string msg = $"Files Copied: {res.FilesCopied}\nFiles Moved: {res.FilesMoved}\nFiles Deleted: {res.FilesDeleted}\n";
            msg += $"Data Transferred: {res.BytesTransferred / 1024.0 / 1024.0:F2} MB\n";
            if (res.Errors > 0) msg += $"\nERRORS ENCOUNTERED: {res.Errors} (Check logs)";
            MessageBox.Show(msg, title, MessageBoxButtons.OK, res.Errors > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private async Task RunTask(string startMsg, Action<ProgressReporter> action)
        {
            EnableUi(false);
            lblStatus.Text = startMsg;
            progressBar1.Value = 0;
            progressBar1.Style = ProgressBarStyle.Marquee;
            progressBar1.MarqueeAnimationSpeed = 30;
            ProgressReporter reporter = (msg, pct) => Invoke((MethodInvoker)(() => {
                lblStatus.Text = msg;
                if (pct >= 0) { if (progressBar1.Style != ProgressBarStyle.Blocks) progressBar1.Style = ProgressBarStyle.Blocks; progressBar1.Value = Math.Min(pct, 100); }
            }));
            try { await Task.Run(() => action(reporter)); }
            catch (Exception ex) { MessageBox.Show($"Operation Failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { EnableUi(true); progressBar1.Style = ProgressBarStyle.Blocks; progressBar1.Value = 0; }
        }

        private void EnableUi(bool en) { tabControls.Enabled = en; Cursor = en ? Cursors.Default : Cursors.WaitCursor; }
        private string Pick() { using var d = new FolderBrowserDialog(); return d.ShowDialog() == DialogResult.OK ? d.SelectedPath : ""; }
        private void btnBrowseHomeMain_Click(object sender, EventArgs e) => txtHomeMain.Text = Pick();
        private void btnBrowseHomeUsb_Click(object sender, EventArgs e) => txtHomeUsb.Text = Pick();
        private void btnBrowseOffsiteTarget_Click(object sender, EventArgs e) => txtOffsiteTarget.Text = Pick();
        private void btnBrowseOffsiteUsb_Click(object sender, EventArgs e) => txtOffsiteUsb.Text = Pick();
    }
}