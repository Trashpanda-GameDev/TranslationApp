using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TranslationApp
{
    public class AutoApplyService
    {
        private readonly GameConfig _gameConfig;
        private readonly string _projectRootPath;
        private readonly string _pythonPath;
        private readonly string _rm2ApplyScriptPath;
        private readonly string _replaceSpecificScriptPath;
        
        // Progress tracking
        public event EventHandler<AutoApplyProgressEventArgs> ProgressChanged;
        public event EventHandler<AutoApplyCompletedEventArgs> ProcessingCompleted;

        public AutoApplyService(GameConfig gameConfig)
        {
            _gameConfig = gameConfig;
            _projectRootPath = gameConfig.ProjectRootPath;
            _pythonPath = gameConfig.PythonPath;
            
            // Set script paths based on project root
            _rm2ApplyScriptPath = Path.Combine(_projectRootPath, "tools", "rm2_apply.py");
            _replaceSpecificScriptPath = Path.Combine(_projectRootPath, "tools", "replace-specific.py");
        }

        /// <summary>
        /// Process XML files that have been saved and need ARC processing
        /// </summary>
        /// <param name="xmlFiles">List of XML file paths that were saved</param>
        public async Task ProcessXmlFilesAsync(List<string> xmlFiles)
        {
            var message = $"[AutoApply] Starting auto-apply process for {xmlFiles.Count} XML files";
            Debug.WriteLine(message);
            Console.WriteLine(message);
            
            if (!_gameConfig.AutoApplyEnabled)
            {
                var disabledMessage = "[AutoApply] Auto-apply is disabled, skipping processing";
                Debug.WriteLine(disabledMessage);
                Console.WriteLine(disabledMessage);
                return;
            }

            try
            {
                Debug.WriteLine("[AutoApply] Step 1: Processing XML files to create/update ARC files");
                var arcFiles = await ProcessXmlToArcAsync(xmlFiles);
                
                Debug.WriteLine($"[AutoApply] Step 1 completed: {arcFiles.Count} ARC files processed");
                
                if (arcFiles.Count > 0)
                {
                    Debug.WriteLine("[AutoApply] Step 2: Updating ISO with new ARC files");
                    await UpdateIsoWithArcFilesAsync(arcFiles);
                    Debug.WriteLine("[AutoApply] Step 2 completed: ISO updated successfully");
                }
                else
                {
                    Debug.WriteLine("[AutoApply] No ARC files to process, skipping ISO update");
                }

                Debug.WriteLine($"[AutoApply] Auto-apply process completed successfully. XML: {xmlFiles.Count}, ARC: {arcFiles.Count}");
                
                // Notify completion
                ProcessingCompleted?.Invoke(this, new AutoApplyCompletedEventArgs
                {
                    Success = true,
                    ProcessedXmlFiles = xmlFiles.Count,
                    ProcessedArcFiles = arcFiles.Count
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoApply] Auto-apply process failed: {ex.Message}");
                Debug.WriteLine($"[AutoApply] Stack trace: {ex.StackTrace}");
                
                ProcessingCompleted?.Invoke(this, new AutoApplyCompletedEventArgs
                {
                    Success = false,
                    ErrorMessage = ex.Message,
                    ProcessedXmlFiles = 0,
                    ProcessedArcFiles = 0
                });
            }
        }

        /// <summary>
        /// Process XML files to create/update corresponding ARC files
        /// </summary>
        private async Task<List<string>> ProcessXmlToArcAsync(List<string> xmlFiles)
        {
            var processedArcFiles = new List<string>();
            var failedXmlFiles = new List<string>();

            Debug.WriteLine($"[AutoApply] Processing {xmlFiles.Count} XML files to ARC files");
            ReportProgress("Processing XML files...", 0, xmlFiles.Count);

            for (int i = 0; i < xmlFiles.Count; i++)
            {
                var xmlFile = xmlFiles[i];
                var arcName = GetArcNameFromXml(xmlFile);
                
                Debug.WriteLine($"[AutoApply] Processing XML file {i + 1}/{xmlFiles.Count}: {Path.GetFileName(xmlFile)}");
                
                if (string.IsNullOrEmpty(arcName))
                {
                    Debug.WriteLine($"[AutoApply] Could not determine ARC name for {xmlFile}, skipping");
                    continue;
                }

                Debug.WriteLine($"[AutoApply] Determined ARC name: {arcName}");
                ReportProgress($"Creating ARC file: {arcName}", i + 1, xmlFiles.Count);

                try
                {
                    Debug.WriteLine($"[AutoApply] Creating ARC file: {arcName}");
                    var success = await CreateArcFileAsync(arcName);
                    if (success)
                    {
                        Debug.WriteLine($"[AutoApply] Successfully created ARC file: {arcName}");
                        processedArcFiles.Add(arcName);
                        // Mark XML as successfully processed
                        MarkXmlAsProcessed(xmlFile);
                    }
                    else
                    {
                        Debug.WriteLine($"[AutoApply] Failed to create ARC file: {arcName}");
                        failedXmlFiles.Add(xmlFile);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AutoApply] Exception while processing XML file {xmlFile}: {ex.Message}");
                    failedXmlFiles.Add(xmlFile);
                }
            }

            Debug.WriteLine($"[AutoApply] XML to ARC processing completed. Success: {processedArcFiles.Count}, Failed: {failedXmlFiles.Count}");

            // Update failed files list for retry on next save
            if (failedXmlFiles.Count > 0)
            {
                Debug.WriteLine($"[AutoApply] Adding {failedXmlFiles.Count} failed files to retry list");
                UpdateFailedXmlFiles(failedXmlFiles);
            }

            return processedArcFiles;
        }

        /// <summary>
        /// Get ARC file name from XML file path
        /// </summary>
        private string GetArcNameFromXml(string xmlFilePath)
        {
            try
            {
                var fileName = Path.GetFileNameWithoutExtension(xmlFilePath);
                var directory = Path.GetDirectoryName(xmlFilePath);
                
                // Determine if this is facechat or npc based on directory structure
                if (directory.Contains("facechat"))
                {
                    return $"{fileName}.arc";
                }
                else if (directory.Contains("npc"))
                {
                    return $"{fileName}.arc";
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Create ARC file using rm2_apply.py script
        /// </summary>
        private async Task<bool> CreateArcFileAsync(string arcName)
        {
            try
            {
                Debug.WriteLine($"[AutoApply] Starting rm2_apply.py for ARC: {arcName}");
                Debug.WriteLine($"[AutoApply] Python path: {_pythonPath}");
                Debug.WriteLine($"[AutoApply] Script path: {_rm2ApplyScriptPath}");
                Debug.WriteLine($"[AutoApply] Working directory: {_projectRootPath}");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = $"\"{_rm2ApplyScriptPath}\" --only {arcName}",
                    WorkingDirectory = _projectRootPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                Debug.WriteLine($"[AutoApply] Command: {startInfo.FileName} {startInfo.Arguments}");

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    Debug.WriteLine($"[AutoApply] Process started with PID: {process.Id}");
                    
                    // Wait for completion with timeout
                    var completed = await Task.Run(() => process.WaitForExit(30000)); // 30 second timeout
                    
                    if (!completed)
                    {
                        Debug.WriteLine($"[AutoApply] Process timed out after 30 seconds, killing process");
                        process.Kill();
                        return false;
                    }

                    Debug.WriteLine($"[AutoApply] Process completed with exit code: {process.ExitCode}");
                    
                    // Log output for debugging
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        Debug.WriteLine($"[AutoApply] rm2_apply.py output: {output}");
                    }
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.WriteLine($"[AutoApply] rm2_apply.py error: {error}");
                    }

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoApply] Exception in CreateArcFileAsync for {arcName}: {ex.Message}");
                Debug.WriteLine($"[AutoApply] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Update ISO with processed ARC files
        /// </summary>
        private async Task UpdateIsoWithArcFilesAsync(List<string> arcFiles)
        {
            if (arcFiles.Count == 0)
            {
                Debug.WriteLine("[AutoApply] No ARC files to update in ISO, skipping");
                return;
            }

            Debug.WriteLine($"[AutoApply] Updating ISO with {arcFiles.Count} ARC files");
            ReportProgress("Updating ISO...", 0, arcFiles.Count);

            for (int i = 0; i < arcFiles.Count; i++)
            {
                var arcFile = arcFiles[i];
                Debug.WriteLine($"[AutoApply] Updating ISO with ARC file {i + 1}/{arcFiles.Count}: {arcFile}");
                ReportProgress($"Updating ISO: {arcFile}", i + 1, arcFiles.Count);

                try
                {
                    var success = await ReplaceArcInIsoAsync(arcFile);
                    if (success)
                    {
                        Debug.WriteLine($"[AutoApply] Successfully updated ISO with ARC file: {arcFile}");
                        // Mark ARC as successfully processed
                        MarkArcAsProcessed(arcFile);
                    }
                    else
                    {
                        Debug.WriteLine($"[AutoApply] Failed to update ISO with ARC file: {arcFile}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AutoApply] Exception while updating ISO with ARC file {arcFile}: {ex.Message}");
                }
            }
            
            Debug.WriteLine("[AutoApply] ISO update process completed");
        }

        /// <summary>
        /// Replace ARC file in ISO using replace-specific.py script
        /// </summary>
        private async Task<bool> ReplaceArcInIsoAsync(string arcName)
        {
            try
            {
                Debug.WriteLine($"[AutoApply] Starting replace-specific.py for ARC: {arcName}");
                
                // Determine the full path to the ARC file in 3_patched directory
                string arcFilePath = null;
                
                // Check facechat directory first
                var facechatPath = Path.Combine(_projectRootPath, "3_patched", "PSP_GAME", "USRDIR", "facechat", arcName);
                Debug.WriteLine($"[AutoApply] Checking facechat path: {facechatPath}");
                if (File.Exists(facechatPath))
                {
                    arcFilePath = facechatPath;
                    Debug.WriteLine($"[AutoApply] Found ARC file in facechat directory");
                }
                else
                {
                    // Check npc directory
                    var npcPath = Path.Combine(_projectRootPath, "3_patched", "PSP_GAME", "USRDIR", "npc", arcName);
                    Debug.WriteLine($"[AutoApply] Checking npc path: {npcPath}");
                    if (File.Exists(npcPath))
                    {
                        arcFilePath = npcPath;
                        Debug.WriteLine($"[AutoApply] Found ARC file in npc directory");
                    }
                }

                if (string.IsNullOrEmpty(arcFilePath))
                {
                    Debug.WriteLine($"[AutoApply] ARC file not found in either facechat or npc directory: {arcName}");
                    return false;
                }

                Debug.WriteLine($"[AutoApply] Using ARC file path: {arcFilePath}");
                Debug.WriteLine($"[AutoApply] Python path: {_pythonPath}");
                Debug.WriteLine($"[AutoApply] Script path: {_replaceSpecificScriptPath}");
                Debug.WriteLine($"[AutoApply] Working directory: {_projectRootPath}");

                var startInfo = new ProcessStartInfo
                {
                    FileName = _pythonPath,
                    Arguments = $"\"{_replaceSpecificScriptPath}\" \"{arcFilePath}\"",
                    WorkingDirectory = _projectRootPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                Debug.WriteLine($"[AutoApply] Command: {startInfo.FileName} {startInfo.Arguments}");

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    Debug.WriteLine($"[AutoApply] Process started with PID: {process.Id}");
                    
                    // Wait for completion with timeout
                    var completed = await Task.Run(() => process.WaitForExit(60000)); // 60 second timeout
                    
                    if (!completed)
                    {
                        Debug.WriteLine($"[AutoApply] Process timed out after 60 seconds, killing process");
                        process.Kill();
                        return false;
                    }

                    Debug.WriteLine($"[AutoApply] Process completed with exit code: {process.ExitCode}");
                    
                    // Log output for debugging
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    
                    if (!string.IsNullOrEmpty(output))
                    {
                        Debug.WriteLine($"[AutoApply] replace-specific.py output: {output}");
                    }
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.WriteLine($"[AutoApply] replace-specific.py error: {error}");
                    }

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoApply] Exception in ReplaceArcInIsoAsync for {arcName}: {ex.Message}");
                Debug.WriteLine($"[AutoApply] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Mark XML file as successfully processed
        /// </summary>
        private void MarkXmlAsProcessed(string xmlFilePath)
        {
            try
            {
                var fileName = Path.GetFileName(xmlFilePath);
                if (_gameConfig.LastXmlProcessed.ContainsKey(fileName))
                {
                    _gameConfig.LastXmlProcessed[fileName] = DateTime.Now;
                }
                else
                {
                    _gameConfig.LastXmlProcessed.Add(fileName, DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to mark XML file as processed: {ex.Message}");
            }
        }

        /// <summary>
        /// Mark ARC file as successfully processed
        /// </summary>
        private void MarkArcAsProcessed(string arcName)
        {
            try
            {
                if (_gameConfig.LastArcProcessed.ContainsKey(arcName))
                {
                    _gameConfig.LastArcProcessed[arcName] = DateTime.Now;
                }
                else
                {
                    _gameConfig.LastArcProcessed.Add(arcName, DateTime.Now);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to mark ARC file as processed: {ex.Message}");
            }
        }

        /// <summary>
        /// Update list of failed XML files for retry
        /// </summary>
        private void UpdateFailedXmlFiles(List<string> failedFiles)
        {
            try
            {
                foreach (var file in failedFiles)
                {
                    var fileName = Path.GetFileName(file);
                    if (!_gameConfig.FailedXmlFiles.Contains(fileName))
                    {
                        _gameConfig.FailedXmlFiles.Add(fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update failed XML files list: {ex.Message}");
            }
        }

        /// <summary>
        /// Report progress updates
        /// </summary>
        private void ReportProgress(string message, int current, int total)
        {
            var percentage = total > 0 ? (current * 100) / total : 0;
            ProgressChanged?.Invoke(this, new AutoApplyProgressEventArgs
            {
                Message = message,
                Current = current,
                Total = total,
                Percentage = percentage
            });
        }
    }

    /// <summary>
    /// Event arguments for progress updates
    /// </summary>
    public class AutoApplyProgressEventArgs : EventArgs
    {
        public string Message { get; set; }
        public int Current { get; set; }
        public int Total { get; set; }
        public int Percentage { get; set; }
    }

    /// <summary>
    /// Event arguments for processing completion
    /// </summary>
    public class AutoApplyCompletedEventArgs : EventArgs
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int ProcessedXmlFiles { get; set; }
        public int ProcessedArcFiles { get; set; }
    }
}
