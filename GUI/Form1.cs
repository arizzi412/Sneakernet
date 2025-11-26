using System;
using System.Collections.Generic;
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

        public Form1()
        {
            InitializeComponent();
            _engine = new SyncEngine();
            _settings = AppSettings.Load();
            ApplySettingsToUi();
            SetupGrid();

            // Hook up FormClosing to save settings
            this.FormClosing += (s, e) => SaveSettingsFromUi();
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

        private void SetupGrid()
        {
            gridChanges.AutoGenerateColumns = false;
            gridChanges.Columns.Clear();
            gridChanges.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Action", DataPropertyName = "Action", Width = 70 });
            gridChanges.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "File / Source", DataPropertyName = "Source", Width = 280 });
            gridChanges.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Destination", DataPropertyName = "Destination", Width = 280 });
            gridChanges.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", DataPropertyName = "SizeInfo", Width = 80 });

            gridChanges.CellFormatting += (s, e) => {
                if (e.RowIndex < 0 || _pendingInstructions == null || e.RowIndex >= _pendingInstructions.Count) return;
                var item = _pendingInstructions[e.RowIndex];
                if (item.Action == "DELETE") e.CellStyle.BackColor = Color.FromArgb(255, 235, 235); // Light Red
                else if (item.Action == "COPY") e.CellStyle.BackColor = Color.FromArgb(235, 255, 235); // Light Green
                else if (item.Action == "MOVE") e.CellStyle.BackColor = Color.FromArgb(235, 245, 255); // Light Blue
            };
        }

        // --- HOME TAB ---

        private async void btnHomeAnalyze_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtHomeMain.Text) || string.IsNullOrWhiteSpace(txtHomeUsb.Text))
            {
                MessageBox.Show("Please select both Main and USB paths."); return;
            }

            // Ensure settings are up to date with UI (for exclusions)
            SaveSettingsFromUi();

            _pendingInstructions = null;
            BindGrid();

            await RunTask("Analysing drives... (This is fast)", reporter =>
            {
                _pendingInstructions = _engine.AnalyzeForHome(txtHomeMain.Text, txtHomeUsb.Text, _settings.ExclusionPatterns, reporter);
            });

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
                MessageBox.Show("Your drives are already in sync. There is nothing to copy.", "Synced", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void btnHomeExecute_Click(object sender, EventArgs e)
        {
            if (_pendingInstructions == null || !_pendingInstructions.Any()) return;

            SyncResult result = null;
            await RunTask("Syncing files to USB...", reporter =>
            {
                result = _engine.ExecuteHomeTransfer(txtHomeMain.Text, txtHomeUsb.Text, _pendingInstructions, reporter);
            });

            btnHomeExecute.Enabled = false;
            _pendingInstructions = null;
            BindGrid();

            ShowSummary(result, "USB Transfer Complete");
            lblStatus.Text = "Transfer finished. Safe to eject USB.";
        }

        // --- OFFSITE TAB ---

        private async void btnOffsiteAnalyze_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOffsiteUsb.Text))
            {
                MessageBox.Show("Please select the USB drive."); return;
            }

            _pendingInstructions = null;
            btnOffsiteExecute.Enabled = false;
            BindGrid();

            await RunTask("Reading USB instructions...", reporter =>
            {
                _pendingInstructions = _engine.AnalyzeForOffsite(txtOffsiteUsb.Text);
                reporter("Done.", 100);
            });

            BindGrid();

            if (_pendingInstructions != null && _pendingInstructions.Any())
            {
                btnOffsiteExecute.Enabled = true;
                lblStatus.Text = $"Pending: {_pendingInstructions.Count} updates from USB.";
            }
            else
            {
                lblStatus.Text = "No pending instructions found on USB.";
            }
        }

        private async void btnOffsiteExecute_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtOffsiteTarget.Text))
            {
                MessageBox.Show("Please select the Offsite Backup folder."); return;
            }

            SyncResult result = null;
            await RunTask("Applying updates to Backup Drive...", reporter =>
            {
                result = _engine.ExecuteOffsiteUpdate(txtOffsiteTarget.Text, txtOffsiteUsb.Text, _pendingInstructions, reporter);
            });

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
                // Note: We don't apply exclusions here by default to ensure the catalog represents REALITY on the backup drive.
                // However, you could pass _settings.ExclusionPatterns if you want the catalog to ignore files that shouldn't be there.
                await RunTask("Generating Catalog...", reporter => _engine.GenerateCatalog(txtOffsiteTarget.Text, txtOffsiteUsb.Text, null));

                lblStatus.Text = "Initialization Complete. USB is ready to go Home.";
                MessageBox.Show("Catalog created successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // --- HELPERS ---

        private void BindGrid()
        {
            gridChanges.DataSource = null;
            if (_pendingInstructions == null)
            {
                lblStats.Text = "";
                return;
            }

            gridChanges.DataSource = _pendingInstructions;
            if (_pendingInstructions.Any())
            {
                long totalSize = _pendingInstructions.Sum(x => x.RawSizeBytes);
                lblStats.Text = $"Data to Transfer: {totalSize / 1024.0 / 1024.0:F2} MB";
            }
            else
            {
                lblStats.Text = "No changes.";
            }
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
                if (pct >= 0)
                {
                    if (progressBar1.Style != ProgressBarStyle.Blocks) progressBar1.Style = ProgressBarStyle.Blocks;
                    progressBar1.Value = Math.Min(pct, 100);
                }
            }));

            try
            {
                await Task.Run(() => action(reporter));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Operation Failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EnableUi(true);
                progressBar1.Style = ProgressBarStyle.Blocks;
                progressBar1.Value = 0;
            }
        }

        private void EnableUi(bool en) { tabControls.Enabled = en; Cursor = en ? Cursors.Default : Cursors.WaitCursor; }
        private string Pick() { using var d = new FolderBrowserDialog(); return d.ShowDialog() == DialogResult.OK ? d.SelectedPath : ""; }

        private void btnBrowseHomeMain_Click(object sender, EventArgs e) => txtHomeMain.Text = Pick();
        private void btnBrowseHomeUsb_Click(object sender, EventArgs e) => txtHomeUsb.Text = Pick();
        private void btnBrowseOffsiteTarget_Click(object sender, EventArgs e) => txtOffsiteTarget.Text = Pick();
        private void btnBrowseOffsiteUsb_Click(object sender, EventArgs e) => txtOffsiteUsb.Text = Pick();
    }
}