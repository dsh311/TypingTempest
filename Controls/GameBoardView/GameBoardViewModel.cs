using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Speech.Recognition;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Xml.Linq;
using System.Xml;

namespace TypingTempest.Controls.GameBoardView
{

    // ---------------------------------------
    public class LessonSettings : INotifyPropertyChanged
    {
        private string _title;
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _estimatedTimeMinutes;
        public int EstimatedTimeMinutes
        {
            get => _estimatedTimeMinutes;
            set
            {
                if (_estimatedTimeMinutes != value)
                {
                    _estimatedTimeMinutes = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _language;
        public string Language
        {
            get => _language;
            set
            {
                if (_language != value)
                {
                    _language = value;
                    OnPropertyChanged();
                }
            }
        }

        private EditorSettings _editor = new EditorSettings();
        public EditorSettings Editor
        {
            get => _editor;
            set
            {
                if (_editor != value)
                {
                    _editor = value;
                    OnPropertyChanged();
                }
            }
        }

        private AudioSettings _audio = new AudioSettings();
        public AudioSettings Audio
        {
            get => _audio;
            set
            {
                if (_audio != value)
                {
                    _audio = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class EditorSettings : INotifyPropertyChanged
    {
        private bool _syntaxHighlighting;
        public bool SyntaxHighlighting
        {
            get => _syntaxHighlighting;
            set
            {
                if (_syntaxHighlighting != value)
                {
                    _syntaxHighlighting = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _theme;
        public string Theme
        {
            get => _theme;
            set
            {
                if (_theme != value)
                {
                    _theme = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _fontSize;
        public int FontSize
        {
            get => _fontSize;
            set
            {
                if (_fontSize != value)
                {
                    _fontSize = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _zoomFactor;
        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                if (_zoomFactor != value)
                {
                    _zoomFactor = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _ignoreLineComments;
        public bool IgnoreLineComments
        {
            get => _ignoreLineComments;
            set
            {
                if (_ignoreLineComments != value)
                {
                    _ignoreLineComments = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class AudioSettings : INotifyPropertyChanged
    {
        private bool _speechEnabled;
        public bool SpeechEnabled
        {
            get => _speechEnabled;
            set
            {
                if (_speechEnabled != value)
                {
                    _speechEnabled = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    // ---------------------------------------


    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
            => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter)
            => _execute();

        public event EventHandler? CanExecuteChanged;

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
    

    public class GameBoardViewModel : INotifyPropertyChanged
    {
        public event Action LessonLoaded;

        //public event Action? SyntaxHighlightingRequested;
        public event Action<string>? SyntaxHighlightingRequested;


        private LessonSettings _settings;
        public LessonSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                OnPropertyChanged();
            }
        }

        private string _syntaxFileName;
        public string SyntaxFileName
        {
            get => _syntaxFileName;
            set
            {
                if (_syntaxFileName != value)
                {
                    _syntaxFileName = value;
                    OnPropertyChanged();
                }
            }
        }


        public IReadOnlyList<string> Words { get; private set; } = Array.Empty<string>();

        public RelayCommand PrevLessonCommand { get; }
        public RelayCommand NextLessonCommand { get; }

        public GameBoardViewModel()
        {
            PrevLessonCommand = new RelayCommand(PrevLesson, CanPrevLesson);
            NextLessonCommand = new RelayCommand(NextLesson, CanNextLesson);
        }


        private string _infoText;
        public string InfoText
        {
            get => _infoText;
            set { _infoText = value; OnPropertyChanged(); }
        }

        private string _codeText;
        public string CodeText
        {
            get => _codeText;
            set { _codeText = value; OnPropertyChanged(); }
        }



        public ObservableCollection<string> LessonFiles { get; } = new();

        private int _selectedLessonIndex = -1;
        public int SelectedLessonIndex
        {
            get => _selectedLessonIndex;
            set
            {
                if (_selectedLessonIndex == value)
                    return;

                _selectedLessonIndex = value;
                OnPropertyChanged();

                SelectedLesson =
                    (value >= 0 && value < LessonFiles.Count)
                    ? LessonFiles[value]
                    : null;

                //LessonLoaded?.Invoke(); // Tell a lesson loaded

                PrevLessonCommand.RaiseCanExecuteChanged();
                NextLessonCommand.RaiseCanExecuteChanged();
            }
        }

        private string? _selectedLesson;
        public string? SelectedLesson
        {
            get => _selectedLesson;
            private set
            {
                _selectedLesson = value;
                OnPropertyChanged();

                if (value != null)
                {
                    LoadSelectedLesson();
                    LessonLoaded?.Invoke(); // Tell a lesson loaded
                }
            }
        }


        private string? _lessonFolderPath;
        public string? LessonFolderPath
        {
            get => _lessonFolderPath;
            set => _lessonFolderPath = value;
        }

        private string? _lessonContent;
        public string? LessonContent
        {
            get => _lessonContent;
            private set
            {
                _lessonContent = value;
                OnPropertyChanged();
            }
        }

        private XDocument? _lessonXml;
        public XDocument? LessonXml
        {
            get => _lessonXml;
            private set
            {
                _lessonXml = value;
                OnPropertyChanged();
            }
        }


        //public Dictionary<int, List<string>> SpeechPhrases { get; } = new();
        public class SpeechCue
        {
            public int Start { get; set; }   // character offset where cue triggers
            public int End { get; set; }     // character offset where underline ends
            public string TextInfo { get; set; } // phrase to speak
            public string TextSpeak { get; set; } // phrase to speak
        }
        // Declare dictionary that maps offset -> list of SpeechCue objects
        public Dictionary<int, List<SpeechCue>> SpeechCues { get; } = new();


        public void LoadLessonFolder(string folderPath)
        {
            LessonFiles.Clear();
            LessonFolderPath = folderPath;

            foreach (var file in Directory.GetFiles(folderPath))
            {
                LessonFiles.Add(Path.GetFileName(file));
            }

            if (LessonFiles.Count > 0)
            {
                //TODO, is this still needed?
                //SelectedLesson = LessonFiles[0];
                SelectedLessonIndex = 0;
            }
        }


        public string GetCodeText()
        {
            return CodeText;
        }

        private LessonSettings ParseSettings(XDocument doc)
        {
            // If doc.Root is null, settingsElement becomes null safely
            var settingsElement = doc.Root?.Element("settings");

            // We return a new object regardless, using defaults if settingsElement is null
            return new LessonSettings
            {
                // Null-conditional ?. ensures we don't crash if settingsElement is missing
                Title = (string)settingsElement?.Element("title") ?? "Default Title",
                EstimatedTimeMinutes = (int?)settingsElement?.Element("estimatedTimeMinutes") ?? 0,
                Language = (string)settingsElement?.Element("language") ?? "csharp",

                Editor = new EditorSettings
                {
                    SyntaxHighlighting = (bool?)settingsElement?.Element("editor")?.Element("syntaxHighlighting") ?? false,
                    Theme = (string)settingsElement?.Element("editor")?.Element("theme") ?? "dark",
                    FontSize = (int?)settingsElement?.Element("editor")?.Element("fontSize") ?? 12,
                    ZoomFactor = (double?)settingsElement?.Element("editor")?.Element("zoomFactor") ?? 1.0,
                    IgnoreLineComments = (bool?)settingsElement?.Element("editor")?.Element("ignoreLineComments") ?? false
                },

                Audio = new AudioSettings
                {
                    SpeechEnabled = (bool?)settingsElement?.Element("audio")?.Element("speechEnabled") ?? false
                }
            };
        }

        public void SetSyntaxHighlighting(string? language)
        {
            SyntaxHighlightingRequested?.Invoke(language);
        }

        private void LoadSelectedLesson()
        {
            if (LessonFolderPath == null || SelectedLesson == null)
            {
                return;
            }

            var fullPath = Path.Combine(LessonFolderPath, SelectedLesson);

            try
            {
                string infoText = null;
                string codeText = null;

                try
                {
                    // Try to load as XML/HTML
                    var lessonXml = XDocument.Load(fullPath);

                    Settings = ParseSettings(lessonXml);
                    SetSyntaxHighlighting(Settings.Language?.ToLower());


                    var sections = lessonXml.Root
                        ?.Element("sections")
                        ?.Elements("section");

                    var infoSection = sections?
                        .FirstOrDefault(s => (string)s.Attribute("type") == "info");

                    infoText = infoSection?.Element("p")?.Value.Trim();

                    var codeSection = sections?
                        .FirstOrDefault(s => (string)s.Attribute("type") == "code");


                    // -------------
                    // Load speech triggers
                    SpeechCues.Clear();
                    

                    var speechParent = codeSection?.Element("speech");
                    var phraseNodes = speechParent?.Elements("phrase");

                    if (phraseNodes != null)
                    {
                        foreach (var phrase in phraseNodes)
                        {
                            try
                            {
                                int start = (int)phrase.Attribute("start");
                                int end = (int)phrase.Attribute("end");
                                string speakInfo = phrase.Attribute("speakinfo")?.Value ?? "";
                                string speakText = phrase.Attribute("speaktext")?.Value ?? "";

                                if (!string.IsNullOrWhiteSpace(speakText))
                                {
                                    var cue = new SpeechCue
                                    {
                                        Start = start,
                                        End = end,
                                        TextInfo = speakInfo,
                                        TextSpeak = speakText
                                    };

                                    if (!SpeechCues.ContainsKey(start))
                                    {
                                        SpeechCues[start] = new List<SpeechCue>();
                                    }

                                    SpeechCues[start].Add(cue);
                                }
                            }
                            catch
                            {
                                // Ignore malformed phrase elements
                            }
                        }
                    }
                    // -------------


                    // Load the code text
                    var codeElement = codeSection?.Element("code");
                    if (codeElement != null && codeElement.FirstNode is XCData cdata)
                    {
                        codeText = cdata.Value;
                    }
                    else
                    {
                        string missingPart = "unknown";
                        if (sections == null) { missingPart = "sections"; }
                        else if (codeSection == null) { missingPart = "code"; }
                        codeText = "Error, missing element: " + missingPart;
                    }
                }
                catch
                {
                    // If loading as XML fails, fall back to plain text
                    infoText = File.ReadAllText(fullPath);
                    codeText = null;
                }

                // Set the TextBlock and TextEditor
                //InfoTextBox.Text = infoText ?? string.Empty;
                //CodeEditor.Text = codeText ?? string.Empty;

                InfoText = infoText ?? string.Empty;
                CodeText = codeText ?? string.Empty;

                ParseLessonWords(CodeText);

            }
            catch (Exception ex)
            {
                InfoText = $"Error loading lesson: {ex.Message}";
                CodeText = string.Empty;
            }


        }

        private int? FindFirstNonEmptyOffset()
        {
            if (string.IsNullOrEmpty(CodeText))
            {
                return null;
            }

            int offset = 0;

            using (var reader = new StringReader(CodeText))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        return offset + 1; // match your original behavior
                    }

                    // +1 accounts for newline character(s)
                    offset += line.Length + Environment.NewLine.Length;
                }
            }

            return null;
        }


        private void ParseLessonWords(string lessonText)
        {
            Words = lessonText
                .Split(new[] { ' ', '\r', '\n', '\t' },
                       StringSplitOptions.RemoveEmptyEntries);
        }



        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));




        private void PrevLesson()
        {
            if (SelectedLessonIndex > 0)
            {
                SelectedLessonIndex--;
            }
        }

        public void NextLesson()
        {
            if (SelectedLessonIndex < LessonFiles.Count - 1)
            {
                SelectedLessonIndex++;
            }
        }

        private bool CanPrevLesson()
            => SelectedLessonIndex > 0;

        private bool CanNextLesson()
            => SelectedLessonIndex < LessonFiles.Count - 1;


    }




}
