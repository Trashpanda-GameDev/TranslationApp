using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace TranslationApp
{
    public partial class fRM2Progress : Form
    {
        private Process process;
        private string scriptPath;
        private string operationName;
        private Config config;
        private bool isCompleted = false;
        private System.Windows.Forms.Timer statusTimer;

        public fRM2Progress(string scriptPath, string operationName, Config config)
        {
            InitializeComponent();
            this.scriptPath = scriptPath;
            this.operationName = operationName;
            this.config = config;
            this.Text = $"RM2 {operationName} - Progress";
            
            // Initialize the form
            InitializeForm();
            
            // Start the process after form is shown
            this.Load += (sender, e) => StartProcess();
        }

        private void InitializeForm()
        {
            // Set initial status
            lblStatus.Text = "Initializing...";
            btnClose.Enabled = false;
            btnClose.Text = "Waiting...";
            
            // Make sure the form is visible and focused
            this.TopMost = true;
            this.BringToFront();
            
            // Initialize status timer
            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 1000; // Check every second
            statusTimer.Tick += StatusTimer_Tick;
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            if (process != null && !process.HasExited)
            {
                try
                {
                    // Check if process is still responding
                    if (!process.Responding)
                    {
                        UpdateStatus("Process appears to be unresponsive...");
                    }
                    else
                    {
                        UpdateStatus($"Process running... (PID: {process.Id})");
                    }
                }
                catch
                {
                    // Process might have exited
                    UpdateStatus("Checking process status...");
                }
            }
        }

        private void StartProcess()
        {
            try
            {
                UpdateStatus("Starting process...");
                
                // Get the RM2 config to use the correct Python path
                var gameConfig = this.config.GetGameConfig("RM2");
                if (gameConfig == null)
                {
                    UpdateStatus("Error: RM2 game config not found!");
                    MessageBox.Show("RM2 game configuration not found. Please configure RM2 settings first.", 
                        "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.Close();
                    return;
                }
                
                string pythonPath = gameConfig.PythonPath;
                if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
                {
                    pythonPath = this.config.PythonLocation;
                    if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
                    {
                        UpdateStatus("Error: Python not configured!");
                        MessageBox.Show("Python location not configured or Python executable not found.\n\nPlease configure Python in RM2 → Settings or in the main application settings.", 
                            "Python Not Configured", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        this.Close();
                        return;
                    }
                }
                
                UpdateStatus($"Using Python: {pythonPath}");
                UpdateStatus($"Script: {scriptPath}");
                UpdateStatus($"Working Directory: {gameConfig.ProjectRootPath}");
                
                // Build arguments based on script name
                string arguments = $"\"{scriptPath}\"";
                if (scriptPath.EndsWith("rm2_apply.py"))
                {
                    arguments = $"\"{scriptPath}\" --target both";
                }
                UpdateStatus($"Arguments: {arguments}");
                
                process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = arguments,
                    WorkingDirectory = gameConfig.ProjectRootPath, // Run from project root, not tools directory
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                process.OutputDataReceived += Process_OutputDataReceived;
                process.ErrorDataReceived += Process_ErrorDataReceived;
                process.Exited += Process_Exited;

                UpdateStatus("Starting Python process...");
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Start the status timer
                statusTimer.Start();

                // Update status
                UpdateStatus("Process started successfully!");
                UpdateStatus("Waiting for output...");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error starting process: {ex.Message}");
                MessageBox.Show($"Error starting process: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                this.Invoke((MethodInvoker)delegate
                {
                    txtOutput.AppendText(e.Data + Environment.NewLine);
                    txtOutput.ScrollToCaret();
                    UpdateStatus($"Running... {e.Data}");
                });
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                this.Invoke((MethodInvoker)delegate
                {
                    txtOutput.AppendText("ERROR: " + e.Data + Environment.NewLine);
                    txtOutput.ScrollToCaret();
                    UpdateStatus($"Error: {e.Data}");
                });
            }
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            this.Invoke((MethodInvoker)delegate
            {
                // Stop the status timer
                if (statusTimer != null)
                {
                    statusTimer.Stop();
                }
                
                isCompleted = true;
                if (process.ExitCode == 0)
                {
                    UpdateStatus("Process completed successfully!");
                    btnClose.Text = "Close";
                    btnClose.Enabled = true;
                }
                else
                {
                    UpdateStatus($"Process completed with exit code: {process.ExitCode}");
                    btnClose.Text = "Close";
                    btnClose.Enabled = true;
                }
            });
        }

        private void UpdateStatus(string status)
        {
            lblStatus.Text = status;
            Application.DoEvents();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            if (!isCompleted && process != null && !process.HasExited)
            {
                var result = MessageBox.Show("The process is still running. Do you want to terminate it?", 
                    "Process Running", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        process.Kill();
                        UpdateStatus("Process terminated by user.");
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Error terminating process: {ex.Message}");
                    }
                }
                else
                {
                    return;
                }
            }
            
            this.Close();
        }

        private void btnCopyOutput_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(txtOutput.Text))
            {
                Clipboard.SetText(txtOutput.Text);
                MessageBox.Show("Output copied to clipboard!", "Copied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if (process != null && !process.HasExited)
            {
                UpdateStatus("Manual refresh requested...");
                // Force a status update
                StatusTimer_Tick(null, null);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (process != null && !process.HasExited)
            {
                var result = MessageBox.Show("The process is still running. Do you want to terminate it?", 
                    "Process Running", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }
                }
                else
                {
                    e.Cancel = true;
                    return;
                }
            }
            
            base.OnFormClosing(e);
        }
    }
}
