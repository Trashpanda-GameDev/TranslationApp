using System;
using System.Collections.Generic;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using TranslationLib;
using PackingLib;
using System.Reflection;
using System.Threading.Tasks;

namespace TranslationApp
{
    public partial class fMain : Form
    {
        private static Config config;
        private GameConfig gameConfig;
        private static TranslationProject Project;
        private static PackingProject PackingAssistant;
        private static List<XMLEntry> CurrentTextList;
        private static List<XMLEntry> CurrentSpeakerList;
        private static List<EntryFound> ListSearch;
        private static List<EntryFound> OtherTranslations;
        private static List<XMLEntry> ContextTranslations;
        private Dictionary<string, Color> ColorByStatus;
        private string gameName;
        private int nbJapaneseDuplicate;
        private static string windowName;
        FormWindowState LastWindowState = FormWindowState.Minimized;

        // Auto-apply service
        private AutoApplyService autoApplyService;

        // RM2 Menu Items
        private System.Windows.Forms.ToolStripMenuItem tsRM2ApplyTranslations;
        private System.Windows.Forms.ToolStripMenuItem tsRM2ReplaceAllFiles;

        private readonly string MULTIPLE_STATUS = "<Multiple Status>";
        private readonly string MULTIPLE_SELECT = "<Multiple Entries Selected>";

        struct ProjectEntry
        {
            public string shortName, fullName, folder;
            public ProjectEntry(string sname, string fname, string dir)
            {
                shortName = sname;
                fullName = fname;
                folder = dir;
            }
        }

        private static readonly ProjectEntry[] Projects = new ProjectEntry[]
        {
            new ProjectEntry("NDX", "Narikiri Dungeon X", "2_translated"),
            new ProjectEntry("TOR", "Tales of Rebirth", "2_translated"),
            new ProjectEntry("TOH", "Tales of Hearts (DS)", "2_translated"),
            new ProjectEntry("RM2", "Tales of the World: Radiant Mythology 2", "2_translated"),
        };

        public fMain()
        {
            InitializeComponent();
            // Use reflection to allow spliters to ignore the Form size
            typeof(Splitter).GetField("minExtra", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(splitter1, -10000);
            typeof(Splitter).GetField("minExtra", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(splitter2, -10000);
            var gitInfo = Assembly.GetExecutingAssembly().GetType("GitVersionInformation");
            var ver = gitInfo.GetField("FullSemVer").GetValue(null);
            var sha = gitInfo.GetField("ShortSha").GetValue(null);
#if DEBUG
            Text = "Translation App v" + ver + " (commit: " + sha + ")";
#else
            Text = "Translation App v" + ver;
#endif
            windowName = Text;
        }

        private void fMain_Load(object sender, EventArgs e)
        {
            cbLanguage.Text = "English (if available)";
            CreateColorByStatusDictionnary();
            PopulateProjectTypes();
            InitialiseStatusText();
            ChangeEnabledProp(false);

            config = new Config();
            config.Load();
            PackingAssistant = new PackingProject();



            // Set initial visibility state
            UpdateOptionsVisibility();

            // Automatically load the last used project on startup
            AutoLoadLastProject();
        }

        private void AutoLoadLastProject()
        {
            try
            {
                // Check if we have any saved game configurations
                if (config?.GamesConfigList != null && config.GamesConfigList.Count > 0)
                {
                    // Find the most recently used project by timestamp
                    var lastUsedProject = config.GamesConfigList
                        .Where(g => !string.IsNullOrEmpty(g.FolderPath) && Directory.Exists(g.FolderPath))
                        .OrderByDescending(g => g.LastTimeLoaded) // Order by most recently accessed
                        .FirstOrDefault();

                    if (lastUsedProject != null)
                    {
                        // Find the corresponding ProjectEntry
                        ProjectEntry? projectEntry = null;
                        foreach (var project in Projects)
                        {
                            if (project.shortName == lastUsedProject.Game)
                            {
                                projectEntry = project;
                                break;
                            }
                        }
                        
                        if (projectEntry.HasValue)
                        {
                            // Load the last used project
                            LoadLastFolder(projectEntry.Value.shortName);
                            
                            // Update UI if project was successfully loaded
                            if (Project != null && Project.CurrentFolder != null)
                            {
                                textPreview1.ChangeImage(projectEntry.Value.shortName);
                                UpdateTitle(projectEntry.Value.fullName);

                                // add listener to shown event
                                this.Shown += (s, e) => {
                                    ShowAutoLoadDialog(projectEntry.Value.fullName, lastUsedProject.FolderPath, lastUsedProject);
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Auto-load error: {ex.Message}");
                // Continue with normal startup - user can manually load projects
            }
        }

        private void ShowAutoLoadDialog(string projectName, string folderPath, GameConfig gameConfig)
        {
            // Check if user has chosen not to show this message
            if (!gameConfig.ShowAutoLoadMessage)
            {
                return;
            }

            // Create custom dialog with checkbox
            using (var form = new Form())
            {
                form.Text = "Auto-loaded";
                form.Size = new System.Drawing.Size(400, 200);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var label = new Label
                {
                    Text = $"Project: \n{projectName}\n\nFolder: \n{folderPath}",
                    Location = new System.Drawing.Point(20, 20),
                    Size = new System.Drawing.Size(350, 80),
                    AutoSize = false
                };

                var checkBox = new CheckBox
                {
                    Text = "Don't show this message again",
                    Location = new System.Drawing.Point(20, 110),
                    Size = new System.Drawing.Size(200, 20)
                };

                var button = new Button
                {
                    Text = "OK",
                    Location = new System.Drawing.Point(300, 130),
                    Size = new System.Drawing.Size(75, 23),
                    DialogResult = DialogResult.OK
                };

                form.Controls.Add(label);
                form.Controls.Add(checkBox);
                form.Controls.Add(button);

                form.ShowDialog();

                // Save the user's preference
                if (checkBox.Checked)
                {
                    gameConfig.ShowAutoLoadMessage = false;
                    config.Save();
                }
            }
        }

        private void PopulateProjectTypes()
        {
            List<ToolStripMenuItem> items = new List<ToolStripMenuItem>();
            foreach (ProjectEntry pe in Projects)
            {
                var item = new ToolStripMenuItem();
                item.Name = "tsi" + pe.shortName;
                item.Text = pe.shortName;
                item.DropDownItems.Add(new ToolStripMenuItem("Open Last Folder") { Tag = pe });
                item.DropDownItems[0].Click += new EventHandler(LoadLastFolder_Click);
                item.DropDownItems.Add(new ToolStripMenuItem("Open New Folder") { Tag = pe });
                item.DropDownItems[1].Click += new EventHandler(LoadNewFolder_Click);
                items.Add(item);
            }
            translationToolStripMenuItem.DropDownItems.AddRange(items.ToArray());
        }

         private void LoadNewFolder_Click(object sender, EventArgs e)
        {
            var previousProject = Project;

            ToolStripMenuItem clickedItem = (ToolStripMenuItem)sender;
            ProjectEntry pe = (ProjectEntry)clickedItem.Tag;
            LoadProjectFolder(pe.shortName, pe.folder);
            
            // Only update UI if a new project was actually loaded
            if (Project != null && Project.CurrentFolder != null && Project != previousProject)
            {
                textPreview1.ChangeImage(pe.shortName);
                UpdateTitle(pe.fullName);
            }
        }

        private void LoadLastFolder_Click(object sender, EventArgs e)
        {
            var previousProject = Project;

            ToolStripMenuItem clickedItem = (ToolStripMenuItem)sender;
            ProjectEntry pe = (ProjectEntry)clickedItem.Tag;
            LoadLastFolder(pe.shortName);
            
            // Only update UI if project was successfully loaded
            if (Project != null && Project.CurrentFolder != null && Project != previousProject)
            {
                textPreview1.ChangeImage(pe.shortName);
                UpdateTitle(pe.fullName);
            }
        }

        private void openFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string p = TryLoadFolder(GetFolderPath(), false);
            if (p != null)
            {
                UpdateTitle($"Single folder {Path.GetDirectoryName(p)}");
            }
        }

        private void UpdateTitle(string name)
        {
            if (Project.CurrentFolder == null || string.IsNullOrWhiteSpace(name))
            {
                Text = windowName;
            }
            else
            {
                Text = windowName + " | " + name;
            }
        }

        private void CreateColorByStatusDictionnary()
        {
            ColorByStatus = new Dictionary<string, Color>
            {
                { "To Do", Color.White },
                { "Editing", Color.FromArgb(162, 255, 255) }, // Light Cyan
                { "Proofreading", Color.FromArgb(255, 102, 255) }, // Magenta
                { "Problematic", Color.FromArgb(255, 255, 162) }, // Light Yellow
                { "Done", Color.FromArgb(162, 255, 162) }, // Light Green
            };
        }

        private void InitialiseStatusText()
        {
            lNbToDo.Text = "";
            lNbEditing.Text = "";
            lNbProb.Text = "";
            lNbProof.Text = "";
            lNbDone.Text = "";

            lNbToDoSect.Text = "";
            lNbProbSect.Text = "";
            lNbEditingSect.Text = "";
            lNbProofSect.Text = "";
            lNbDoneSect.Text = "";
        }

        private void ChangeEnabledProp(bool status)
        {
            cbFileType.Enabled = status;
            cbFileList.Enabled = status;
            cbLanguage.Enabled = status;
            cbStatus.Enabled = status;
            tbEnglishText.Enabled = status;
            tbJapaneseText.Enabled = status;
            tbFriendlyName.Enabled = status;
            tbSectionName.Enabled = status;
            tbNoteText.Enabled = status;
            tabSearchMass.Enabled = status;

            lbEntries.Enabled = status;

            //Checked List
            cbToDo.Enabled = status;
            cbEditing.Enabled = status;
            cbProof.Enabled = status;
            cbDone.Enabled = status;
            cbProblematic.Enabled = status;
            cbDone.Enabled = status;
            cbEmpty.Enabled = status;

            //Button
            bSaveAll.Enabled = status;
            btnRefresh.Enabled = status;
            btnSaveFile.Enabled = status;

            //Panel
            panelNb1.Enabled = status;
            panelNb2.Enabled = status;
        }

        private void DrawEntries(DrawItemEventArgs e, List<XMLEntry> EntryList, bool displaySection)
        {
            bool isSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);

            //Draw only if elements are present in the listbox
            if (e.Index > -1)
            {
                //Regardless of text, draw elements close together
                //and use the intmax size as per the docs
                TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                Size proposedSize = new Size(int.MaxValue, int.MaxValue);

                //Grab the current entry to draw
                XMLEntry entry = EntryList[e.Index];

                // Background item brush
                SolidBrush backgroundBrush = new SolidBrush(isSelected ? SystemColors.Highlight : ColorByStatus[entry.Status]);

                // Text colors
                Color regularColor = e.ForeColor;
                Color tagColor = isSelected ? Color.Orange : Color.Blue;

                // Draw the background
                e.Graphics.FillRectangle(backgroundBrush, e.Bounds);

                // Add separators for each entry
                e.Graphics.DrawLine(new Pen(Color.DimGray, 1.5f), new Point(0, e.Bounds.Bottom - 1), new Point(e.Bounds.Width, e.Bounds.Bottom - 1));
                e.Graphics.DrawLine(new Pen(Color.DimGray, 1.5f), new Point(0, e.Bounds.Top - 1), new Point(e.Bounds.Width, e.Bounds.Top - 1));

                Font normalFont = new Font("Arial", 8, FontStyle.Regular);
                Font boldFont = new Font("Arial", 8, FontStyle.Bold);

                string text = GetTextBasedLanguage(e.Index, EntryList);
                Point startPoint = new Point(3, e.Bounds.Y + 3);

                //0. Add Section if needed
                if (displaySection)
                {

                    EntryFound entryFound = ListSearch[e.Index];
                    string sectionDetail = $"{entryFound.Folder} - " +
                $"{Project.GetFolderByName(entryFound.Folder).XMLFiles[entryFound.FileId].Name} - " +
                $"{entryFound.Section} - {entry.Id}";

                    SolidBrush backgroundBrushSection = new SolidBrush(Color.LightGray);
                    Size mySize = TextRenderer.MeasureText(e.Graphics, sectionDetail, normalFont, proposedSize, flags);
                    e.Graphics.FillRectangle(backgroundBrushSection, e.Bounds.X, e.Bounds.Y, e.Bounds.Width, 19);
                    TextRenderer.DrawText(e.Graphics, sectionDetail, boldFont, startPoint, Color.Black, flags);
                    startPoint.Y += 16;


                    e.Graphics.DrawLine(new Pen(Color.LightGray, 1.5f), new Point(0, startPoint.Y), new Point(e.Bounds.Width, startPoint.Y));
                    startPoint.Y += 3;
                }


                //1. Add Speaker name
                if (EntryList[e.Index].SpeakerId != null)
                {
                    TextRenderer.DrawText(e.Graphics, EntryList[e.Index].SpeakerName, boldFont, startPoint, tagColor, flags);
                    startPoint.Y += 13;
                }

                //2. Split based on the line breaks
                if (!string.IsNullOrEmpty(text))
                    DrawLines(e, text, ref startPoint, boldFont, tagColor, normalFont, regularColor, proposedSize, flags);


                // Clean up
                backgroundBrush.Dispose();
            }

            e.DrawFocusRectangle();
        }

        private void DrawSearchEntries(DrawItemEventArgs e, List<EntryFound> EntryList, bool highlightSearch)
        {
            bool isSelected = ((e.State & DrawItemState.Selected) == DrawItemState.Selected);

            //Draw only if elements are present in the listbox
            if (e.Index > -1)
            {
                //Regardless of text, draw elements close together
                //and use the intmax size as per the docs
                TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
                Size proposedSize = new Size(int.MaxValue, int.MaxValue);

                //Grab the current entry to draw
                EntryFound entryFound = EntryList[e.Index];

                // Background item brush
                SolidBrush backgroundBrush = new SolidBrush(isSelected ? SystemColors.Highlight : ColorByStatus["To Do"]);

                // Text colors
                Color regularColor = e.ForeColor;
                Color hightlightSearch = Color.Orange;
                Color tagColor = isSelected ? Color.Orange : Color.Blue;

                // Draw the background
                e.Graphics.FillRectangle(backgroundBrush, e.Bounds);

                // Add separators for each entry
                e.Graphics.DrawLine(new Pen(Color.DimGray, 1.5f), new Point(0, e.Bounds.Bottom - 1), new Point(e.Bounds.Width, e.Bounds.Bottom - 1));
                e.Graphics.DrawLine(new Pen(Color.DimGray, 1.5f), new Point(0, e.Bounds.Top - 1), new Point(e.Bounds.Width, e.Bounds.Top - 1));

                Font normalFont = new Font("Arial", 8, FontStyle.Regular);
                Font boldFont = new Font("Arial", 8, FontStyle.Bold);

                string text = GetTextBasedLanguage(e.Index, EntryList.Select(x => x.Entry).ToList());
                Point startPoint = new Point(3, e.Bounds.Y + 3);

                //0. Add Section if needed
                string sectionDetail = $"{entryFound.Folder} - " +
            $"{Project.GetFolderByName(entryFound.Folder).XMLFiles[entryFound.FileId].Name} - " +
            $"{entryFound.Section} - {entryFound.Id}";

                SolidBrush backgroundBrushSection = new SolidBrush(Color.LightGray);
                Size mySize = TextRenderer.MeasureText(e.Graphics, sectionDetail, normalFont, proposedSize, flags);
                e.Graphics.FillRectangle(backgroundBrushSection, e.Bounds.X, e.Bounds.Y, e.Bounds.Width, 19);
                TextRenderer.DrawText(e.Graphics, sectionDetail, boldFont, startPoint, Color.Black, flags);
                startPoint.Y += 16;


                e.Graphics.DrawLine(new Pen(Color.LightGray, 1.5f), new Point(0, startPoint.Y), new Point(e.Bounds.Width, startPoint.Y));
                startPoint.Y += 3;



                //1. Add Speaker name
                if (entryFound.Entry.SpeakerId != null)
                {
                    TextRenderer.DrawText(e.Graphics, entryFound.Entry.SpeakerName, boldFont, startPoint, tagColor, flags);
                    startPoint.Y += 13;
                }

                //2. Split based on the line breaks
                if (!string.IsNullOrEmpty(text))
                {

                    //Split based on searched item
                    string pattern = $@"({tbSearch.Text})";
                    List<string> result = Regex.Split(text, pattern, RegexOptions.IgnoreCase).Where(x => x != "").ToList();
                    List<Color> textColors = result.Select(x => x.Equals(tbSearch.Text, StringComparison.OrdinalIgnoreCase) ? Color.OrangeRed : e.ForeColor).ToList();
                    List<Font> textFont = result.Select(x => x.Equals(tbSearch.Text, StringComparison.OrdinalIgnoreCase) ? boldFont : normalFont).ToList();

                    for (int i = 0; i < result.Count; i++)
                    {
                        DrawLines(e, result[i], ref startPoint, normalFont, tagColor, textFont[i], textColors[i], proposedSize, flags);
                    }

                }

                // Clean up
                backgroundBrush.Dispose();
            }

            e.DrawFocusRectangle();

        }

        private void DrawLines(DrawItemEventArgs e, string text, ref Point startPoint, Font tagFont, Color tagColor, Font regularFont, Color regularColor, Size proposedSize, TextFormatFlags flags)
        {
            Size mySize;

            string[] lines = Regex.Split(text, "\\r*\\n", RegexOptions.IgnoreCase);

            //Starting point for drawing, a little offsetted
            //in order to not touch the borders
            //Point startPoint = new Point(3, e.Bounds.Y + 3);

            for (int i = 0; i < lines.Length; i++)
            {

                //3. Split based on the different tags
                //Split the text based on the Tags < xxx >
                string line = lines[i];
                string pattern = @"(<[\w/]+:?\w+>)";
                string[] result = Regex.Split(line, pattern, RegexOptions.IgnoreCase).Where(x => x != "").ToArray();
                //We need to loop over each element to adjust the color
                foreach (string element in result)
                {
                    if (element[0] == '<')
                    {
                        mySize = TextRenderer.MeasureText(e.Graphics, element, tagFont, proposedSize, flags);

                        TextRenderer.DrawText(e.Graphics, element, tagFont, startPoint, tagColor, flags);
                        startPoint.X += mySize.Width;
                    }
                    else
                    {
                        mySize = TextRenderer.MeasureText(e.Graphics, element, regularFont, proposedSize, flags);

                        TextRenderer.DrawText(e.Graphics, element, regularFont, startPoint, regularColor, flags);
                        startPoint.X += mySize.Width;
                    }
                }

                // Update HorizonalExtent so we can have horizontal scrolling
                if (lbEntries.HorizontalExtent < startPoint.X)
                {
                    lbEntries.HorizontalExtent = startPoint.X + 20;
                }

                if (i < lines.Length - 1)
                {
                    startPoint.Y += 13;
                    startPoint.X = 3;
                }
            }
        }


        //Draw entries with multiline and font color changed
        private void lbEntries_DrawItem(object sender, DrawItemEventArgs e)
        {
            DrawEntries(e, CurrentTextList, false);
        }

        private void lbSpeaker_DrawItem(object sender, DrawItemEventArgs e)
        {
            DrawEntries(e, CurrentSpeakerList, false);
        }

        private void ShowOtherTranslations()
        {
            if (tbJapaneseText.Text != "")
            {
                string translation = string.IsNullOrEmpty(tbEnglishText.Text) ? tbJapaneseText.Text : tbEnglishText.Text;
                translation = translation.Replace("\r\n", "\n");
                string jptext = tbJapaneseText.Text.Replace("\r\n", "\n");
                List<EntryFound> Entryfound = FindOtherTranslations("All", jptext, "Japanese", true, false, false);
                OtherTranslations = Entryfound.Where(x => x.Entry.JapaneseText.Replace("\r\n", "\n") == jptext && (x.Entry.EnglishText ?? string.Empty).Replace("\r\n", "\n") != translation).ToList();

                string cleanedString = tbEnglishText.Text.Replace("\r\n", "").Replace(" ", "");
                List<EntryFound> DifferentLineBreak = Entryfound.Where(x => x.Entry.EnglishText != null).
                    Where(x => x.Entry.EnglishText.Replace("\n", "").Replace(" ", "") == cleanedString && x.Entry.EnglishText != translation).ToList();
                DifferentLineBreak.ForEach(x => x.Category = "Linebreak");

                OtherTranslations.AddRange(DifferentLineBreak);
                lNbOtherTranslations.ForeColor = OtherTranslations.Count > 0 ? Color.Red : Color.Green;
                lLineBreak.ForeColor = DifferentLineBreak.Count > 0 ? Color.Red : Color.Green;
                int distinctCount = OtherTranslations.Select(x => x.Entry.EnglishText).Distinct().Count();

                if (nbJapaneseDuplicate > 0)
                {
                    lNbOtherTranslations.Text = $"({distinctCount} other/missing translation(s) found)";
                    lLineBreak.Text = $"({DifferentLineBreak.Count} linebreak(s) different found)";
                }
                else
                {
                    lNbOtherTranslations.Text = "";
                    lLineBreak.Text = "";
                }

                lbDistinctTranslations.DataSource = OtherTranslations.Select(x => $"{x.Folder} - " +
                $"{Project.GetFolderByName(x.Folder).XMLFiles[Convert.ToInt32(x.FileId)].Name} - " +
                $"{x.Section} - {x.Entry.EnglishText}").ToList();
            }
        }
        private void lbEntries_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadEntryData(lbEntries);
            ShowOtherTranslations();
        }

        private List<EntryFound> FindOtherTranslations(string folderSearch, string textToFind, string language, bool matchWholeEntry, bool matchCase, bool matchWholeWord)
        {
            List<EntryFound> res = new List<EntryFound>();
            if (folderSearch != "All")
            {
                XMLFolder folder = Project.XmlFolders.Where(x => String.Equals(x.Name, folderSearch, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (folder != null)
                {
                    res = folder.SearchJapanese(textToFind, matchWholeEntry, matchCase, matchWholeWord, language);
                }
            }
            else
            {
                foreach (XMLFolder folder in Project.XmlFolders)
                {
                    res.AddRange(folder.SearchJapanese(textToFind, matchWholeEntry, matchCase, matchWholeWord, language));
                }
            }
            return res;
        }

        private void LoadEntryData(ListBox lb)
        {
            tbEnglishText.TextChanged -= tbEnglishText_TextChanged;

            if (lb.SelectedIndices.Count > 1)
            {
                tbJapaneseText.Text = MULTIPLE_SELECT;
                tbJapaneseText.Enabled = false;

                tbEnglishText.Text = MULTIPLE_SELECT;
                tbEnglishText.Enabled = false;
                string st = ((XMLEntry)lb.SelectedItems[0]).Status;
                foreach (XMLEntry e in lb.SelectedItems)
                {
                    if (st != e.Status)
                    {
                        if (!cbStatus.Items.Contains(MULTIPLE_STATUS))
                        {
                            cbStatus.Items.Add(MULTIPLE_STATUS);
                        }
                        cbStatus.Text = MULTIPLE_STATUS;
                        return;
                    }
                }
                cbStatus.Text = st;
            }
            else
            {
                if (cbStatus.Items.Contains(MULTIPLE_STATUS))
                {
                    cbStatus.Items.Remove(MULTIPLE_STATUS);
                }

                tbJapaneseText.Text = string.Empty;
                tbEnglishText.Text = string.Empty;

                XMLEntry currentEntry = (XMLEntry)lb.SelectedItem;
                if (currentEntry == null)
                {
                    tbJapaneseText.Enabled = false;
                    tbEnglishText.Enabled = false;
                    cbStatus.Enabled = false;
                    cbEmpty.Enabled = false;
                    return;
                }
                else
                {
                    tbJapaneseText.Enabled = true;
                    tbEnglishText.Enabled = true;
                    cbStatus.Enabled = true;
                    cbEmpty.Enabled = true;
                }

                TranslationEntry TranslationEntry;
                nbJapaneseDuplicate = 0;
                if (currentEntry.JapaneseText == null)

                    TranslationEntry = new TranslationEntry { EnglishTranslation = "" };
                else
                {
                    foreach (XMLFolder folder in Project.XmlFolders)
                    {
                        folder.Translations.TryGetValue(currentEntry.JapaneseText, out TranslationEntry);

                        if (TranslationEntry != null)
                            nbJapaneseDuplicate += TranslationEntry.Count;
                    }
                    nbJapaneseDuplicate -= 1;
                }


                if (nbJapaneseDuplicate > 0)
                    lblJapanese.Text = $@"Japanese ({nbJapaneseDuplicate} duplicate(s) found)";
                else
                    lblJapanese.Text = $@"Japanese";

                if (currentEntry.JapaneseText != null)
                    tbJapaneseText.Text = currentEntry.JapaneseText.Replace("\r", "").Replace("\n", Environment.NewLine);
                if (currentEntry.EnglishText != null)
                    tbEnglishText.Text = currentEntry.EnglishText.Replace("\r", "").Replace("\n", Environment.NewLine);
                if (tbNoteText != null)
                    tbNoteText.Text = currentEntry.Notes;

                cbEmpty.Checked = currentEntry.EnglishText?.Equals("") ?? false;

                cbStatus.Text = currentEntry._Status; // Need the modified name (bandaid)
            }
            textPreview1.ReDraw(tbEnglishText.Text);
            tbEnglishText.TextChanged += tbEnglishText_TextChanged;
        }

        private void bSave_Click(object sender, EventArgs e)
        {
            int count = 0;
            var savedFiles = new List<string>();
            
            foreach (var folder in Project.XmlFolders)
            {
                // Collect files that need saving BEFORE calling SaveChanged
                var filesToSave = new List<string>();
                foreach (var file in folder.XMLFiles)
                {
                    if (file.needsSave && !string.IsNullOrEmpty(file.FilePath))
                    {
                        filesToSave.Add(file.FilePath);
                    }
                }
                
                // Save the files that need saving
                count += folder.SaveChanged();
                
                // Add the files that were actually saved to our list
                savedFiles.AddRange(filesToSave);
            }
            
            // Show save status in the status area instead of message box
            if (lErrors != null)
            {
                lErrors.Text = $"{count} XML files saved successfully";
                lErrors.ForeColor = Color.Green;
            }

            UpdateDisplayedEntries();
            UpdateStatusData();

            // Trigger auto-apply for saved files
            if (savedFiles.Count > 0)
            {
                TriggerAutoApply(savedFiles);
            }
        }


        private void trackBarAlign_ValueChanged(object sender, EventArgs e)
        {
            Invalidate();
            string val = textPreview1.DoLineBreak(tbEnglishText.Text, trackBarAlign.Value * 30);
            tbEnglishText.Text = val;
        }

        private string GetTextBasedLanguage(int entryIndex, List<XMLEntry> EntryList)
        {
            var myEntry = EntryList[entryIndex];
            if (cbLanguage.Text == "Japanese")
                return myEntry.JapaneseText;
            else
                return myEntry.EnglishText == null ? myEntry.JapaneseText : myEntry.EnglishText;
        }

        public string GetFolderPath()
        {
            // Use modern Windows API folder picker for better user experience
            try
            {
                return GetModernWindowsFolderPicker();
            }
            catch
            {
                // Fallback to Windows Forms FolderBrowserDialog
                return GetFallbackFolderPicker();
            }
        }

        private string GetModernWindowsFolderPicker()
        {
            try
            {
                // Try to use the modern Windows Vista+ folder picker
                using (var openFileDialog = new OpenFileDialog())
                {
                    openFileDialog.ValidateNames = false;
                    openFileDialog.CheckFileExists = false;
                    openFileDialog.CheckPathExists = true;
                    openFileDialog.FileName = "Select Folder";
                    openFileDialog.Title = "Select your project folder";
                    
                    // Set initial directory to parent of last used path (go up one level)
                    if (!string.IsNullOrEmpty(config?.GamesConfigList?.LastOrDefault()?.LastFolderPath))
                    {
                        string lastPath = config.GamesConfigList.Last().LastFolderPath;
                        openFileDialog.InitialDirectory = lastPath;
                    }
                    else
                    {
                        openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
                    }

                    // Show the dialog and handle the result
                    DialogResult result = openFileDialog.ShowDialog();
                    if (result == DialogResult.OK)
                    {
                        string folderPath = Path.GetDirectoryName(openFileDialog.FileName);
                        if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                            return folderPath;
                    }
                    // If user cancelled (DialogResult.Cancel), return empty string
                    return "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Modern folder picker error: {ex.Message}");
                // Fall through to fallback method
            }

            return "";
        }

        private string GetFallbackFolderPicker()
        {
            try
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select your project folder";
                    
                    // Show the dialog and handle the result
                    DialogResult result = fbd.ShowDialog();
                    if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        // Verify the selected path actually exists
                        if (Directory.Exists(fbd.SelectedPath))
                            return fbd.SelectedPath;
                    }
                    // If user cancelled (DialogResult.Cancel), return empty string
                    return "";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fallback folder picker error: {ex.Message}");
            }

            return "";
        }

         private void LoadProjectFolder(string gameName, string path)
        {
            this.gameName = gameName; // Assign to class field
            lbEntries.BorderStyle = BorderStyle.FixedSingle;
            
            // Get the folder path from user
            string selectedFolder = GetFolderPath();
            
            // Check if user cancelled the folder picker
            if (string.IsNullOrEmpty(selectedFolder))
            {
                // User cancelled, don't proceed
                return;
            }
            
            var loadedFolder = TryLoadFolder(Path.Combine(selectedFolder, path), gameName.Equals("NDX"));
            
            // Check if folder loading failed
            if (loadedFolder == null)
            {
                // Folder loading failed, don't proceed
                return;
            }
            
            gameConfig = config.GamesConfigList.Where(x => x.Game == gameName).FirstOrDefault();

            if (gameConfig == null)
            {
                GameConfig newConfig = new GameConfig(gameName);
                newConfig.FolderPath = loadedFolder;
                newConfig.LastFolderPath = selectedFolder; // Store the parent folder, not the subfolder
                newConfig.Game = gameName;
                newConfig.LastTimeLoaded = DateTime.Now; // Track when this project was accessed
                config.GamesConfigList.Add(newConfig);
                gameConfig = config.GetGameConfig(gameName);
            }
            else
            {
                gameConfig.FolderPath = loadedFolder;
                gameConfig.LastFolderPath = selectedFolder; // Store the parent folder, not the subfolder
                gameConfig.Game = gameName;
                gameConfig.LastTimeLoaded = DateTime.Now; // Update access timestamp
            }

            config.Save();
            UpdateOptionsVisibility();
        }
        private void LoadLastFolder(string gameName)
        {
            this.gameName = gameName; // Assign to class field
            var gameConfig = config.GetGameConfig(gameName);
            if (gameConfig != null)
            {
                TryLoadFolder(gameConfig.FolderPath, false);
                gameConfig.LastTimeLoaded = DateTime.Now; // Update access timestamp
                config.Save(); // Save the updated timestamp
                UpdateOptionsVisibility();
                
            }
            else
                MessageBox.Show("The game you are trying to load is not inside the configuration file,\nplease load a new folder.");
        }

        public string TryLoadFolder(string path, bool legacy)
        {
            if (Directory.Exists(path))
            {
                LoadFolder(path, legacy);
                return path;
            }

            MessageBox.Show("Are you sure you selected the right folder?\n" +
                            "The folder you choose doesn't represent a valid Tales of Repo.\n" +
                            $"It should have the Data folder in it.\nPath {path} not valid.");
            return null;
        }

        private void LoadFolder(string path, bool legacy)
        {
            DisableEventHandlers();

            var folderIncluded = new List<string>();
            foreach (string p in Directory.GetDirectories(path))
            {
                folderIncluded.Add(new DirectoryInfo(p).Name);
            }

            Project = new TranslationProject(path, folderIncluded, legacy);

            if (Project.CurrentFolder == null)
            {
                MessageBox.Show("Are you sure you selected the right folder?\n" +
                            "The folder you have chosen doesn't contain any subfolders\n" +
                            "or they are empty, please try again.");
                return;
            }

            CurrentTextList = Project.CurrentFolder.CurrentFile.CurrentSection.Entries;
            CurrentSpeakerList = Project.CurrentFolder.CurrentFile.Speakers;
            cbFileType.DataSource = Project.GetFolderNames().OrderByDescending(x => x).ToList();
            cbFileList.DataSource = Project.CurrentFolder.FileList();
            cbSections.DataSource = Project.CurrentFolder.CurrentFile.GetSectionNames();
            cbFileList.SelectedIndex = 0;

            UpdateDisplayedEntries();
            UpdateStatusData();

            ChangeEnabledProp(true);
            EnableEventHandlers();
            
            // Restore last used file selections if available
            RestoreLastUsedFileSelections();
            
            // Save initial status filter states if this is the first time loading this project
            SaveInitialStatusFilterStates();

            // Initialize auto-apply service if this is an RM2 project
            InitializeAutoApplyService();
        }

        private void DisableEventHandlers()
        {
            cbFileType.TextChanged -= cbFileType_TextChanged;
            cbFileList.DrawItem -= cbFileList_DrawItem;
            cbFileList.TextChanged -= cbFileList_TextChanged;
            cbSections.TextChanged -= cbSections_TextChanged;
            cbSections.SelectedIndexChanged -= cbSections_SelectedIndexChanged;
        }

        private void EnableEventHandlers()
        {
            cbFileType.TextChanged += cbFileType_TextChanged;
            cbFileList.DrawItem += cbFileList_DrawItem;
            cbFileList.TextChanged += cbFileList_TextChanged;
            cbSections.TextChanged += cbSections_TextChanged;
            cbSections.SelectedIndexChanged += cbSections_SelectedIndexChanged;
        }

        private List<XMLEntry> getSkitNameList()
        {
            XMLFile slps = Project.XmlFolders[1].XMLFiles.FirstOrDefault(x => x.Name == "SLPS");
            XMLSection section = slps?.Sections.FirstOrDefault(x => x.Name.StartsWith("Skit"));
            List<XMLEntry> r = section?.Entries;
            return r ?? new List<XMLEntry>();
        }

        private void cbFileType_TextChanged(object sender, EventArgs e)
        {
            if (cbFileType.SelectedItem.ToString() != string.Empty)
            {
                Project.SetCurrentFolder(cbFileType.SelectedItem.ToString());
                List<string> filelist = Project.CurrentFolder.FileList();
                
                // Save the current file type selection
                SaveCurrentFileSelections();

                if (cbFileType.SelectedItem.ToString().Equals("Menu", StringComparison.InvariantCultureIgnoreCase))
                {
                    cbFileList.DataSource = filelist;
                }
                else if (cbFileType.SelectedItem.ToString() == "Skits")
                {
                    List<XMLEntry> names = getSkitNameList();
                    if (names.Count != 1157)
                    {
                        cbFileList.DataSource = filelist.Select(x => x + ".xml").ToList();
                    }
                    else
                    {
                        for (int i = 0, j = 0; i < filelist.Count; i++)
                        {
                            if (((i > 1072) && (i < 1082)) || ((i > 1092) && (i < 1099)))
                            {
                                j++;
                                filelist[i] += " | NO NAME";
                                continue;
                            }
                            filelist[i] = filelist[i] + " | " + (names[i - j].EnglishText ?? names[i - j].JapaneseText);
                        }
                        cbFileList.DataSource = filelist;
                    }
                }
                else
                {
                    for (int i = 0; i < filelist.Count; i++)
                    {
                        string fname = Project.CurrentFolder.XMLFiles[i].FriendlyName ?? "NO NAME";
                        filelist[i] = filelist[i] + " | " + fname;
                    }
                    cbFileList.DataSource = filelist;
                }


                cbSections.DataSource = Project.CurrentFolder.CurrentFile.GetSectionNames();
                UpdateStatusData();
            }
        }

        private void UpdateDisplayedEntries()
        {
            var checkedFilters = new List<string>
            {
                cbToDo.Checked ? "To Do" : string.Empty,
                cbProof.Checked ? "Proofreading" : string.Empty,
                cbEditing.Checked ? "Editing" : string.Empty,
                cbProblematic.Checked ? "Problematic" : string.Empty,
                cbDone.Checked ? "Done" : string.Empty
            };
            
            System.Diagnostics.Debug.WriteLine($"UpdateDisplayedEntries - Status filters: ToDo={cbToDo.Checked}, Proof={cbProof.Checked}, Editing={cbEditing.Checked}, Problematic={cbProblematic.Checked}, Done={cbDone.Checked}");
            System.Diagnostics.Debug.WriteLine($"UpdateDisplayedEntries - Checked filters: {string.Join(", ", checkedFilters.Where(f => !string.IsNullOrEmpty(f)))}");
            if (tcType.Controls[tcType.SelectedIndex].Text == "Text")
            {
                CurrentTextList = Project.CurrentFolder.CurrentFile.CurrentSection.Entries.Where(e => checkedFilters.Contains(e.Status)).ToList();
                var old_index = lbEntries.SelectedIndex;
                lbEntries.DataSource = CurrentTextList;
                if (lbEntries.SelectedIndices.Count == 1)
                {
                    if (lbEntries.Items.Count > old_index)
                    {
                        lbEntries.SelectedIndices.Clear();
                        lbEntries.SelectedIndices.Add(old_index);
                    }
                }
                LoadEntryData(lbEntries);
            }
            else
            {
                var speakers = Project.CurrentFolder.CurrentFile.Speakers;
                if (speakers != null)
                {
                    CurrentSpeakerList = speakers.Where(e => checkedFilters.Contains(e.Status)).ToList();
                }
                else
                {
                    CurrentSpeakerList = new List<XMLEntry>();
                }
                var old_index = lbSpeaker.SelectedIndex;
                lbSpeaker.DataSource = CurrentSpeakerList;
                if (lbSpeaker.SelectedIndices.Count == 1)
                {
                    if (lbSpeaker.Items.Count > old_index)
                    {
                        lbSpeaker.SelectedIndices.Clear();
                        lbSpeaker.SelectedIndices.Add(old_index);
                    }
                }
                LoadEntryData(lbSpeaker);
            }
        }

        public void UpdateOptionsVisibility()
        {
            bool TORValid = config.IsPackingVisibility("TOR");
            tsTORPacking.Enabled = tsTORMakeIso.Enabled = tsTORExtract.Enabled = TORValid;
            
            // Show RM2 menu only when RM2 project is loaded
            bool RM2Valid = gameName == "RM2";
            tsRM2.Visible = RM2Valid;
        }

        private void UpdateStatusData()
        {
            var speakerStatusStats = Project.CurrentFolder.CurrentFile.SpeakersGetStatusData();
            var statusStats = Project.CurrentFolder.CurrentFile.GetStatusData();
            //File Count of status
            lNbToDo.Text = (statusStats["To Do"]).ToString();
            lNbProof.Text = (statusStats["Proofread"]).ToString();
            lNbProb.Text = (statusStats["Problematic"]).ToString();
            lNbEditing.Text = (statusStats["Edited"]).ToString();
            lNbDone.Text = (statusStats["Done"]).ToString();

            Dictionary<string, int> sectionStatusStats = new Dictionary<string, int>();
            if (tcType.SelectedTab.Text == "Speaker")
                sectionStatusStats = speakerStatusStats;
            else
                sectionStatusStats = Project.CurrentFolder.CurrentFile.CurrentSection.GetStatusData();
            //Section Count of status
            lNbToDoSect.Text = sectionStatusStats["To Do"].ToString();
            lNbProofSect.Text = sectionStatusStats["Proofread"].ToString();
            lNbProbSect.Text = sectionStatusStats["Problematic"].ToString();
            lNbEditingSect.Text = sectionStatusStats["Edited"].ToString();
            lNbDoneSect.Text = sectionStatusStats["Done"].ToString();
        }

        private void cbFileList_TextChanged(object sender, EventArgs e)
        {
            if (cbFileList.SelectedIndex != -1)
            {
                Project.CurrentFolder.SetCurrentFile(cbFileList.SelectedIndex);
                
                // Save the current file selections
                SaveCurrentFileSelections();

                string filetype = cbFileType.SelectedItem.ToString();
                if (filetype.Equals("Menu", StringComparison.InvariantCultureIgnoreCase))
                {
                    tbFriendlyName.Enabled = false;
                    tbFriendlyName.Text = cbFileList.Text;
                }
                else
                {
                    tbFriendlyName.Enabled = true;
                    tbFriendlyName.Text = Project.CurrentFolder.CurrentFile.FriendlyName ?? "";
                }

                var old_section = cbSections.SelectedItem.ToString();

                cbSections.DataSource = Project.CurrentFolder.CurrentFile.GetSectionNames();

                if (cbSections.Items.Contains(old_section))
                    cbSections.SelectedItem = old_section;
                CurrentTextList = Project.CurrentFolder.CurrentFile.CurrentSection.Entries;
                CurrentSpeakerList = Project.CurrentFolder.CurrentFile.Speakers;
                FilterEntryList();

                bSaveAll.Enabled = true;
            }
        }

        private void NDXToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
        }

        private void tbEnglishText_TextPasted(object sender, ClipboardEventArgs e)
        {
            tbEnglishText.Paste(e.ClipboardText.Replace("\r", "").Replace("\n", Environment.NewLine));
        }

        private void tbEnglishText_TextChanged(object sender, EventArgs e)
        {

            bool error = (tbEnglishText.Text.Count(x => x == '<') == tbEnglishText.Text.Count(x => x == '>'));

            lErrors.Text = "";
            if (!error)
            {
                lErrors.Text = "Warning: Missing '<' or '>' in tag.";
                lErrors.ForeColor = Color.Red;
            }

            string status = cbStatus.Text;
            if (tbEnglishText.Text == tbJapaneseText.Text)
                status = "Edited";
            else if (tbEnglishText.Text != "")
                status = "Edited";

            if (tcType.Controls[tcType.SelectedIndex].Text == "Speaker")
            {
                CurrentSpeakerList[lbSpeaker.SelectedIndex].EnglishText = status == "To Do" ? null : tbEnglishText.Text;
                CurrentSpeakerList[lbSpeaker.SelectedIndex].Status = status;
                int? speakerId = CurrentSpeakerList[lbSpeaker.SelectedIndex].Id;
                Project.CurrentFolder.CurrentFile.CurrentSection.Entries.ForEach(x => x.SpeakerName = x.Id == speakerId ? x.SpeakerName = tbEnglishText.Text : x.SpeakerName);
            }
            else
            {
                if (tbEnglishText.Text.Length == 0)
                {
                    status = "To Do";
                    CurrentTextList[lbEntries.SelectedIndex].EnglishText = null;
                }
                else
                {
                    CurrentTextList[lbEntries.SelectedIndex].EnglishText = tbEnglishText.Text;
                }
                CurrentTextList[lbEntries.SelectedIndex].Status = status;
            }

            cbStatus.Text = status;
            textPreview1.ReDraw(tbEnglishText.Text);
            Project.CurrentFolder.CurrentFile.needsSave = true;

            //string split = textPreview1.DoLineBreak(tbEnglishText.Text, 400);
            //string split = textPreview1.WordWrap(tbEnglishText.Text, Convert.ToInt32(tbMax.Text));
            //tbWrap.Text = split;
        }


        private void cbStatus_TextChanged(object sender, EventArgs e)
        {
        }

        private void tbNoteText_TextChanged(object sender, EventArgs e)
        {
            if (lbEntries.SelectedIndex > -1 && lbEntries.SelectedIndex < CurrentTextList.Count)
            {
                CurrentTextList[lbEntries.SelectedIndex].Notes = tbNoteText.Text;
                Project.CurrentFolder.CurrentFile.needsSave = true;
            }
        }

        private void lbEntries_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index >= CurrentTextList.Count)
                return;

            string text = GetTextBasedLanguage(e.Index, CurrentTextList);

            text = text == null ? "" : text;

            int nb = 0;
            if (CurrentTextList[e.Index].SpeakerId != null)
            {
                nb += 1;
            }

            nb += Regex.Matches(text, "\\r*\\n").Count;

            var size = (int)((nb + 1) * 14) + 6;

            e.ItemHeight = size;
        }

        private void cbFileList_DrawItem(object sender, DrawItemEventArgs e)
        {
            //Get the file selected
            if (Project?.CurrentFolder.FileList().Count > 0)
            {
                string text = ((ComboBox)sender).Items[e.Index].ToString();
                if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                {
                    e.Graphics.FillRectangle(new SolidBrush(Color.Black), e.Bounds);
                }
                else
                {
                    var count = CurrentTextList.Count;
                    var sdata = Project.CurrentFolder.XMLFiles[e.Index].GetStatusData();
                    if (sdata["Problematic"] != 0)
                    {
                        SolidBrush backgroundBrush = new SolidBrush(ColorByStatus["Problematic"]);
                        e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
                    }
                    else if (sdata["To Do"] > 0)
                    {
                        e.Graphics.FillRectangle(new SolidBrush(((Control)sender).BackColor), e.Bounds);
                    }
                    else if (sdata["Edited"] > 0)
                    {
                        SolidBrush backgroundBrush = new SolidBrush(ColorByStatus["Editing"]);
                        e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
                    }
                    else if (sdata["Proofread"] > 0)
                    {
                        SolidBrush backgroundBrush = new SolidBrush(ColorByStatus["Proofreading"]);
                        e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
                    }
                    else
                    {
                        SolidBrush backgroundBrush = new SolidBrush(ColorByStatus["Done"]);
                        e.Graphics.FillRectangle(backgroundBrush, e.Bounds);
                    }
                }

                SolidBrush textBrush = new SolidBrush(e.ForeColor);
                e.Graphics.DrawString(text, ((Control)sender).Font, textBrush, e.Bounds, StringFormat.GenericDefault);

                textBrush.Dispose();
            }
        }

        private void cbSections_TextChanged(object sender, EventArgs e)
        {
        }

        private void hexToJapaneseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            fHexToJapanese myForm = new fHexToJapanese();
            myForm.Show();
        }

        private void menuToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            MessageBox.Show("Extraction of Rebirth's files is in progress.\n You can still continue other work in the meantime");
            string successMessage = "Extraction of the files";
            PackingAssistant.CallPython(config.PythonLocation, Path.Combine(config.GetGameConfig("TOR").LastFolderPath, @"..\..\..\PythonLib"), "TOR", "unpack", $"Init --iso \"{config.GetGameConfig("TOR").IsoPath}\"", successMessage);
        }

        private void cbToDo_CheckedChanged(object sender, EventArgs e)
        {
            FilterEntryList();
            SaveCurrentFileSelections();
        }

        private void cbProof_CheckedChanged(object sender, EventArgs e)
        {
            FilterEntryList();
            SaveCurrentFileSelections();
        }

        private void cbDone_CheckedChanged(object sender, EventArgs e)
        {
            FilterEntryList();
            SaveCurrentFileSelections();
        }

        private void cbProblematic_CheckedChanged(object sender, EventArgs e)
        {
            FilterEntryList();
            SaveCurrentFileSelections();
        }

        private void cbInReview_CheckedChanged(object sender, EventArgs e)
        {
            FilterEntryList();
            SaveCurrentFileSelections();
        }

        private void FilterEntryList()
        {
            UpdateDisplayedEntries();
            UpdateStatusData();
        }

        private void cbLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            lbEntries.Invalidate();
        }

        private void cbSections_SelectedIndexChanged(object sender, EventArgs e)
        {
            string item = cbSections.SelectedItem.ToString();
            Project.CurrentFolder.CurrentFile.SetSection(item);

            if (cbSections.SelectedIndex < 1)
            {
                tbSectionName.Enabled = false;
            }
            else
            {
                tbSectionName.Enabled = true;
            }
            tbSectionName.Text = cbSections.Text;
            UpdateDisplayedEntries();
            UpdateStatusData();
            
            // Save the current file selections
            SaveCurrentFileSelections();
        }
        
        /// <summary>
        /// Saves the current file selections (file type, file name, section) and status filters to the game config
        /// </summary>
        private void SaveCurrentFileSelections()
        {
            try
            {
                if (Project?.CurrentFolder?.CurrentFile != null && !string.IsNullOrEmpty(gameName))
                {
                    var gameConfig = config.GetGameConfig(gameName);
                    if (gameConfig != null)
                    {
                        // Save current file selections
                        gameConfig.LastUsedFileType = cbFileType.Text;
                        gameConfig.LastUsedFileName = cbFileList.Text;
                        gameConfig.LastUsedSection = cbSections.Text;
                        
                        // Save current status filter states
                        gameConfig.LastUsedToDo = cbToDo.Checked;
                        gameConfig.LastUsedProof = cbProof.Checked;
                        gameConfig.LastUsedEditing = cbEditing.Checked;
                        gameConfig.LastUsedProblematic = cbProblematic.Checked;
                        gameConfig.LastUsedDone = cbDone.Checked;
                        
                        // Save the configuration
                        config.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Error saving file selections: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Restores the last used file selections (file type, file name, section) and status filters from the game config
        /// </summary>
        private void RestoreLastUsedFileSelections()
        {
            try
            {
                if (!string.IsNullOrEmpty(gameName))
                {
                    var gameConfig = config.GetGameConfig(gameName);
                    if (gameConfig != null && !string.IsNullOrEmpty(gameConfig.LastUsedFileType))
                    {
                        System.Diagnostics.Debug.WriteLine($"Restoring file selections: {gameConfig.LastUsedFileType} -> {gameConfig.LastUsedFileName} -> {gameConfig.LastUsedSection}");
                        
                        // Temporarily disable event handlers to prevent saving during restoration
                        DisableEventHandlers();
                        
                        // Step 1: Restore file type and manually trigger the formatting logic
                        if (cbFileType.Items.Contains(gameConfig.LastUsedFileType))
                        {
                            // Set the file type
                            cbFileType.Text = gameConfig.LastUsedFileType;
                            
                            // Manually trigger the folder change and formatting logic
                            Project.SetCurrentFolder(gameConfig.LastUsedFileType);
                            
                            // Manually populate and format the file list (same logic as cbFileType_TextChanged)
                            List<string> filelist = Project.CurrentFolder.FileList();
                            if (gameConfig.LastUsedFileType.Equals("Menu", StringComparison.InvariantCultureIgnoreCase))
                            {
                                cbFileList.DataSource = filelist;
                            }
                            else if (gameConfig.LastUsedFileType == "Skits")
                            {
                                List<XMLEntry> names = getSkitNameList();
                                if (names.Count != 1157)
                                {
                                    cbFileList.DataSource = filelist.Select(x => x + ".xml").ToList();
                                }
                                else
                                {
                                    for (int i = 0, j = 0; i < filelist.Count; i++)
                                    {
                                        if (((i > 1072) && (i < 1082)) || ((i > 1092) && (i < 1099)))
                                        {
                                            j++;
                                            filelist[i] += " | NO NAME";
                                            continue;
                                        }
                                        filelist[i] = filelist[i] + " | " + (names[i - j].EnglishText ?? names[i - j].JapaneseText);
                                    }
                                    cbFileList.DataSource = filelist;
                                }
                            }
                            else
                            {
                                for (int i = 0; i < filelist.Count; i++)
                                {
                                    string fname = Project.CurrentFolder.XMLFiles[i].FriendlyName ?? "NO NAME";
                                    filelist[i] = filelist[i] + " | " + fname;
                                }
                                cbFileList.DataSource = filelist;
                            }
                            
                            // Let the UI update and then restore file name
                            this.BeginInvoke(new Action(() =>
                            {
                                // Step 2: Restore file name
                                if (cbFileList.Items.Contains(gameConfig.LastUsedFileName))
                                {
                                    cbFileList.Text = gameConfig.LastUsedFileName;
                                    
                                    // Manually set the current file and populate sections
                                    int fileIndex = cbFileList.Items.IndexOf(gameConfig.LastUsedFileName);
                                    if (fileIndex >= 0)
                                    {
                                        Project.CurrentFolder.SetCurrentFile(fileIndex);
                                        cbSections.DataSource = Project.CurrentFolder.CurrentFile.GetSectionNames();
                                        
                                        // Update the current text and speaker lists for the restored file
                                        CurrentTextList = Project.CurrentFolder.CurrentFile.CurrentSection.Entries;
                                        CurrentSpeakerList = Project.CurrentFolder.CurrentFile.Speakers;
                                    }
                                    
                                    // Let the UI update and then restore section
                                    this.BeginInvoke(new Action(() =>
                                    {
                                        // Step 3: Restore section
                                        if (cbSections.Items.Contains(gameConfig.LastUsedSection))
                                        {
                                            cbSections.Text = gameConfig.LastUsedSection;
                                            
                                            // Manually set the current section in the project
                                            Project.CurrentFolder.CurrentFile.SetSection(gameConfig.LastUsedSection);
                                            
                                            // Update the current text and speaker lists
                                            CurrentTextList = Project.CurrentFolder.CurrentFile.CurrentSection.Entries;
                                            CurrentSpeakerList = Project.CurrentFolder.CurrentFile.Speakers;
                                        }
                                        
                                        // Step 4: Restore status filter states
                                        RestoreStatusFilterStates(gameConfig);
                                        
                                        // Step 5: Ensure current lists are properly initialized
                                        if (Project?.CurrentFolder?.CurrentFile?.CurrentSection != null)
                                        {
                                            CurrentTextList = Project.CurrentFolder.CurrentFile.CurrentSection.Entries;
                                            CurrentSpeakerList = Project.CurrentFolder.CurrentFile.Speakers;
                                        }
                                        
                                        // Re-enable event handlers
                                        EnableEventHandlers();
                                        
                                        // Now update the display with the restored filters applied
                                        UpdateDisplayedEntries();
                                        UpdateStatusData();
                                    }));
                                }
                                else
                                {
                                    // Re-enable event handlers if file name not found
                                    EnableEventHandlers();
                                    UpdateDisplayedEntries();
                                    UpdateStatusData();
                                }
                            }));
                        }
                        else
                        {
                            // Re-enable event handlers if file type not found
                            EnableEventHandlers();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Error restoring file selections: {ex.Message}");
                
                // Make sure event handlers are re-enabled even if there's an error
                EnableEventHandlers();
            }
                }
        
        /// <summary>
        /// Saves the initial status filter states when a project is first loaded
        /// </summary>
        private void SaveInitialStatusFilterStates()
        {
            try
            {
                if (!string.IsNullOrEmpty(gameName))
                {
                    var gameConfig = config.GetGameConfig(gameName);
                    if (gameConfig != null)
                    {
                        // Only save if we don't have saved status filter states yet
                        if (string.IsNullOrEmpty(gameConfig.LastUsedFileType))
                        {
                            // Save current status filter states as initial values
                            gameConfig.LastUsedToDo = cbToDo.Checked;
                            gameConfig.LastUsedProof = cbProof.Checked;
                            gameConfig.LastUsedEditing = cbEditing.Checked;
                            gameConfig.LastUsedProblematic = cbProblematic.Checked;
                            gameConfig.LastUsedDone = cbDone.Checked;
                            
                            // Save the configuration
                            config.Save();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Error saving initial status filter states: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Restores the last used status filter states from the game config
        /// </summary>
        private void RestoreStatusFilterStates(GameConfig gameConfig)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Restoring status filters: ToDo={gameConfig.LastUsedToDo}, Proof={gameConfig.LastUsedProof}, Editing={gameConfig.LastUsedEditing}, Problematic={gameConfig.LastUsedProblematic}, Done={gameConfig.LastUsedDone}");
                
                // Temporarily disable event handlers to prevent saving during restoration
                cbToDo.CheckedChanged -= cbToDo_CheckedChanged;
                cbProof.CheckedChanged -= cbProof_CheckedChanged;
                cbDone.CheckedChanged -= cbDone_CheckedChanged;
                cbProblematic.CheckedChanged -= cbProblematic_CheckedChanged;
                cbEditing.CheckedChanged -= cbInReview_CheckedChanged;
                
                // Restore status filter states
                cbToDo.Checked = gameConfig.LastUsedToDo;
                cbProof.Checked = gameConfig.LastUsedProof;
                cbEditing.Checked = gameConfig.LastUsedEditing;
                cbProblematic.Checked = gameConfig.LastUsedProblematic;
                cbDone.Checked = gameConfig.LastUsedDone;
                
                // Re-enable event handlers
                cbToDo.CheckedChanged += cbToDo_CheckedChanged;
                cbProof.CheckedChanged += cbProof_CheckedChanged;
                cbDone.CheckedChanged += cbDone_CheckedChanged;
                cbProblematic.CheckedChanged += cbProblematic_CheckedChanged;
                cbEditing.CheckedChanged += cbInReview_CheckedChanged;
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Error restoring status filter states: {ex.Message}");
                
                // Make sure event handlers are re-enabled even if there's an error
                cbToDo.CheckedChanged += cbToDo_CheckedChanged;
                cbProof.CheckedChanged += cbProof_CheckedChanged;
                cbDone.CheckedChanged += cbDone_CheckedChanged;
                cbProblematic.CheckedChanged += cbProblematic_CheckedChanged;
                cbEditing.CheckedChanged += cbInReview_CheckedChanged;
            }
        }
        
        /// <summary>
        /// Handles keyboard shortcuts for the main form
        /// Available shortcuts:
        /// - Ctrl+S: Save current file and trigger auto-apply
        /// - Ctrl+Up/Down: Navigate through entries
        /// - Ctrl+Alt+Up/Down: Navigate through file list
        /// - Ctrl+L: Copy Japanese text to English field
        /// - Ctrl+E: Toggle empty entries filter
        /// </summary>
        private void fMain_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control)
            {
                switch (e.KeyCode)
                {
                    case Keys.Down:
                        if (e.Alt)
                        {
                            if (cbFileList.Items.Count - 1 != cbFileList.SelectedIndex)
                                cbFileList.SelectedIndex += 1;
                            break;
                        }

                        if (tcType.SelectedIndex == 0)
                        {
                            if (lbEntries.Items.Count - 1 != lbEntries.SelectedIndex)
                            {
                                int idx = lbEntries.SelectedIndex;
                                lbEntries.ClearSelected();
                                lbEntries.SelectedIndex = idx + 1;
                            }
                        }
                        else
                        {
                            if (lbSpeaker.Items.Count - 1 != lbSpeaker.SelectedIndex)
                            {
                                int idx = lbSpeaker.SelectedIndex;
                                lbSpeaker.ClearSelected();
                                lbSpeaker.SelectedIndex = idx + 1;

                            }
                        }
                        tbEnglishText.Select();
                        tbEnglishText.SelectionStart = tbEnglishText.Text.Length;
                        tbEnglishText.SelectionLength = 0;
                        break;
                    case Keys.Up:
                        if (e.Alt)
                        {
                            if (cbFileList.SelectedIndex > 0)
                                cbFileList.SelectedIndex -= 1;
                            break;
                        }

                        if (tcType.SelectedIndex == 0)
                        {
                            if (lbEntries.SelectedIndex > 0)
                            {
                                int idx = lbEntries.SelectedIndex;
                                lbEntries.ClearSelected();
                                lbEntries.SelectedIndex = idx - 1;
                            }
                        }
                        else
                        {
                            if (lbSpeaker.SelectedIndex > 0)
                            {
                                int idx = lbSpeaker.SelectedIndex;
                                lbSpeaker.ClearSelected();
                                lbSpeaker.SelectedIndex = idx - 1;
                            }
                        }
                        tbEnglishText.Select();
                        tbEnglishText.SelectionStart = tbEnglishText.Text.Length;
                        tbEnglishText.SelectionLength = 0;
                        break;
                    case Keys.L:
                        if (string.IsNullOrWhiteSpace(tbEnglishText.Text))
                            tbEnglishText.Text = tbJapaneseText.Text;
                        break;
                    case Keys.S:
                        // Save all changed files and trigger auto-apply
                        bSaveAll.PerformClick();
                        break;
                    case Keys.E:
                        if (cbEmpty.Enabled)
                            cbEmpty.Checked = !cbEmpty.Checked;
                        break;
                    default:
                        e.Handled = false;
                        return;
                }

                e.Handled = true;
            }
        }

        private string stripTags(string input)
        {
            string output = "";
            string pattern = @"(<[\w/]+:?\w+>)";
            string[] result = Regex.Split(input.Replace("\r", "").Replace("\n", ""), pattern, RegexOptions.IgnoreCase).Where(x => x != "").ToArray();

            string[] names = { "<Veigue>", "<Mao>", "<Eugene>", "<Annie>", "<Tytree>", "<Hilda>", "<Claire>", "<Agarte>", "<Annie (NPC)>", "<Leader>" };

            foreach (string element in result)
            {
                if (element[0] == '<')
                {
                    if (names.Contains(element))
                    {
                        output += element.Substring(1, element.Length - 2);
                    }

                    if (element.Contains("unk") || element.Contains("var"))
                    {
                        output += "***";
                    }

                    if (element.Contains("nmb"))
                    {
                        string el = element.Substring(5, element.Length - 6);
                        output += Convert.ToInt32(el, 16);
                    }
                }
                else
                {
                    output += element;
                }
            }

            return output;
        }

        private void lbEntries_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.C)
            {
                List<string> st = new List<string>();
                ListBox curr_lb = (tcType.SelectedIndex == 0) ? lbEntries : lbSpeaker;

                if (curr_lb.SelectedIndices.Count > 1)
                {
                    foreach (XMLEntry et in curr_lb.SelectedItems)
                    {
                        st.Add(stripTags(et.JapaneseText));
                    }
                    Clipboard.SetText(string.Join("\n", st));
                }
                else
                {
                    Clipboard.SetText(stripTags(tbJapaneseText.Text));
                }
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            DisableEventHandlers();
            Project.CurrentFolder.XMLFiles[cbFileList.SelectedIndex] = Project.CurrentFolder.LoadXML(Project.CurrentFolder.CurrentFile.FilePath);
            Project.CurrentFolder.CurrentFile = Project.CurrentFolder.XMLFiles[cbFileList.SelectedIndex];
            Project.CurrentFolder.InvalidateTranslations();
            EnableEventHandlers();

            UpdateDisplayedEntries();
            UpdateStatusData();
        }

        private void tsSetup_Click(object sender, EventArgs e)
        {
            fSetup setupForm = new fSetup(this, config, PackingAssistant);
            setupForm.Show();
        }

        private void lbSpeaker_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadEntryData(lbSpeaker);
        }

        private void lbSpeaker_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index >= CurrentSpeakerList.Count)
                return;

            string text = GetTextBasedLanguage(e.Index, CurrentSpeakerList);

            int nb;
            if (string.IsNullOrEmpty(text))
                nb = 0;
            else
                nb = Regex.Matches(text, "\\r*\\n").Count;

            var size = (int)((nb + 1) * 14) + 6;

            e.ItemHeight = size;
        }

        private void bBrowse_Click(object sender, EventArgs e)
        {

        }

        private void tcType_Selected(object sender, TabControlEventArgs e)
        {
            if (Project == null)
                return;
            UpdateDisplayedEntries();
            UpdateStatusData();
        }

        private void extractIsoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Extraction of NDX's files is in progress.\n You can still continue other work in the meantime");
            string successMessage = "Extraction of the files";
            PackingAssistant.CallPython(config.PythonLocation, Path.Combine(config.GetGameConfig("NDX").LastFolderPath, @"..\..\..\PythonLib"), "NDX", "unpack", $"Init --iso \"{config.GetGameConfig("NDX").IsoPath}\"", successMessage);
        }

        private void cbEmpty_CheckedChanged(object sender, EventArgs e)
        {
            if (tcType.Controls[tcType.SelectedIndex].Text == "Text")
            {
                setEmpty(lbEntries);
            }
            else
            {
                setEmpty(lbSpeaker);
            }
            Project.CurrentFolder.CurrentFile.needsSave = true;
        }

        private void setEmpty(ListBox lb)
        {
            if (cbEmpty.Checked)
            {
                foreach (XMLEntry e in lb.SelectedItems)
                {
                    e.EnglishText = "";
                    e.Status = "Done";
                    cbStatus.Text = "Done";
                }
            }
            else
            {
                foreach (XMLEntry e in lb.SelectedItems)
                {
                    if (e.EnglishText != null && e.EnglishText.Length == 0)
                    {
                        e.EnglishText = null;
                        e.Status = "To Do";
                        cbStatus.Text = "To Do";
                    }
                }
            }
        }


        private void cbStatus_SelectionChangeCommitted(object sender, EventArgs e)
        {
            if ((cbStatus.Text == string.Empty) || (cbStatus.Text == MULTIPLE_STATUS))
                return;
            if (cbStatus.Items.Contains(MULTIPLE_STATUS))
            {
                cbStatus.Items.Remove(MULTIPLE_STATUS);
            }
            ListBox lb;
            if (tcType.Controls[tcType.SelectedIndex].Text == "Text")
            {
                lb = lbEntries;
            }
            else
            {
                lb = lbSpeaker;
            }
            foreach (XMLEntry entry in lb.SelectedItems)
            {
                entry.Status = cbStatus.Text;
            }
            Project.CurrentFolder.CurrentFile.needsSave = true;
            UpdateStatusData();
        }


        private void btnRename_Click(object sender, EventArgs e)
        {
            // Project.CurrentFolder.CurrentFile.Sections[]
        }
        private void btnSaveFile_Click(object sender, EventArgs e)
        {
            Project.CurrentFolder.CurrentFile.SaveToDisk();
            UpdateDisplayedEntries();
            UpdateStatusData();

            // Trigger auto-apply if enabled
            TriggerAutoApply(new List<string> { Project.CurrentFolder.CurrentFile.FilePath });
        }
        private void saveCurrentFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
        }
        private void reloadCurrentFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Project.CurrentFolder.CurrentFile.SaveToDisk();
            UpdateDisplayedEntries();
            UpdateStatusData();
        }
        private void saveAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Project.XmlFolders.ForEach(f => f.XMLFiles.AsParallel().ForAll(x => x.SaveToDisk()));
            MessageBox.Show("Text has been written to the XML files");
            UpdateDisplayedEntries();
            UpdateStatusData();

            // Trigger auto-apply for all saved files
            var allXmlFiles = new List<string>();
            foreach (var folder in Project.XmlFolders)
            {
                foreach (var file in folder.XMLFiles)
                {
                    if (!string.IsNullOrEmpty(file.FilePath))
                    {
                        allXmlFiles.Add(file.FilePath);
                    }
                }
            }
            TriggerAutoApply(allXmlFiles);
        }
        private void reloadAllToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var selectedFileType = cbFileType.Text;
            var selectedFile = cbFileList.Text;
            var selectedSection = cbSections.Text;
            var selectedLanguage = cbLanguage.Text;
            LoadFolder(Project.ProjectPath, Project.isLegacy);
            DisableEventHandlers();
            Project.SetCurrentFolder(selectedFileType);
            cbFileType.Text = selectedFileType;
            if (cbFileList.SelectedIndex > -1)
            {
                Project.CurrentFolder.SetCurrentFile(cbFileList.SelectedIndex);
            }
            cbFileList.DataSource = Project.CurrentFolder.FileList();
            cbFileList.Text = selectedFile;
            Project.CurrentFolder.CurrentFile.SetSection(selectedSection);
            cbSections.DataSource = Project.CurrentFolder.CurrentFile.GetSectionNames();
            cbSections.Text = selectedSection;
            cbLanguage.Text = selectedLanguage;
            lbEntries.DataSource = Project.CurrentFolder.CurrentFile.CurrentSection.Entries;
            lbSpeaker.DataSource = Project.CurrentFolder.CurrentFile.Speakers;
            EnableEventHandlers();
            UpdateDisplayedEntries();
            UpdateStatusData();
        }

        private void tbFriendlyName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                int sindex = cbSections.SelectedIndex;
                int tindex = lbEntries.SelectedIndex;
                int findex = cbFileList.SelectedIndex;
                Project.CurrentFolder.CurrentFile.FriendlyName = tbFriendlyName.Text;
                cbFileType.Text = "___";
                cbFileList.SelectedIndex = findex;
                cbSections.SelectedIndex = sindex;
                lbEntries.SelectedIndices.Clear();
                lbEntries.SelectedIndex = tindex;
                e.Handled = true;
                e.SuppressKeyPress = true;
                Project.CurrentFolder.CurrentFile.needsSave = true;
            }
        }
        private void tbSectionName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                int sindex = cbSections.SelectedIndex;
                int tindex = lbEntries.SelectedIndex;
                Project.CurrentFolder.CurrentFile.Sections[cbSections.SelectedIndex].Name = tbSectionName.Text;
                cbFileList.Text = "___";
                cbSections.SelectedIndex = sindex;
                lbEntries.SelectedIndices.Clear();
                lbEntries.SelectedIndex = tindex;
                e.Handled = true;
                e.SuppressKeyPress = true;
                Project.CurrentFolder.CurrentFile.needsSave = true;
            }
        }

        private void exportFileToCsvToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                string fname = saveDialog.FileName;
                Project.CurrentFolder.CurrentFile.SaveAsCsv(fname);
                MessageBox.Show("File exported");
            }
        }

        private void importFromCsvToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Not implemented yet");
        }

        #region Auto-Apply Service Event Handlers

        private void AutoApplyService_ProgressChanged(object sender, AutoApplyProgressEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AutoApplyService_ProgressChanged(sender, e)));
                return;
            }

            // Update progress bar
            progressBarAutoApply.Maximum = 100;
            progressBarAutoApply.Value = e.Percentage;
            
            // Update status label with detailed information
            var statusText = $"{e.Message} ({e.Current}/{e.Total} - {e.Percentage}%)";
            lblAutoApplyStatus.Text = statusText;
            
            // Show progress panel and ensure it's positioned at bottom-right
            panelAutoApplyProgress.Visible = true;
            PositionProgressBarAtBottomRight();
            
            // Also update the main status area for visibility
            if (lErrors != null)
            {
                lErrors.Text = $"Auto-Apply: {e.Message}";
                lErrors.ForeColor = Color.Blue;
            }
        }

        private void AutoApplyService_ProcessingCompleted(object sender, AutoApplyCompletedEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AutoApplyService_ProcessingCompleted(sender, e)));
                return;
            }

            // Hide progress bar
            panelAutoApplyProgress.Visible = false;

            if (e.Success)
            {
                // Show success message in status area
                if (e.ProcessedXmlFiles > 0)
                {
                    var successMessage = $"Auto-apply completed! Processed {e.ProcessedXmlFiles} XML file(s), Updated {e.ProcessedArcFiles} ARC file(s)";
                    if (lErrors != null)
                    {
                        lErrors.Text = successMessage;
                        lErrors.ForeColor = Color.Green;
                    }
                    Debug.WriteLine($"[AutoApply] {successMessage}");
                    Console.WriteLine($"[AutoApply] {successMessage}");
                }
            }
            else
            {
                // Show error message in status area
                var errorMessage = $"Auto-apply failed: {e.ErrorMessage}";
                if (lErrors != null)
                {
                    lErrors.Text = errorMessage;
                    lErrors.ForeColor = Color.Red;
                }
                Debug.WriteLine($"[AutoApply] {errorMessage}");
                Console.WriteLine($"[AutoApply] {errorMessage}");
            }
        }

        /// <summary>
        /// Positions the progress bar at the bottom-right corner of the application window
        /// </summary>
        private void PositionProgressBarAtBottomRight()
        {
            if (panelAutoApplyProgress != null)
            {
                // Get the form's client area
                var clientSize = this.ClientSize;
                
                // Position at bottom-right corner with some padding
                var x = clientSize.Width - panelAutoApplyProgress.Width - 10;
                var y = clientSize.Height - panelAutoApplyProgress.Height - 10;
                
                panelAutoApplyProgress.Location = new System.Drawing.Point(x, y);
                panelAutoApplyProgress.BringToFront();
            }
        }

        #endregion

        /// <summary>
        /// Initializes the auto-apply service for RM2 projects
        /// </summary>
        private void InitializeAutoApplyService()
        {
            try
            {
                // Check if this is an RM2 project
                if (Project != null && Project.ProjectPath.Contains("RM2"))
                {
                    var rm2Config = config.GetGameConfig("RM2");
                    if (rm2Config != null)
                    {
                        autoApplyService = new AutoApplyService(rm2Config);
                        autoApplyService.ProgressChanged += AutoApplyService_ProgressChanged;
                        autoApplyService.ProcessingCompleted += AutoApplyService_ProcessingCompleted;
                    }
                }
                else
                {
                    // Clean up auto-apply service for non-RM2 projects
                    if (autoApplyService != null)
                    {
                        autoApplyService.ProgressChanged -= AutoApplyService_ProgressChanged;
                        autoApplyService.ProcessingCompleted -= AutoApplyService_ProcessingCompleted;
                        autoApplyService = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to initialize auto-apply service: {ex.Message}");
            }
        }

        /// <summary>
        /// Triggers the auto-apply workflow for the specified XML files
        /// </summary>
        private void TriggerAutoApply(List<string> xmlFiles)
        {
            try
            {
                var message = $"[AutoApply] TriggerAutoApply called with {xmlFiles.Count} XML files";
                Debug.WriteLine(message);
                Console.WriteLine(message);
                
                // Check if auto-apply is enabled and we have an RM2 project
                if (autoApplyService == null)
                {
                    var nullMessage = "[AutoApply] Auto-apply service is null, skipping";
                    Debug.WriteLine(nullMessage);
                    Console.WriteLine(nullMessage);
                    return;
                }
                
                if (!gameConfig?.AutoApplyEnabled == true)
                {
                    var disabledMessage = "[AutoApply] Auto-apply is disabled in settings, skipping";
                    Debug.WriteLine(disabledMessage);
                    Console.WriteLine(disabledMessage);
                    return;
                }

                // Check if this is an RM2 project
                if (!Project.ProjectPath.Contains("RM2"))
                {
                    var notRm2Message = "[AutoApply] Not an RM2 project, skipping auto-apply";
                    Debug.WriteLine(notRm2Message);
                    Console.WriteLine(notRm2Message);
                    return;
                }

                var startMessage = $"[AutoApply] Starting auto-apply process for files: {string.Join(", ", xmlFiles.Select(Path.GetFileName))}";
                Debug.WriteLine(startMessage);
                Console.WriteLine(startMessage);
                
                // Start the auto-apply process
                _ = autoApplyService.ProcessXmlFilesAsync(xmlFiles);
            }
            catch (Exception ex)
            {
                var errorMessage = $"[AutoApply] Exception in TriggerAutoApply: {ex.Message}";
                Debug.WriteLine(errorMessage);
                Console.WriteLine(errorMessage);
                Debug.WriteLine($"[AutoApply] Stack trace: {ex.StackTrace}");
            }
        }

        private void setFileAsDoneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            foreach (XMLSection s in Project.CurrentFolder.CurrentFile.Sections.Where(s => s.Name != "All strings"))
            {
                foreach (XMLEntry entry in s.Entries)
                {
                    entry.Status = "Done";
                }
            }
            cbFileList.Text = "___";
        }

        private void setSectionAsDoneToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (cbSections.SelectedIndex == 0)
            {
                return;
            }

            foreach (XMLEntry entry in Project.CurrentFolder.CurrentFile.Sections[cbSections.SelectedIndex].Entries)
            {
                entry.Status = "Done";
            }
            cbFileList.Text = "___";
        }

        private void bSearch_Click(object sender, EventArgs e)
        {
            string textToFind = tbSearch.Text.Replace("\r\n", "\n");
            ListSearch = FindOtherTranslations(cbFileKindSearch.Text, textToFind, cbLangSearch.Text, cbExact.Checked, cbCase.Checked, cbMatchWhole.Checked);

            lEntriesFound.Text = $"Entries Found ({ListSearch.Count} entries)";
            lbSearch.DataSource = ListSearch.Select(x => $"{x.Folder} - " +
            $"{Project.GetFolderByName(x.Folder).XMLFiles[Convert.ToInt32(x.FileId)].Name} - " +
            $"{x.Section} - {x.Id}").ToList();
        }

        private void lNbOtherTranslations_Click(object sender, EventArgs e)
        {
            tabSearchMass.SelectedIndex = 1;
        }


        private void lbMassReplace_DrawItem(object sender, DrawItemEventArgs e)
        {
            DrawEntries(e, OtherTranslations.Select(x => x.Entry).ToList(), false);
        }

        private void lbSearch_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index >= ListSearch.Count)
                return;

            string text = GetTextBasedLanguage(e.Index, ListSearch.Select(x => x.Entry).ToList());

            text = text == null ? "" : text;

            int nb = 2;
            if (ListSearch[e.Index].Entry.SpeakerId != null)
            {
                nb += 1;
            }

            nb += Regex.Matches(text, "\\r*\\n").Count;

            var size = (int)((nb + 1) * 14) + 6;

            e.ItemHeight = size;
        }

        private void lbSearch_DrawItem(object sender, DrawItemEventArgs e)
        {
            DrawSearchEntries(e, ListSearch, true);
        }

        private void lbDistinctTranslations_DrawItem(object sender, DrawItemEventArgs e)
        {
            DrawSearchEntries(e, OtherTranslations, false);
        }

        private void lbDistinctTranslations_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index >= OtherTranslations.Count)
                return;

            string text = GetTextBasedLanguage(e.Index, OtherTranslations.Select(x => x.Entry).ToList());

            text = text == null ? "" : text;

            int nb = 2;
            if (OtherTranslations[e.Index].Entry.SpeakerId != null)
            {
                nb += 1;
            }

            nb += Regex.Matches(text, "\\r*\\n").Count;

            var size = (int)((nb + 1) * 14) + 6;

            e.ItemHeight = size;
        }

        private void lbDistinctTranslations_SelectedIndexChanged(object sender, EventArgs e)
        {
            //OtherTranslations[0].
            EntryFound entry = OtherTranslations[lbDistinctTranslations.SelectedIndex];
            int folderId = Project.GetFolderId(entry.Folder);
            List<XMLEntry> entries = Project.XmlFolders[folderId].XMLFiles[entry.FileId].Sections.Where(x => x.Name == entry.Section).First().Entries;

            List<int?> idList = new List<int?>();
            if (entry.Id > 0)
                idList.Add(entry.Id - 1);

            idList.Add(entry.Id);

            if (entry.Id < entries.Count - 1)
                idList.Add(entry.Id + 1);

            entries = entries.Where(x => idList.Contains(x.Id)).ToList();

            ContextTranslations = entries;
            lbContext.DataSource = ContextTranslations;
        }

        private void lbContext_DrawItem(object sender, DrawItemEventArgs e)
        {
            DrawEntries(e, ContextTranslations, false);
        }

        private void lbContext_MeasureItem(object sender, MeasureItemEventArgs e)
        {
            if (e.Index >= ContextTranslations.Count)
                return;

            string text = GetTextBasedLanguage(e.Index, ContextTranslations);

            text = text == null ? "" : text;

            int nb = 2;
            if (ContextTranslations[e.Index].SpeakerId != null)
            {
                nb += 1;
            }

            nb += Regex.Matches(text, "\\r*\\n").Count;

            var size = (int)((nb + 1) * 14) + 6;

            e.ItemHeight = size;
        }

        private void lbSearch_Click(object sender, EventArgs e)
        {
            if (!(cbDone.Checked && cbDone.Checked && cbProblematic.Checked && cbEditing.Checked && cbToDo.Checked && cbProof.Checked))
            {
                cbToDo.Checked = true;
                cbProof.Checked = true;
                cbEditing.Checked = true;
                cbProblematic.Checked = true;
                cbDone.Checked = true;
            }

            if (ListSearch != null)
            {
                if (cbDone.Checked && cbDone.Checked && cbProblematic.Checked && cbEditing.Checked && cbToDo.Checked && cbProof.Checked)
                {
                    EntryFound eleSelected = ListSearch[lbSearch.SelectedIndex];
                    cbFileType.Text = eleSelected.Folder;
                    cbFileList.SelectedIndex = eleSelected.FileId;


                    if (eleSelected.Section == "Speaker")
                    {
                        lbSpeaker.ClearSelected();
                        tcType.SelectedIndex = 1;
                        lbSpeaker.SelectedIndex = eleSelected.Id;
                    }
                    else
                    {
                        lbEntries.ClearSelected();
                        cbSections.Text = "All strings";
                        tcType.SelectedIndex = 0;
                        lbEntries.SelectedIndex = CurrentTextList.FindIndex(x => x.Id == eleSelected.Id);
                    }
                }
            }
        }

        private void splitter2_SplitterMoved(object sender, SplitterEventArgs e)
        {

        }

        private void fMain_Resize(object sender, EventArgs e)
        {
            // Reposition progress bar when window is resized
            if (panelAutoApplyProgress != null && panelAutoApplyProgress.Visible)
            {
                PositionProgressBarAtBottomRight();
            }
            
            if (WindowState != LastWindowState)
            {
                if (WindowState == FormWindowState.Maximized)
                {
                    leftColumn.Size = new Size((int)(ClientSize.Width * 0.3f), leftColumn.Height);
                    middleColumn.Size = new Size((int)(ClientSize.Width * 0.4f), middleColumn.Height);
                    //rightColumn.Size = new Size((int)(ClientSize.Width * 0.3f), rightColumn.Height);
                }
                else if (WindowState == FormWindowState.Normal)
                {
                    leftColumn.Size = new Size((int)(ClientSize.Width * 0.3f), leftColumn.Height);
                    middleColumn.Size = new Size((int)(ClientSize.Width * 0.35f), middleColumn.Height);
                    //rightColumn.Size = new Size((int)(ClientSize.Width * 0.3f), rightColumn.Height);
                }
                LastWindowState = WindowState;
            }
        }

        private void tbMax_KeyDown(object sender, KeyEventArgs e)
        {
            int max = 0;
            bool t = Int32.TryParse(tbMax.Text, out max);

            if (t)
                tbWrap.Text = textPreview1.DoLineBreak(tbEnglishText.Text, Convert.ToInt32(tbMax.Text));
        }

        private void cbRM2Options_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbRM2Options.SelectedItem != null && cbRM2Options.SelectedItem.ToString() == "Settings")
            {
                // TODO: Implement RM2 Settings dialog/form
                MessageBox.Show("RM2 Settings functionality will be implemented here.", "RM2 Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void tsRM2_Click(object sender, EventArgs e)
        {
            // TODO: Implement RM2 menu functionality
            MessageBox.Show("RM2 menu clicked. Add your RM2-specific functionality here.", "RM2", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void tsRM2Settings_Click(object sender, EventArgs e)
        {
            // Open RM2 Settings window
            fRM2Settings settingsForm = new fRM2Settings(config);
            settingsForm.ShowDialog();
        }

        private void tsRM2ApplyTranslations_Click(object sender, EventArgs e)
        {
            ExecuteRM2Script("rm2_apply.py", "Apply RM2 Translations");
        }

        private void tsRM2ReplaceAllFiles_Click(object sender, EventArgs e)
        {
            // First, show the better confirmation dialog (similar to ExecuteRM2Script)
            var gameConfig = config.GetGameConfig("RM2");
            if (gameConfig == null)
            {
                MessageBox.Show("Please configure the RM2 project folder path in RM2 → Settings first.", 
                    "RM2 Project Path Not Set", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string scriptPath = Path.Combine(gameConfig.ProjectRootPath, "tools", "replace-all.py");
            var result = MessageBox.Show(
                $"This will execute replace-all.py to replace all files.\n\n" +
                $"Script: {scriptPath}\n" +
                $"ISO: {gameConfig.IsoPath}\n\n" +
                "Do you want to continue?",
                "Confirm Replace All Files",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return; // User cancelled
            }

            // Then, automatically ensure the RM2_translated.iso exists in the build folder
            if (!EnsureRM2TranslatedISOExists())
            {
                return; // Stop if we couldn't create the ISO
            }
            
            // Finally, execute the replace-all.py script (skip confirmation since we already confirmed)
            ExecuteRM2Script("replace-all.py", "Replace All Files", true);
        }
        
        private void tsRM2ForceFreshISO_Click(object sender, EventArgs e)
        {
            // Always create a fresh copy of the ISO
            if (!ForceFreshISOCopy())
            {
                return; // Stop if we couldn't create the ISO
            }
            
            MessageBox.Show(
                "Fresh ISO copy created successfully!\n\n" +
                "The RM2_translated.iso has been replaced with a fresh copy of the original ISO.",
                "Fresh ISO Copy Created",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private bool EnsureRM2TranslatedISOExists()
        {
            try
            {
                // Get the RM2 project configuration
                var gameConfig = config.GetGameConfig("RM2");
                if (gameConfig == null || string.IsNullOrEmpty(gameConfig.FolderPath))
                {
                    MessageBox.Show("Please configure the RM2 project folder path in RM2 → Settings first.", 
                        "RM2 Project Path Not Set", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Get the ISO path
                if (string.IsNullOrEmpty(gameConfig.IsoPath))
                {
                    MessageBox.Show("Please configure the RM2 ISO path in RM2 → Settings first.", 
                        "RM2 ISO Path Not Set", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Check if original ISO exists
                if (!File.Exists(gameConfig.IsoPath))
                {
                    MessageBox.Show($"Original ISO file not found: {gameConfig.IsoPath}\n\nPlease check the ISO path in RM2 → Settings.", 
                        "Original ISO Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // Construct the build folder path and target ISO path
                string buildFolderPath = Path.Combine(gameConfig.ProjectRootPath, "build");
                string targetISOPath = Path.Combine(buildFolderPath, "RM2_translated.iso");

                // Check if the target ISO already exists
                if (File.Exists(targetISOPath))
                {
                    // ISO already exists, we can proceed without copying
                    return true;
                }

                // Ensure build folder exists
                if (!Directory.Exists(buildFolderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(buildFolderPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to create build folder: {ex.Message}", 
                            "Create Folder Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }

                // Copy the original ISO to the build folder with progress indication
                try
                {
                    // Get file size for progress calculation
                    long fileSize = new FileInfo(gameConfig.IsoPath).Length;
                    
                    // Show progress dialog
                    var progressForm = new fRM2Progress("ISO Copy", "Copying ISO file...", config, true);
                    progressForm.ShowCopyProgress(gameConfig.IsoPath, targetISOPath, fileSize);
                    progressForm.ShowDialog();
                    
                    // Check if copy was successful
                    if (File.Exists(targetISOPath))
                    {
                        return true; // Successfully created, proceed silently
                    }
                    else
                    {
                        MessageBox.Show("ISO copy operation was cancelled or failed.", 
                            "Copy Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy ISO file: {ex.Message}", 
                        "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error ensuring RM2_translated.iso exists: {ex.Message}", 
                    "ISO Check Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        
        private bool ForceFreshISOCopy()
        {
            try
            {
                // Get the RM2 project configuration
                var gameConfig = config.GetGameConfig("RM2");
                if (gameConfig == null || string.IsNullOrEmpty(gameConfig.FolderPath))
                {
                    MessageBox.Show("Please configure the RM2 project folder path in RM2 → Settings first.", 
                        "RM2 Project Path Not Set", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                // Get the ISO path
                if (string.IsNullOrEmpty(gameConfig.IsoPath))
                {
                    MessageBox.Show("Please configure the RM2 ISO path in RM2 → Settings first.", 
                        "RM2 ISO Path Not Set", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // Check if original ISO exists
                if (!File.Exists(gameConfig.IsoPath))
                {
                    MessageBox.Show($"Original ISO file not found: {gameConfig.IsoPath}\n\nPlease check the ISO path in RM2 → Settings.", 
                        "Original ISO Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }

                // Construct the build folder path and target ISO path
                string buildFolderPath = Path.Combine(gameConfig.ProjectRootPath, "build");
                string targetISOPath = Path.Combine(buildFolderPath, "RM2_translated.iso");

                // Always delete existing ISO if it exists
                if (File.Exists(targetISOPath))
                {
                    try
                    {
                        File.Delete(targetISOPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to delete existing ISO file: {ex.Message}", 
                            "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }

                // Ensure build folder exists
                if (!Directory.Exists(buildFolderPath))
                {
                    try
                    {
                        Directory.CreateDirectory(buildFolderPath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to create build folder: {ex.Message}", 
                            "Create Folder Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }

                // Copy the original ISO to the build folder with progress indication
                try
                {
                    // Get file size for progress calculation
                    long fileSize = new FileInfo(gameConfig.IsoPath).Length;
                    
                    // Show progress dialog
                    var progressForm = new fRM2Progress("ISO Copy", "Creating fresh ISO copy...", config, true);
                    progressForm.ShowCopyProgress(gameConfig.IsoPath, targetISOPath, fileSize);
                    progressForm.ShowDialog();
                    
                    // Check if copy was successful
                    if (File.Exists(targetISOPath))
                    {
                        return true; // Successfully created fresh copy
                    }
                    else
                    {
                        MessageBox.Show("ISO copy operation was cancelled or failed.", 
                            "Copy Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy ISO file: {ex.Message}", 
                        "Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating fresh ISO copy: {ex.Message}", 
                    "ISO Copy Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void ExecuteRM2Script(string scriptName, string operationName, bool skipConfirmation = false)
        {
            try
            {
                // Get the RM2 project folder path
                var gameConfig = config.GetGameConfig("RM2");
                if (gameConfig == null || string.IsNullOrEmpty(gameConfig.FolderPath))
                {
                    MessageBox.Show("Please configure the RM2 project folder path in RM2 → Settings first.", 
                        "RM2 Project Path Not Set", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Get the ISO path
                if (string.IsNullOrEmpty(gameConfig.IsoPath))
                {
                    MessageBox.Show("Please configure the RM2 ISO path in RM2 → Settings first.", 
                        "RM2 ISO Path Not Set", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Construct the script path using project root
                string scriptPath = Path.Combine(gameConfig.ProjectRootPath, "tools", scriptName);
                if (!File.Exists(scriptPath))
                {
                    MessageBox.Show($"Script not found: {scriptPath}\n\nPlease ensure the RM2 project root folder contains the tools directory with {scriptName}", 
                        "Script Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Check if Python is available (try RM2-specific path first, then global)
                string pythonPath = gameConfig.PythonPath;
                if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
                {
                    pythonPath = config.PythonLocation;
                    if (string.IsNullOrEmpty(pythonPath) || !File.Exists(pythonPath))
                    {
                        MessageBox.Show("Python location not configured or Python executable not found.\n\nPlease configure Python in RM2 → Settings or in the main application settings.", 
                            "Python Not Configured", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                // Show confirmation dialog (unless skipped)
                if (!skipConfirmation)
                {
                    var result = MessageBox.Show(
                        $"This will execute {scriptName} to {operationName.ToLower()}.\n\n" +
                        $"Script: {scriptPath}\n" +
                        $"ISO: {gameConfig.IsoPath}\n\n" +
                        "Do you want to continue?",
                        $"Confirm {operationName}", 
                        MessageBoxButtons.YesNo, 
                        MessageBoxIcon.Question);

                    if (result != DialogResult.Yes)
                    {
                        return; // User cancelled
                    }
                }

                // Open the progress form to show real-time output
                var progressForm = new fRM2Progress(scriptPath, operationName, config);
                progressForm.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error executing {scriptName}: {ex.Message}", 
                    "Script Execution Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

}