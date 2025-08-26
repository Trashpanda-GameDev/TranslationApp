using System;
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
            }
            else
            {
                txtFolderPath.Text = "Not set";
                txtIsoPath.Text = "";
                chkShowAutoLoadMessage.Checked = true; // Default to showing the message
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
