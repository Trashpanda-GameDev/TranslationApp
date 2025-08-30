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
            if (!_gameConfig.AutoApplyEnabled)
                return;

            try
            {
                // Step 1: Process XML files to create/update ARC files
                var arcFiles = await ProcessXmlToArcAsync(xmlFiles);
                
                if (arcFiles.Count > 0)
                {
                    // Step 2: Update ISO with new ARC files
                    await UpdateIsoWithArcFilesAsync(arcFiles);
                }

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

            ReportProgress("Processing XML files...", 0, xmlFiles.Count);

            for (int i = 0; i < xmlFiles.Count; i++)
            {
                var xmlFile = xmlFiles[i];
                var arcName = GetArcNameFromXml(xmlFile);
                
                if (string.IsNullOrEmpty(arcName))
                    continue;

                ReportProgress($"Creating ARC file: {arcName}", i + 1, xmlFiles.Count);

                try
                {
                    var success = await CreateArcFileAsync(arcName);
                    if (success)
                    {
                        processedArcFiles.Add(arcName);
                        // Mark XML as successfully processed
                        MarkXmlAsProcessed(xmlFile);
                    }
                    else
                    {
                        failedXmlFiles.Add(xmlFile);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to process XML file {xmlFile}: {ex.Message}");
                    failedXmlFiles.Add(xmlFile);
                }
            }

            // Update failed files list for retry on next save
            if (failedXmlFiles.Count > 0)
            {
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

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    
                    // Wait for completion with timeout
                    var completed = await Task.Run(() => process.WaitForExit(30000)); // 30 second timeout
                    
                    if (!completed)
                    {
                        process.Kill();
                        return false;
                    }

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to create ARC file {arcName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Update ISO with processed ARC files
        /// </summary>
        private async Task UpdateIsoWithArcFilesAsync(List<string> arcFiles)
        {
            if (arcFiles.Count == 0)
                return;

            ReportProgress("Updating ISO...", 0, arcFiles.Count);

            for (int i = 0; i < arcFiles.Count; i++)
            {
                var arcFile = arcFiles[i];
                ReportProgress($"Updating ISO: {arcFile}", i + 1, arcFiles.Count);

                try
                {
                    var success = await ReplaceArcInIsoAsync(arcFile);
                    if (success)
                    {
                        // Mark ARC as successfully processed
                        MarkArcAsProcessed(arcFile);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to update ISO with ARC file {arcFile}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Replace ARC file in ISO using replace-specific.py script
        /// </summary>
        private async Task<bool> ReplaceArcInIsoAsync(string arcName)
        {
            try
            {
                // Determine the full path to the ARC file in 3_patched directory
                string arcFilePath = null;
                
                // Check facechat directory first
                var facechatPath = Path.Combine(_projectRootPath, "3_patched", "PSP_GAME", "USRDIR", "facechat", arcName);
                if (File.Exists(facechatPath))
                {
                    arcFilePath = facechatPath;
                }
                else
                {
                    // Check npc directory
                    var npcPath = Path.Combine(_projectRootPath, "3_patched", "PSP_GAME", "USRDIR", "npc", arcName);
                    if (File.Exists(npcPath))
                    {
                        arcFilePath = npcPath;
                    }
                }

                if (string.IsNullOrEmpty(arcFilePath))
                    return false;

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

                using (var process = new Process { StartInfo = startInfo })
                {
                    process.Start();
                    
                    // Wait for completion with timeout
                    var completed = await Task.Run(() => process.WaitForExit(60000)); // 60 second timeout
                    
                    if (!completed)
                    {
                        process.Kill();
                        return false;
                    }

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to replace ARC file {arcName} in ISO: {ex.Message}");
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
