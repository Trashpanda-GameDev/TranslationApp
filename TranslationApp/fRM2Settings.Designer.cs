namespace TranslationApp
{
    partial class fRM2Settings
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lblCurrentPaths = new System.Windows.Forms.Label();
            this.lblFolderPath = new System.Windows.Forms.Label();
            this.txtFolderPath = new System.Windows.Forms.TextBox();
            this.lblIsoPath = new System.Windows.Forms.Label();
            this.txtIsoPath = new System.Windows.Forms.TextBox();
            this.btnBrowseIso = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblCurrentPaths
            // 
            this.lblCurrentPaths.AutoSize = true;
            this.lblCurrentPaths.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblCurrentPaths.Location = new System.Drawing.Point(12, 15);
            this.lblCurrentPaths.Name = "lblCurrentPaths";
            this.lblCurrentPaths.Size = new System.Drawing.Size(95, 15);
            this.lblCurrentPaths.TabIndex = 0;
            this.lblCurrentPaths.Text = "Current Paths:";
            // 
            // lblFolderPath
            // 
            this.lblFolderPath.AutoSize = true;
            this.lblFolderPath.Location = new System.Drawing.Point(12, 40);
            this.lblFolderPath.Name = "lblFolderPath";
            this.lblFolderPath.Size = new System.Drawing.Size(70, 13);
            this.lblFolderPath.TabIndex = 1;
            this.lblFolderPath.Text = "Folder Path:";
            // 
            // txtFolderPath
            // 
            this.txtFolderPath.Location = new System.Drawing.Point(88, 37);
            this.txtFolderPath.Name = "txtFolderPath";
            this.txtFolderPath.Size = new System.Drawing.Size(338, 20);
            this.txtFolderPath.TabIndex = 2;
            this.txtFolderPath.ReadOnly = true;
            this.txtFolderPath.BackColor = System.Drawing.SystemColors.Control;
            // 
            // lblIsoPath
            // 
            this.lblIsoPath.AutoSize = true;
            this.lblIsoPath.Location = new System.Drawing.Point(12, 65);
            this.lblIsoPath.Name = "lblIsoPath";
            this.lblIsoPath.Size = new System.Drawing.Size(58, 13);
            this.lblIsoPath.TabIndex = 3;
            this.lblIsoPath.Text = "ISO Path:";
            // 
            // txtIsoPath
            // 
            this.txtIsoPath.Location = new System.Drawing.Point(88, 62);
            this.txtIsoPath.Name = "txtIsoPath";
            this.txtIsoPath.Size = new System.Drawing.Size(338, 20);
            this.txtIsoPath.TabIndex = 4;
            this.txtIsoPath.ReadOnly = false;
            this.txtIsoPath.TextChanged += new System.EventHandler(this.txtIsoPath_TextChanged);
            // 
            // btnBrowseIso
            // 
            this.btnBrowseIso.Location = new System.Drawing.Point(432, 60);
            this.btnBrowseIso.Name = "btnBrowseIso";
            this.btnBrowseIso.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseIso.TabIndex = 5;
            this.btnBrowseIso.Text = "Browse...";
            this.btnBrowseIso.UseVisualStyleBackColor = true;
            this.btnBrowseIso.Click += new System.EventHandler(this.btnBrowseIso_Click);
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(350, 100);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(75, 23);
            this.btnSave.TabIndex = 6;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(432, 100);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 7;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // fRM2Settings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(519, 135);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnBrowseIso);
            this.Controls.Add(this.txtIsoPath);
            this.Controls.Add(this.lblIsoPath);
            this.Controls.Add(this.txtFolderPath);
            this.Controls.Add(this.lblFolderPath);
            this.Controls.Add(this.lblCurrentPaths);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "fRM2Settings";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "RM2 Settings";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Label lblCurrentPaths;
        private System.Windows.Forms.Label lblFolderPath;
        private System.Windows.Forms.TextBox txtFolderPath;
        private System.Windows.Forms.Label lblIsoPath;
        private System.Windows.Forms.TextBox txtIsoPath;
        private System.Windows.Forms.Button btnBrowseIso;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
    }
}
