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
        
        // ISO Copy specific fields
        private bool isISOCopy = false;
        private string sourceISOPath;
        private string targetISOPath;
        private long totalFileSize;

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
        
        // Constructor for ISO copy operations
        public fRM2Progress(string operationName, string statusText, Config config, bool isISOCopy)
        {
            InitializeComponent();
            this.operationName = operationName;
            this.config = config;
            this.Text = $"RM2 {operationName} - Progress";
            this.isISOCopy = isISOCopy;
            
            // Initialize the form
            InitializeForm();
            
            // Set initial status for ISO copy
            UpdateStatus(statusText);
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
        
        public void ShowCopyProgress(string sourcePath, string targetPath, long fileSize)
        {
            this.sourceISOPath = sourcePath;
            this.targetISOPath = targetPath;
            this.totalFileSize = fileSize;
            
            // Configure form for ISO copy operation
            this.progressBar.Visible = true;
            this.progressBar.Minimum = 0;
            this.progressBar.Maximum = 100;
            this.progressBar.Value = 0;
            this.txtOutput.Visible = false;
            
            // Start the copy operation
            this.Load += (sender, e) => StartISOCopy();
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

        private void StartISOCopy()
        {
            try
            {
                UpdateStatus("Starting ISO copy operation...");
                
                // Ensure target directory exists
                string targetDir = Path.GetDirectoryName(targetISOPath);
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
                
                // Start the copy operation in a background thread
                System.Threading.Thread copyThread = new System.Threading.Thread(() =>
                {
                    try
                    {
                        // Use optimized copy with larger buffer and less frequent UI updates
                        CopyFileOptimized(sourceISOPath, targetISOPath);
                        
                        // Copy completed successfully
                        this.Invoke((MethodInvoker)delegate
                        {
                            UpdateStatus("ISO copy completed successfully!");
                            btnClose.Text = "Close";
                            btnClose.Enabled = true;
                            isCompleted = true;
                            
                            // Auto-close the form after a short delay
                            System.Threading.Thread.Sleep(1000); // Wait 1 second
                            this.Close();
                        });
                    }
                    catch (Exception ex)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            UpdateStatus($"Error during copy: {ex.Message}");
                            btnClose.Text = "Close";
                            btnClose.Enabled = true;
                            isCompleted = true;
                        });
                    }
                });
                
                copyThread.Start();
            }
            catch (Exception ex)
            {
                UpdateStatus($"Error starting ISO copy: {ex.Message}");
                btnClose.Text = "Close";
                btnClose.Enabled = true;
                isCompleted = true;
            }
        }
        
        private void CopyFileOptimized(string sourcePath, string targetPath)
        {
            // Use a much larger buffer for better performance
            const int bufferSize = 1024 * 1024; // 1MB buffer (much faster than 8KB)
            byte[] buffer = new byte[bufferSize];
            
            using (FileStream sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream targetStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                long totalBytesRead = 0;
                int bytesRead;
                int updateCounter = 0;
                const int updateFrequency = 50; // Update UI every 50 reads (less frequent updates = better performance)
                
                while ((bytesRead = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    targetStream.Write(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;
                    updateCounter++;
                    
                    // Update progress less frequently to improve performance
                    if (updateCounter >= updateFrequency)
                    {
                        this.Invoke((MethodInvoker)delegate
                        {
                            double progressPercent = (double)totalBytesRead / totalFileSize * 100;
                            UpdateStatus($"Copying ISO... {progressPercent:F1}% ({FormatFileSize(totalBytesRead)} / {FormatFileSize(totalFileSize)})");
                            
                            // Update progress bar
                            if (progressBar != null)
                            {
                                progressBar.Value = (int)progressPercent;
                            }
                        });
                        updateCounter = 0;
                    }
                }
                
                // Final progress update
                this.Invoke((MethodInvoker)delegate
                {
                    UpdateStatus($"Copying ISO... 100.0% ({FormatFileSize(totalFileSize)} / {FormatFileSize(totalFileSize)})");
                    if (progressBar != null)
                    {
                        progressBar.Value = 100;
                    }
                });
            }
        }
        
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
        
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (isISOCopy)
            {
                // For ISO copy, just close without confirmation
                // Streams are automatically disposed by using statements
            }
            else if (process != null && !process.HasExited)
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
