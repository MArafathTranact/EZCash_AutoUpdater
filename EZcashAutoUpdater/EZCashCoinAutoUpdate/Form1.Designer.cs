namespace EZCashCoinAutoUpdate
{
    partial class AutoUpdater
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AutoUpdater));
            lblStatus = new Label();
            btnCancel = new Button();
            txtServiceInstallPath = new TextBox();
            lblService = new Label();
            txtServiceDownloadURL = new TextBox();
            lblVersion = new Label();
            btnSaveConfiguration = new Button();
            folderselectdialog = new FolderBrowserDialog();
            btnFolderSelect = new Button();
            SuspendLayout();
            // 
            // lblStatus
            // 
            lblStatus.AutoSize = true;
            lblStatus.BackColor = Color.Transparent;
            lblStatus.Font = new Font("Verdana", 9F, FontStyle.Bold);
            lblStatus.ForeColor = SystemColors.HighlightText;
            lblStatus.Location = new Point(28, 90);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(0, 14);
            lblStatus.TabIndex = 14;
            // 
            // btnCancel
            // 
            btnCancel.Font = new Font("Verdana", 9F);
            btnCancel.Location = new Point(188, 159);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new Size(75, 29);
            btnCancel.TabIndex = 13;
            btnCancel.Text = "Cancel";
            btnCancel.UseVisualStyleBackColor = true;
            btnCancel.Visible = false;
            btnCancel.Click += btnCancel_Click;
            // 
            // txtServiceInstallPath
            // 
            txtServiceInstallPath.Font = new Font("Verdana", 9F);
            txtServiceInstallPath.Location = new Point(177, 109);
            txtServiceInstallPath.Name = "txtServiceInstallPath";
            txtServiceInstallPath.Size = new Size(152, 22);
            txtServiceInstallPath.TabIndex = 12;
            txtServiceInstallPath.Visible = false;
            // 
            // lblService
            // 
            lblService.AutoSize = true;
            lblService.BackColor = Color.Transparent;
            lblService.Font = new Font("Verdana", 9F, FontStyle.Bold);
            lblService.ForeColor = SystemColors.HighlightText;
            lblService.Location = new Point(12, 112);
            lblService.Name = "lblService";
            lblService.Size = new Size(137, 14);
            lblService.TabIndex = 11;
            lblService.Text = "Service Install Path";
            lblService.Visible = false;
            // 
            // txtServiceDownloadURL
            // 
            txtServiceDownloadURL.Font = new Font("Verdana", 9F);
            txtServiceDownloadURL.Location = new Point(177, 65);
            txtServiceDownloadURL.Name = "txtServiceDownloadURL";
            txtServiceDownloadURL.Size = new Size(152, 22);
            txtServiceDownloadURL.TabIndex = 10;
            txtServiceDownloadURL.Visible = false;
            // 
            // lblVersion
            // 
            lblVersion.AutoSize = true;
            lblVersion.BackColor = Color.Transparent;
            lblVersion.Font = new Font("Verdana", 9F, FontStyle.Bold);
            lblVersion.ForeColor = SystemColors.HighlightText;
            lblVersion.Location = new Point(12, 68);
            lblVersion.Name = "lblVersion";
            lblVersion.Size = new Size(158, 14);
            lblVersion.TabIndex = 9;
            lblVersion.Text = "Service Download URL";
            lblVersion.Visible = false;
            // 
            // btnSaveConfiguration
            // 
            btnSaveConfiguration.Font = new Font("Verdana", 9F);
            btnSaveConfiguration.Location = new Point(98, 159);
            btnSaveConfiguration.Name = "btnSaveConfiguration";
            btnSaveConfiguration.Size = new Size(76, 30);
            btnSaveConfiguration.TabIndex = 8;
            btnSaveConfiguration.Text = "Save";
            btnSaveConfiguration.UseVisualStyleBackColor = true;
            btnSaveConfiguration.Visible = false;
            btnSaveConfiguration.Click += btnSaveConfiguration_Click;
            // 
            // btnFolderSelect
            // 
            btnFolderSelect.Location = new Point(333, 110);
            btnFolderSelect.Name = "btnFolderSelect";
            btnFolderSelect.Size = new Size(31, 23);
            btnFolderSelect.TabIndex = 15;
            btnFolderSelect.Text = "...";
            btnFolderSelect.UseVisualStyleBackColor = true;
            btnFolderSelect.Visible = false;
            btnFolderSelect.Click += btnFolderSelect_Click;
            // 
            // AutoUpdater
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackgroundImage = (Image)resources.GetObject("$this.BackgroundImage");
            BackgroundImageLayout = ImageLayout.Stretch;
            ClientSize = new Size(377, 218);
            Controls.Add(lblStatus);
            Controls.Add(btnCancel);
            Controls.Add(txtServiceInstallPath);
            Controls.Add(lblService);
            Controls.Add(txtServiceDownloadURL);
            Controls.Add(lblVersion);
            Controls.Add(btnSaveConfiguration);
            Controls.Add(btnFolderSelect);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Name = "AutoUpdater";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "EZCash Coin Service Auto Update";
            Load += AutoUpdater_LoadAsync;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblStatus;
        private Button btnCancel;
        private TextBox txtServiceInstallPath;
        private Label lblService;
        private TextBox txtServiceDownloadURL;
        private Label lblVersion;
        private Button btnSaveConfiguration;
        private FolderBrowserDialog folderselectdialog;
        private Button btnFolderSelect;
    }
}
