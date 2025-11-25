namespace GUI
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            tabControls = new TabControl();
            tabHome = new TabPage();
            grpHomeActions = new GroupBox();
            btnHomeExecute = new Button();
            btnHomeAnalyze = new Button();
            btnBrowseHomeUsb = new Button();
            txtHomeUsb = new TextBox();
            label2 = new Label();
            btnBrowseHomeMain = new Button();
            txtHomeMain = new TextBox();
            label1 = new Label();
            tabOffsite = new TabPage();
            grpOffsiteActions = new GroupBox();
            btnInit = new Button();
            btnOffsiteExecute = new Button();
            btnOffsiteAnalyze = new Button();
            btnBrowseOffsiteUsb = new Button();
            txtOffsiteUsb = new TextBox();
            label3 = new Label();
            btnBrowseOffsiteTarget = new Button();
            txtOffsiteTarget = new TextBox();
            label4 = new Label();
            gridChanges = new DataGridView();
            progressBar1 = new ProgressBar();
            lblStatus = new Label();
            lblStats = new Label();
            tabControls.SuspendLayout();
            tabHome.SuspendLayout();
            grpHomeActions.SuspendLayout();
            tabOffsite.SuspendLayout();
            grpOffsiteActions.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridChanges).BeginInit();
            SuspendLayout();
            // 
            // tabControls
            // 
            tabControls.Controls.Add(tabHome);
            tabControls.Controls.Add(tabOffsite);
            tabControls.Dock = DockStyle.Top;
            tabControls.Location = new Point(0, 0);
            tabControls.Name = "tabControls";
            tabControls.SelectedIndex = 0;
            tabControls.Size = new Size(684, 180);
            tabControls.TabIndex = 0;
            // 
            // tabHome
            // 
            tabHome.Controls.Add(grpHomeActions);
            tabHome.Controls.Add(btnBrowseHomeUsb);
            tabHome.Controls.Add(txtHomeUsb);
            tabHome.Controls.Add(label2);
            tabHome.Controls.Add(btnBrowseHomeMain);
            tabHome.Controls.Add(txtHomeMain);
            tabHome.Controls.Add(label1);
            tabHome.Location = new Point(4, 24);
            tabHome.Name = "tabHome";
            tabHome.Padding = new Padding(3);
            tabHome.Size = new Size(676, 152);
            tabHome.TabIndex = 0;
            tabHome.Text = "1. AT HOME (Main PC)";
            tabHome.UseVisualStyleBackColor = true;
            // 
            // grpHomeActions
            // 
            grpHomeActions.Controls.Add(btnHomeExecute);
            grpHomeActions.Controls.Add(btnHomeAnalyze);
            grpHomeActions.Location = new Point(11, 75);
            grpHomeActions.Name = "grpHomeActions";
            grpHomeActions.Size = new Size(657, 68);
            grpHomeActions.TabIndex = 6;
            grpHomeActions.TabStop = false;
            grpHomeActions.Text = "Actions";
            // 
            // btnHomeExecute
            // 
            btnHomeExecute.Enabled = false;
            btnHomeExecute.Location = new Point(198, 22);
            btnHomeExecute.Name = "btnHomeExecute";
            btnHomeExecute.Size = new Size(170, 35);
            btnHomeExecute.TabIndex = 1;
            btnHomeExecute.Text = "Step 2: Sync to USB";
            btnHomeExecute.UseVisualStyleBackColor = true;
            btnHomeExecute.Click += btnHomeExecute_Click;
            // 
            // btnHomeAnalyze
            // 
            btnHomeAnalyze.Location = new Point(15, 22);
            btnHomeAnalyze.Name = "btnHomeAnalyze";
            btnHomeAnalyze.Size = new Size(170, 35);
            btnHomeAnalyze.TabIndex = 0;
            btnHomeAnalyze.Text = "Step 1: Analyze Differences";
            btnHomeAnalyze.UseVisualStyleBackColor = true;
            btnHomeAnalyze.Click += btnHomeAnalyze_Click;
            // 
            // btnBrowseHomeUsb
            // 
            btnBrowseHomeUsb.Location = new Point(593, 44);
            btnBrowseHomeUsb.Name = "btnBrowseHomeUsb";
            btnBrowseHomeUsb.Size = new Size(75, 23);
            btnBrowseHomeUsb.TabIndex = 5;
            btnBrowseHomeUsb.Text = "Browse";
            btnBrowseHomeUsb.Click += btnBrowseHomeUsb_Click;
            // 
            // txtHomeUsb
            // 
            txtHomeUsb.Location = new Point(100, 44);
            txtHomeUsb.Name = "txtHomeUsb";
            txtHomeUsb.ReadOnly = true;
            txtHomeUsb.Size = new Size(487, 23);
            txtHomeUsb.TabIndex = 4;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(8, 48);
            label2.Name = "label2";
            label2.Size = new Size(61, 15);
            label2.TabIndex = 7;
            label2.Text = "USB Drive:";
            // 
            // btnBrowseHomeMain
            // 
            btnBrowseHomeMain.Location = new Point(593, 15);
            btnBrowseHomeMain.Name = "btnBrowseHomeMain";
            btnBrowseHomeMain.Size = new Size(75, 23);
            btnBrowseHomeMain.TabIndex = 2;
            btnBrowseHomeMain.Text = "Browse";
            btnBrowseHomeMain.Click += btnBrowseHomeMain_Click;
            // 
            // txtHomeMain
            // 
            txtHomeMain.Location = new Point(100, 15);
            txtHomeMain.Name = "txtHomeMain";
            txtHomeMain.ReadOnly = true;
            txtHomeMain.Size = new Size(487, 23);
            txtHomeMain.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(8, 19);
            label1.Name = "label1";
            label1.Size = new Size(84, 15);
            label1.TabIndex = 8;
            label1.Text = "Main HD Path:";
            // 
            // tabOffsite
            // 
            tabOffsite.Controls.Add(grpOffsiteActions);
            tabOffsite.Controls.Add(btnBrowseOffsiteUsb);
            tabOffsite.Controls.Add(txtOffsiteUsb);
            tabOffsite.Controls.Add(label3);
            tabOffsite.Controls.Add(btnBrowseOffsiteTarget);
            tabOffsite.Controls.Add(txtOffsiteTarget);
            tabOffsite.Controls.Add(label4);
            tabOffsite.Location = new Point(4, 24);
            tabOffsite.Name = "tabOffsite";
            tabOffsite.Padding = new Padding(3);
            tabOffsite.Size = new Size(676, 152);
            tabOffsite.TabIndex = 1;
            tabOffsite.Text = "2. AT OFFSITE (Backup PC)";
            tabOffsite.UseVisualStyleBackColor = true;
            // 
            // grpOffsiteActions
            // 
            grpOffsiteActions.Controls.Add(btnInit);
            grpOffsiteActions.Controls.Add(btnOffsiteExecute);
            grpOffsiteActions.Controls.Add(btnOffsiteAnalyze);
            grpOffsiteActions.Location = new Point(11, 75);
            grpOffsiteActions.Name = "grpOffsiteActions";
            grpOffsiteActions.Size = new Size(657, 68);
            grpOffsiteActions.TabIndex = 13;
            grpOffsiteActions.TabStop = false;
            grpOffsiteActions.Text = "Actions";
            // 
            // btnInit
            // 
            btnInit.Location = new Point(481, 22);
            btnInit.Name = "btnInit";
            btnInit.Size = new Size(155, 35);
            btnInit.TabIndex = 2;
            btnInit.Text = "Init / Reset Catalog";
            btnInit.UseVisualStyleBackColor = true;
            btnInit.Click += btnInit_Click;
            // 
            // btnOffsiteExecute
            // 
            btnOffsiteExecute.Enabled = false;
            btnOffsiteExecute.Location = new Point(198, 22);
            btnOffsiteExecute.Name = "btnOffsiteExecute";
            btnOffsiteExecute.Size = new Size(170, 35);
            btnOffsiteExecute.TabIndex = 1;
            btnOffsiteExecute.Text = "Step 2: Update Backup Drive";
            btnOffsiteExecute.UseVisualStyleBackColor = true;
            btnOffsiteExecute.Click += btnOffsiteExecute_Click;
            // 
            // btnOffsiteAnalyze
            // 
            btnOffsiteAnalyze.Location = new Point(15, 22);
            btnOffsiteAnalyze.Name = "btnOffsiteAnalyze";
            btnOffsiteAnalyze.Size = new Size(170, 35);
            btnOffsiteAnalyze.TabIndex = 0;
            btnOffsiteAnalyze.Text = "Step 1: View USB Changes";
            btnOffsiteAnalyze.UseVisualStyleBackColor = true;
            btnOffsiteAnalyze.Click += btnOffsiteAnalyze_Click;
            // 
            // btnBrowseOffsiteUsb
            // 
            btnBrowseOffsiteUsb.Location = new Point(593, 44);
            btnBrowseOffsiteUsb.Name = "btnBrowseOffsiteUsb";
            btnBrowseOffsiteUsb.Size = new Size(75, 23);
            btnBrowseOffsiteUsb.TabIndex = 12;
            btnBrowseOffsiteUsb.Text = "Browse";
            btnBrowseOffsiteUsb.Click += btnBrowseOffsiteUsb_Click;
            // 
            // txtOffsiteUsb
            // 
            txtOffsiteUsb.Location = new Point(100, 44);
            txtOffsiteUsb.Name = "txtOffsiteUsb";
            txtOffsiteUsb.ReadOnly = true;
            txtOffsiteUsb.Size = new Size(487, 23);
            txtOffsiteUsb.TabIndex = 11;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(8, 48);
            label3.Name = "label3";
            label3.Size = new Size(61, 15);
            label3.TabIndex = 14;
            label3.Text = "USB Drive:";
            // 
            // btnBrowseOffsiteTarget
            // 
            btnBrowseOffsiteTarget.Location = new Point(593, 15);
            btnBrowseOffsiteTarget.Name = "btnBrowseOffsiteTarget";
            btnBrowseOffsiteTarget.Size = new Size(75, 23);
            btnBrowseOffsiteTarget.TabIndex = 9;
            btnBrowseOffsiteTarget.Text = "Browse";
            btnBrowseOffsiteTarget.Click += btnBrowseOffsiteTarget_Click;
            // 
            // txtOffsiteTarget
            // 
            txtOffsiteTarget.Location = new Point(100, 15);
            txtOffsiteTarget.Name = "txtOffsiteTarget";
            txtOffsiteTarget.ReadOnly = true;
            txtOffsiteTarget.Size = new Size(487, 23);
            txtOffsiteTarget.TabIndex = 8;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(8, 19);
            label4.Name = "label4";
            label4.Size = new Size(76, 15);
            label4.TabIndex = 15;
            label4.Text = "Backup Path:";
            // 
            // gridChanges
            // 
            gridChanges.AllowUserToAddRows = false;
            gridChanges.AllowUserToDeleteRows = false;
            gridChanges.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            gridChanges.BackgroundColor = Color.White;
            gridChanges.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridChanges.Location = new Point(4, 186);
            gridChanges.Name = "gridChanges";
            gridChanges.ReadOnly = true;
            gridChanges.RowHeadersVisible = false;
            gridChanges.Size = new Size(676, 320);
            gridChanges.TabIndex = 1;
            // 
            // progressBar1
            // 
            progressBar1.Dock = DockStyle.Bottom;
            progressBar1.Location = new Point(0, 528);
            progressBar1.Name = "progressBar1";
            progressBar1.Size = new Size(684, 23);
            progressBar1.TabIndex = 2;
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.Dock = DockStyle.Bottom;
            lblStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblStatus.Location = new Point(0, 508);
            lblStatus.Name = "lblStatus";
            lblStatus.Padding = new Padding(5, 0, 0, 5);
            lblStatus.Size = new Size(48, 20);
            lblStatus.TabIndex = 3;
            lblStatus.Text = "Ready.";
            // 
            // lblStats
            // 
            lblStats.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            lblStats.AutoSize = true;
            lblStats.Location = new Point(550, 513);
            lblStats.Name = "lblStats";
            lblStats.Size = new Size(0, 15);
            lblStats.TabIndex = 4;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(684, 551);
            Controls.Add(lblStats);
            Controls.Add(lblStatus);
            Controls.Add(progressBar1);
            Controls.Add(gridChanges);
            Controls.Add(tabControls);
            Name = "Form1";
            Text = "SneakerNet Sync - Guided Wizard";
            tabControls.ResumeLayout(false);
            tabHome.ResumeLayout(false);
            tabHome.PerformLayout();
            grpHomeActions.ResumeLayout(false);
            tabOffsite.ResumeLayout(false);
            tabOffsite.PerformLayout();
            grpOffsiteActions.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridChanges).EndInit();
            ResumeLayout(false);
            PerformLayout();

        }

        private System.Windows.Forms.TabControl tabControls;
        private System.Windows.Forms.TabPage tabHome;
        private System.Windows.Forms.TabPage tabOffsite;
        private System.Windows.Forms.DataGridView gridChanges;
        private System.Windows.Forms.ProgressBar progressBar1;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblStats;

        // Home Controls
        private System.Windows.Forms.GroupBox grpHomeActions;
        private System.Windows.Forms.Button btnHomeExecute;
        private System.Windows.Forms.Button btnHomeAnalyze;
        private System.Windows.Forms.Button btnBrowseHomeUsb;
        private System.Windows.Forms.TextBox txtHomeUsb;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnBrowseHomeMain;
        private System.Windows.Forms.TextBox txtHomeMain;
        private System.Windows.Forms.Label label1;

        // Offsite Controls
        private System.Windows.Forms.GroupBox grpOffsiteActions;
        private System.Windows.Forms.Button btnInit;
        private System.Windows.Forms.Button btnOffsiteExecute;
        private System.Windows.Forms.Button btnOffsiteAnalyze;
        private System.Windows.Forms.Button btnBrowseOffsiteUsb;
        private System.Windows.Forms.TextBox txtOffsiteUsb;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnBrowseOffsiteTarget;
        private System.Windows.Forms.TextBox txtOffsiteTarget;
        private System.Windows.Forms.Label label4;
    }
}