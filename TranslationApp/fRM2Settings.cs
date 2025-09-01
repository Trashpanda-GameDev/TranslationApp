using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace TranslationApp
{
    public partial class fRM2Settings : Form
    {
        private string isoFilePath;
        private Config config;

        public fRM2Settings(Config config)
        {
            InitializeComponent();
            this.config = config;
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load existing paths if available
            var gameConfig = config.GetGameConfig("RM2");
            if (gameConfig != null)
            {
                // Display current folder path
                if (!string.IsNullOrEmpty(gameConfig.FolderPath))
                {
                    txtFolderPath.Text = gameConfig.FolderPath;
                }
                else
                {
                    txtFolderPath.Text = "Not set";
                }

                // Display current ISO path
                if (!string.IsNullOrEmpty(gameConfig.IsoPath))
                {
                    txtIsoPath.Text = gameConfig.IsoPath;
                    isoFilePath = gameConfig.IsoPath;
                }
                else
                {
                    txtIsoPath.Text = "";
                }

                // Set the auto-load message checkbox
                chkShowAutoLoadMessage.Checked = gameConfig.ShowAutoLoadMessage;

                // Set the Python path
                if (!string.IsNullOrEmpty(gameConfig.PythonPath))
                {
                    txtPythonPath.Text = gameConfig.PythonPath;
                }
                else
                {
                    txtPythonPath.Text = "";
                }

                // Set the auto-apply checkbox
                chkAutoApplyEnabled.Checked = gameConfig.AutoApplyEnabled;
            }
            else
            {
                txtFolderPath.Text = "Not set";
                txtIsoPath.Text = "";
                chkShowAutoLoadMessage.Checked = true; // Default to showing the message
                txtPythonPath.Text = "";
                chkAutoApplyEnabled.Checked = false; // Default to disabled
            }
        }

        private void btnBrowseIso_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "ISO files (*.iso)|*.iso|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.Title = "Select RM2 Original ISO File";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtIsoPath.Text = openFileDialog.FileName;
                    isoFilePath = openFileDialog.FileName;
                }
            }
        }

        private void txtIsoPath_TextChanged(object sender, EventArgs e)
        {
            // Update the private field when user types in the text box
            isoFilePath = txtIsoPath.Text.Trim();
        }

        private void btnBrowsePython_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Python executable (python.exe)|python.exe|All files (*.*)|*.*";
                openFileDialog.FilterIndex = 1;
                openFileDialog.Title = "Select Python Executable";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    txtPythonPath.Text = openFileDialog.FileName;
                }
            }
        }

        private void btnDetectPython_Click(object sender, EventArgs e)
        {
            try
            {
                // Use the same detection method as the main setup
                var start = new System.Diagnostics.ProcessStartInfo();
                start.FileName = @"cmd.exe";
                start.Arguments = "/c where python";
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;

                var process = new System.Diagnostics.Process();
                process.StartInfo = start;

                var pythonInstallations = new List<string>();
                process.Start();
                process.WaitForExit();

                using (var reader = process.StandardOutput)
                {
                    while (reader.Peek() >= 0)
                    {
                        pythonInstallations.Add(reader.ReadLine());
                    }
                }
                process.Dispose();

                if (pythonInstallations.Count > 0)
                {
                    // Show a dialog to let user choose which Python installation
                    using (var form = new Form())
                    {
                        form.Text = "Select Python Installation";
                        form.Size = new System.Drawing.Size(400, 200);
                        form.StartPosition = FormStartPosition.CenterParent;
                        form.FormBorderStyle = FormBorderStyle.FixedDialog;
                        form.MaximizeBox = false;
                        form.MinimizeBox = false;

                        var label = new Label
                        {
                            Text = "Multiple Python installations found. Please select one:",
                            Location = new System.Drawing.Point(10, 10),
                            AutoSize = true
                        };

                        var listBox = new ListBox
                        {
                            Location = new System.Drawing.Point(10, 35),
                            Size = new System.Drawing.Size(360, 80)
                        };

                        foreach (var installation in pythonInstallations)
                        {
                            listBox.Items.Add(installation);
                        }

                        if (listBox.Items.Count > 0)
                            listBox.SelectedIndex = 0;

                        var btnOK = new Button
                        {
                            Text = "OK",
                            DialogResult = DialogResult.OK,
                            Location = new System.Drawing.Point(200, 125)
                        };

                        var btnCancel = new Button
                        {
                            Text = "Cancel",
                            DialogResult = DialogResult.Cancel,
                            Location = new System.Drawing.Point(280, 125)
                        };

                        form.Controls.AddRange(new Control[] { label, listBox, btnOK, btnCancel });

                        if (form.ShowDialog() == DialogResult.OK && listBox.SelectedItem != null)
                        {
                            txtPythonPath.Text = listBox.SelectedItem.ToString();
                        }
                    }
                }
                else
                {
                    MessageBox.Show("No Python installations found on your system.\n\nPlease install Python or manually browse to the Python executable.", 
                        "Python Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error detecting Python installations: {ex.Message}", 
                    "Detection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            string isoPath = txtIsoPath.Text.Trim();
            if (string.IsNullOrEmpty(isoPath))
            {
                MessageBox.Show("Please enter an ISO file path before saving.", "No ISO Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!File.Exists(isoPath))
            {
                MessageBox.Show("The specified ISO file does not exist. Please check the path.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Save the ISO path to the game configuration
            var gameConfig = config.GetGameConfig("RM2");
            if (gameConfig == null)
            {
                gameConfig = new GameConfig("RM2");
                config.GamesConfigList.Add(gameConfig);
            }

            gameConfig.IsoPath = isoPath;
            gameConfig.ShowAutoLoadMessage = chkShowAutoLoadMessage.Checked;
            gameConfig.PythonPath = txtPythonPath.Text.Trim();
            gameConfig.AutoApplyEnabled = chkAutoApplyEnabled.Checked;
            config.Save();

            MessageBox.Show("RM2 Settings saved successfully!", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        public string GetIsoPath()
        {
            return isoFilePath;
        }
    }
}
