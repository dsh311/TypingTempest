using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;

namespace TypingTempest.Controls.GameBoardView
{
    /// <summary>
    /// Interaction logic for GameBoardView.xaml
    /// </summary>
    public partial class GameBoardView : System.Windows.Controls.UserControl
    {

        string _selectedFolder;
        private TypingColorizer _typingColorizer;
        SpeechSynthesizer synthesizer;
        //private int _currentWordLength;

        public GameBoardView()
        {
            InitializeComponent();

            // Setup speech
            synthesizer = new SpeechSynthesizer();
            // Automatically choose the first installed voice
            if (synthesizer.GetInstalledVoices().Count > 0)
            {
                synthesizer.SelectVoice(synthesizer.GetInstalledVoices()[0].VoiceInfo.Name);
            }

            var vm = new GameBoardViewModel();
            DataContext = vm;

            vm.LessonLoaded += OnLessonLoaded;

            CodeEditor.PreviewMouseDown += CodeEditor_PreviewMouseDown;

            // Ensure the ViewModel has access to change Views syntax high lighting
            vm.SyntaxHighlightingRequested += (language) => ApplySyntaxHighlighting(language);


            /*
            Debug.WriteLine("----------------------");
            foreach (var color in CodeEditor.SyntaxHighlighting.NamedHighlightingColors)
            {
                Debug.WriteLine($"{color.Name} -> {color.Foreground?.GetBrush(null)}");
            }
            Debug.WriteLine("----------------------");
            */

            // Pass in so Colorizer no longer depends on UI control
            _typingColorizer = new TypingColorizer(CodeEditor.Document);

        }


        private void ApplySyntaxHighlighting(string language)
        {

            string fileName = $"{language?.ToLower() ?? "csharp"}.xshd";

            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string path = System.IO.Path.Combine(basePath, "Assets", "SyntaxHighlighting", fileName);

            if (System.IO.File.Exists(path))
            {
                using (var reader = new XmlTextReader(path))
                {
                    var highlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                    CodeEditor.SyntaxHighlighting = highlighting;
                }
            }
            else
            {
                Debug.WriteLine($"Syntax file not found: {path}");
            }

            // Send focus
            input.Focus();

            // Forces the keyboard specifically to hook into the textbox
            //Keyboard.Focus(input);

        }

        private void LoadLessonFolder_Click(object sender, RoutedEventArgs e)
        {


            using var dialog = new FolderBrowserDialog
            {
                Description = "Select Lesson Folder",
                UseDescriptionForTitle = true
            };



            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {

                if (DataContext is GameBoardViewModel vm)
                {
                    vm.LoadLessonFolder(dialog.SelectedPath);
                    
                    // Show folder name
                    if (vm.LessonFolderPath != null)
                    {
                        string lessonFolderPath = vm.LessonFolderPath;
                        string folderName = System.IO.Path.GetFileName(lessonFolderPath);
                        LessonGroupName.Text = folderName;
                    }
                }
            }




        }

        private void UpdateDebugMetrics()
        {
            // Debug.WriteLine("Updating metrics, " + DateTime.Now.ToString());
            currentWord.Text = _typingColorizer.CurrentWord;
            currentWordStartOffset.Text = _typingColorizer.CurrentWordStartCharacterLoc.ToString();
            currentWordEndOffset.Text = _typingColorizer.CurrentWordEndCharacterLoc.ToString();
            currentWordRelativeOffset.Text = _typingColorizer.NextCharacterLoc.ToString();
        }

        
        // Start all location counters at the beginning
        private void OnLessonLoaded()
        {
            typedwords.Text = "";

            // TODO
            _typingColorizer.CurrentLine = 0;

            // TODO Should we clear here?
            // Ensure Colorizer is starting over
            _typingColorizer.Mistakes.Clear();
            _typingColorizer.Underlines.Clear();

            _typingColorizer.CurrentWordStartCharacterLoc = 0;
            _typingColorizer.CurrentWordEndCharacterLoc = 0;


            //_typingColorizer.NextCharacterLoc = wordStart;

            //_typingColorizer.CurrentCharIndex = 0;
            //_typingColorizer.StartCharSelectionLineIndex = 0;


            if (DataContext is GameBoardViewModel vm)
            {
                _typingColorizer.CurrentWord = vm.Words[0];

                _typingColorizer.NextCharacterLoc = 1; // Since first word not yet typed

                typedwords.Clear();


                if (_typingColorizer.CurrentLine == 0)
                {
                    int? firstNonEmptyLine = FindFirstNonEmptyOffset();
                    if (firstNonEmptyLine != null)
                    {
                        _typingColorizer.CurrentLine = firstNonEmptyLine.Value;
                        //_typingColorizer.SelectFirstWordOfLine(firstNonEmptyLine.Value);
                    }
                }



                MoveToNextWord();


                //TODO, save the start and end offsets also
            }

            // TODO, is this neede?
            //CodeEditor.Document = new TextDocument(CodeEditor.Text);

            // Force TextView (the visual rendering layer of AvalonEdit) to repaint on the next render pass.
            //CodeEditor.TextArea.TextView.InvalidateVisual();

            // This is the correct one for colorizers
            CodeEditor.TextArea.TextView.Redraw();

            UpdateDebugMetrics();

            // Forces layout recalculation for a layout size change.
            //CodeEditor.TextArea.TextView.InvalidateMeasure();

            // Now we know text should be present, so add the transformer
            // CodeEditor.TextArea.TextView.LineTransformers.Add(_typingColorizer);
        }



        private void EnsureColorizerAttached()
        {
            if (CodeEditor.Document != null && CodeEditor.Document.TextLength > 0)
            {
                if (!CodeEditor.TextArea.TextView.LineTransformers.Contains(_typingColorizer))
                {
                    // This is suppoosed to call ColorizeLine()
                    CodeEditor.TextArea.TextView.LineTransformers.Add(_typingColorizer);
                    Debug.WriteLine("Just added a LineTransformers");
                }
            }
        }

        private void MoveToNextWord()
        {
            var doc = CodeEditor.Document;
            if (doc == null)
            {
                return;
            }

            // Store current line number before moving
            int oldLineNumber = doc.GetLineByOffset(_typingColorizer.CurrentWordStartCharacterLoc).LineNumber;

            //-----------
            // Find the start of the next word
            int searchOffset = _typingColorizer.CurrentWordEndCharacterLoc + 1; // Plus one move to character after the word

            // Just above, we moved past the end character location to what might be the space character
            // Loop until it is not a whitespace
            char charAtOffset = doc.GetCharAt(searchOffset);
            while (searchOffset < doc.TextLength && char.IsWhiteSpace(charAtOffset))
            {
                // We are trying to move passed white spaces at the end of the line only
                if (charAtOffset == '\n')
                {
                    searchOffset++;

                    // If the end of the document
                    if (searchOffset >= doc.TextLength)
                    {
                        Debug.WriteLine("End of document reached");

                        // Check if they reached the end before typing all characters of last word
                        if (input.Text.Length < _typingColorizer.CurrentWord.Length)
                        {
                            int lineNumber = oldLineNumber;
                            int neededCharsLength = _typingColorizer.CurrentWord.Length - input.Text.Length;

                            // Make sure the list exists for this line
                            if (!_typingColorizer.Mistakes.ContainsKey(lineNumber))
                            {
                                _typingColorizer.Mistakes[lineNumber] = new List<TypingColorizer.TextRange>();
                            }

                            // Colorize Red the remaining characters they did not type
                            for (int i = 0; i < neededCharsLength; i++)
                            {
                                _typingColorizer.Mistakes[lineNumber].Add(new TypingColorizer.TextRange(
                                    _typingColorizer.NextCharacterLoc + i,
                                    _typingColorizer.NextCharacterLoc + i + 1,
                                    Brushes.Red
                                ));
                            }

                            string placeHolderString = new string('\u25A1', neededCharsLength);
                            input.AppendText(placeHolderString);
                            // Make sure its in since
                            typedwords.AppendText(placeHolderString);

                            // Move caret to the end
                            input.CaretIndex = input.Text.Length;
                            input.ScrollToEnd();

                            // Update the next character so delete could technically work if we didn't go to the next lesson
                            _typingColorizer.NextCharacterLoc = _typingColorizer.CurrentWordEndCharacterLoc + 1;

                            Debug.WriteLine("Completed last word");

                        }

                        // Go to next lesson
                        if (DataContext is GameBoardViewModel vm)
                        {
                            // TODO, show message box before going to next lesson
                            vm.NextLesson();
                        }

                        return;
                    }

                    charAtOffset = doc.GetCharAt(searchOffset);
                    // Loop until a new line number is hit

                    int nextLineNumber = doc.GetLineByOffset(searchOffset).LineNumber;
                    while (searchOffset < doc.TextLength && (oldLineNumber == nextLineNumber))
                    {
                        searchOffset++;
                        nextLineNumber = doc.GetLineByOffset(searchOffset).LineNumber;
                    }

                    charAtOffset = doc.GetCharAt(searchOffset);
                    // Move the to first character on the new line
                    while (searchOffset < doc.TextLength && char.IsWhiteSpace(charAtOffset))
                    {
                        searchOffset++;
                        charAtOffset = doc.GetCharAt(searchOffset);
                    }


                    break;
                }

                searchOffset++;
                charAtOffset = doc.GetCharAt(searchOffset);
            }
            // We should have the start of the word, and it shouldn't be past the length of the field
            if (searchOffset >= doc.TextLength)
            {
                return;
            }
            int wordStart = searchOffset;
            //-----------


            //-----------
            // Find the end of the word by looping until a space is hit or the end of document reached
            while (searchOffset < doc.TextLength && !char.IsWhiteSpace(doc.GetCharAt(searchOffset)))
            {
                searchOffset++;
            }
            int wordEnd = searchOffset - 1; // Subtract one because the loop stopped when space was found or end of doc
            //-----------





            // Detect if the word is on a new line
            int newLineNumber = doc.GetLineByOffset(wordStart).LineNumber;
            if (newLineNumber != oldLineNumber)
            {
                Debug.WriteLine($"Moved to a new line: {newLineNumber}");
                // Add a new line to the input TextBox
                //input.AppendText(Environment.NewLine);
                //input.CaretIndex = input.Text.Length;
            }

            string curInputWord = input.Text;
            input.Clear();

            // SPECIAL CASE WHERE THEY PRESS SPACE WITHOUT TYPING ANYTHING
            // If user moved to text work without typing anything

            if (curInputWord == " " || 
                curInputWord.Length == 0 ||
                curInputWord.Length < _typingColorizer.CurrentWord.Length)
            {



                int lineNumber = doc.GetLineByOffset(_typingColorizer.CurrentWordStartCharacterLoc).LineNumber;

                Debug.WriteLine("WRONG, ensuring colorized on line: " + lineNumber);

                // Make sure the list exists for this line
                if (!_typingColorizer.Mistakes.ContainsKey(lineNumber))
                {
                    _typingColorizer.Mistakes[lineNumber] = new List<TypingColorizer.TextRange>();
                }

                // ----------
                // Add a new mistake for each character of the word that was skipped
                int neededCharsLength = _typingColorizer.CurrentWord.Length;
                if (curInputWord == " " || curInputWord.Length == 0)
                {
                    neededCharsLength = _typingColorizer.CurrentWord.Length;
                }
                else if (curInputWord.Length < _typingColorizer.CurrentWord.Length)
                {
                    //neededCharsLength = _typingColorizer.CurrentWord.Length - curInputWord.Length + 1;
                    neededCharsLength = _typingColorizer.CurrentWord.Length - curInputWord.Length;
                }
                for (int i = 0; i < neededCharsLength; i++)
                {
                    _typingColorizer.Mistakes[lineNumber].Add(new TypingColorizer.TextRange(
                        _typingColorizer.NextCharacterLoc + i,
                        _typingColorizer.NextCharacterLoc + i + 1,
                        Brushes.Red
                    ));
                }

                string placeHolderString = new string('\u25A1', neededCharsLength);
                Debug.WriteLine($"Adding placeholder: {placeHolderString}, CurrentWord is: {_typingColorizer.CurrentWord}");
                typedwords.AppendText(placeHolderString);
                // ----------

            }


            //-----
            // Update the offsets to make the next word the current word
            _typingColorizer.CurrentWordStartCharacterLoc = wordStart;
            _typingColorizer.CurrentWordEndCharacterLoc = wordEnd;
            _typingColorizer.NextCharacterLoc = wordStart;

            // Save the new current word
            int start = _typingColorizer.CurrentWordStartCharacterLoc;
            int end = _typingColorizer.CurrentWordEndCharacterLoc;

            // The start and end could be the same if the line has only one character
            if (start >= 0 && end >= start && end <= doc.TextLength)
            {
                int wordLength = (end - start) + 1;
                _typingColorizer.CurrentWord = doc.GetText(start, wordLength);
            }
            //-----

            // We no longer append since we append as we go
            //typedwords.AppendText(Environment.NewLine + curInputWord);

            // If nothing was typed then fill the line with box characters
            if (_typingColorizer.CurrentWord.Length == 0)
            {

            }

            // We just completed a word so add a new line
            typedwords.AppendText(Environment.NewLine);

            // Update current line in colorizer
            _typingColorizer.CurrentLine = newLineNumber;

        }

        private void RemoveLastLine()
        {
            typedwords.UpdateLayout();

            // typedwords should be 'hidden' so it is still in the layout
            if (typedwords.LineCount == 0)
            {
                return;
            }

            int lastLineIndex = typedwords.LineCount - 1;

            int lineStart = typedwords.GetCharacterIndexFromLineIndex(lastLineIndex);

            typedwords.Text = typedwords.Text.Substring(0, lineStart).TrimEnd('\r', '\n');

            typedwords.CaretIndex = typedwords.Text.Length;
        }


        // Remove the last character of the saved words that have been typed
        private void RemoveLastCharacterOfTypedWords()
        {
            if (typedwords.Text.Length == 0)
            {
                return;
            }

            // Get the text of the TextBox
            string text = typedwords.Text;

            // Find the previous newline
            int lastNewLineIndex = text.LastIndexOf('\n');

            int lineStart = (lastNewLineIndex == -1) ? 0 : lastNewLineIndex + 1;
            int charactersInCurrentLine = text.Length - lineStart;

            if (charactersInCurrentLine == 1)
            {
                // Only one character on this line → remove entire line
                if (lastNewLineIndex >= 0)
                {
                    typedwords.Text = text.Remove(lastNewLineIndex);
                }
                else
                {
                    typedwords.Clear();
                }
            }
            else
            {
                // Normal backspace
                int lastCharIndex = text.Length - 1;
                typedwords.Text = text.Remove(lastCharIndex, 1);
            }

            typedwords.CaretIndex = typedwords.Text.Length;
        }


        private void MoveLastLineToInput()
        {
            typedwords.UpdateLayout();

            // typedwords should be 'hidden' so it is still in the layout
            if (typedwords.LineCount == 0)
            {
                return;
            }

            int lastLineIndex = typedwords.LineCount - 1;

            int lineStart = typedwords.GetCharacterIndexFromLineIndex(lastLineIndex);

            int lineEnd;

            if (lastLineIndex < typedwords.LineCount - 1)
            {
                // Not last visual line
                lineEnd = typedwords.GetCharacterIndexFromLineIndex(lastLineIndex + 1);
            }
            else
            {
                // Final line — go to end of text
                lineEnd = typedwords.Text.Length;
            }

            string lastLine = typedwords.Text.Substring(lineStart, lineEnd - lineStart);

            Debug.WriteLine("Last line is: " + lastLine);

            input.Text = lastLine;
            input.CaretIndex = input.Text.Length;
        }


        // Handle special keys of SPACE, RETURN, BACKSPACE
        private void InputBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            UpdateDebugMetrics();

            // Ignore modifier keys
            if (e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            {
                return;
            }


            if (_typingColorizer.CurrentLine == 0)
            {
                // Find the first non empty line
                int? firstNonEmptyLine = FindFirstNonEmptyOffset();
                if (firstNonEmptyLine != null)
                {
                    Debug.WriteLine("@@First non empty offset is: " + firstNonEmptyLine.Value);
                    _typingColorizer.CurrentLine = firstNonEmptyLine.Value;
                    _typingColorizer.SelectFirstWordOfLine(firstNonEmptyLine.Value);
                }
                Debug.WriteLine("@FIRST previwkeydown NONEMPTY LINE AT: " + _typingColorizer.CurrentLine);
            }


            if (e.Key == Key.Space)
            {
                Debug.WriteLine("SPACE KEY PRESSED");
                MoveToNextWord();
                // Don't allow space to propogate or we'll have a space at the front of the next word
                e.Handled = true;
            }
            else if (e.Key == Key.Return)
            {
                Debug.WriteLine("RETRUN KEY PRESSED");
                MoveToNextWord();
                // Don't allow return to propogate
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                Debug.WriteLine("BACK KEY PRESSED");

                // If deleting the current word
               if (_typingColorizer.NextCharacterLoc > _typingColorizer.CurrentWordStartCharacterLoc)
                {

                    // Debug.WriteLine("**CurWordOffset: " + _typingColorizer.CurrentWordStartCharacterLoc + ", CurTypeOffset: " + _typingColorizer.NextCharacterLoc);

                    // Remove the last character of the saved words that have been typed
                    RemoveLastCharacterOfTypedWords();

                    // We allow the delete to remove the character from the 'input' Textbox
                    // However, skip everything if the typed word is > input word


                    _typingColorizer.NextCharacterLoc--;

                    // Remove mistake at this offset if it exists
                    int lineNumber = CodeEditor.Document
                        .GetLineByOffset(_typingColorizer.NextCharacterLoc)
                        .LineNumber;

                    if (_typingColorizer.Mistakes.TryGetValue(lineNumber, out var mistakes))
                    {
                        // Find all mistakes that matches the current offset and remove
                        int removedCount = mistakes.RemoveAll(
                            m => m.Start == _typingColorizer.NextCharacterLoc);

                        if (removedCount > 0)
                        {
                            Debug.WriteLine($"Removed {removedCount} mistake(s) at offset {_typingColorizer.NextCharacterLoc} on line {lineNumber}");
                        }
                        else
                        {
                            Debug.WriteLine("!!!No mistakes removed");
                        }

                        if (mistakes.Count == 0)
                        {
                            _typingColorizer.Mistakes.Remove(lineNumber);
                        }
                    }


                }
                else
                {
                    // If deleted to move to previous word
                    // At this point, CurrentTypedOffset matches start of word and are at beginning of word

                    Debug.WriteLine("Deleting to previous word");

                    // Make sure at least one word typed before moving back
                    if (_typingColorizer.CurrentWordStartCharacterLoc > 0)
                    {
                        // Note, CurrentTypedOffset has already been subracted
                        //RemoveLastCharacter();
                        Debug.WriteLine("BACK KEY PRESSED - Calling RemoveLastLine()");
                        RemoveLastLine();
                        SelectPreviousWord();

                        // Don't allow delete to propogate or we'll be missing a character
                        e.Handled = true;
                        MoveLastLineToInput();
                    }
                }




            }


            EnsureColorizerAttached();
            CodeEditor.TextArea.TextView.Redraw();

            UpdateDebugMetrics();
        }


        // Starts at 1
        private int? FindFirstNonEmptyOffset()
        {
            var doc = CodeEditor?.Document;
            if (doc == null)
            {
                return null;
            }

            foreach (var line in doc.Lines)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                string text = doc.GetText(line);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return line.Offset + 1;
                }
            }

            return null;
        }


        // NORMAL CHARACTERS TYPED
        private void InputBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            UpdateDebugMetrics();

            // Make sure we don't add characters past word length
            // We could allow this, but how to hilight the word if all prev chars are correct?
            // Its easier to just prevent it
            if (input.Text.Length + 1 > _typingColorizer.CurrentWord.Length)
            {
                e.Handled = true; // prevent inserting character into input textbox
                return;
            }


            var doc = CodeEditor.Document;
            if (doc == null || doc.TextLength == 0)
            {
                return;
            }
            

            char pressed = e.Text[0];
            // Save character to the typedwords textbox
            typedwords.AppendText(pressed.ToString());
            // If they typed more characters than current word, don't process anymore
            if ((_typingColorizer.NextCharacterLoc - 1) >= _typingColorizer.CurrentWordEndCharacterLoc)
            {
                return;
            }

            char expected = doc.GetCharAt(_typingColorizer.NextCharacterLoc);
            int lineNumber = doc.GetLineByOffset(_typingColorizer.NextCharacterLoc).LineNumber;


            // --------------------
            // Speak if offset is found
            if (DataContext is GameBoardViewModel vm)
            {
                // If there is a SpeechCue loaded from the the lesson file that starts on this character
                if (vm.SpeechCues.TryGetValue(_typingColorizer.NextCharacterLoc, out var cues))
                {
                    // Make sure the Underlines list exists for this line
                    if (!_typingColorizer.Underlines.ContainsKey(lineNumber))
                    {
                        _typingColorizer.Underlines[lineNumber] = new List<TypingColorizer.TextRange>();
                    }

                    foreach (var cue in cues)
                    {
                        // Speak the phrase
                        synthesizer.SpeakAsync(cue.TextSpeak);

                        // Put words into the speakwords textbox
                        textspeak.Text = cue.TextSpeak;
                        textinfo.Text = cue.TextInfo;

                        // Add underline if needed
                        if (cue.End >= cue.Start)
                        {
                            _typingColorizer.Underlines[lineNumber].Add(
                                new TypingColorizer.TextRange(
                                    cue.Start,
                                    cue.End,
                                    Brushes.Yellow
                                )
                            );
                        }
                    }
                }
            }
            // --------------------




            if (pressed != expected)
            {
                Debug.WriteLine("WRONG, expected: " + expected + ", got: " + pressed);

                // Make sure the list exists for this line
                if (!_typingColorizer.Mistakes.ContainsKey(lineNumber))
                {
                    _typingColorizer.Mistakes[lineNumber] = new List<TypingColorizer.TextRange>();
                }

                // Add a new mistake for this character
                _typingColorizer.Mistakes[lineNumber].Add(new TypingColorizer.TextRange(
                    _typingColorizer.NextCharacterLoc,
                    _typingColorizer.NextCharacterLoc + 1,
                    Brushes.Red
                ));
            }

            // Advance typed offset in any case
            _typingColorizer.NextCharacterLoc++;

            EnsureColorizerAttached();
            CodeEditor.TextArea.TextView.Redraw();

            UpdateDebugMetrics();

            //e.Handled = true; // prevent AvalonEdit from inserting the character
        }


        private void SelectPreviousWord()
        {
            var doc = CodeEditor.Document;
            if (doc == null || doc.TextLength == 0)
            {
                return;
            }

            // Since we are selecting the previous word, start at the beginning of the current word minus one
            int prevCharLoc = _typingColorizer.CurrentWordStartCharacterLoc - 1;

            // Find current line
            var line = doc.GetLineByOffset(prevCharLoc);
            int lineNumber = line.LineNumber;

            // TODO, do we ned this?
            string text = doc.GetText(line);

            // Calculate position relative to start of line
            //int posInLine = prevCharLoc - line.Offset;

            // If we are at the start of the line, move to previous line if exists
            if (prevCharLoc == 0)
            {
                if (lineNumber == 1)
                {
                    // Already at very beginning of document
                    return;
                }

                line = doc.GetLineByNumber(lineNumber - 1);
                lineNumber = line.LineNumber;
                text = doc.GetText(line);
                prevCharLoc = text.Length;
            }

            // Step back to find the end of the previous word
            int endOfPrevWordLoc = prevCharLoc;

            //TODO, crashes when going back from line 3 to line 2

            // Skip trailing whitespace
            //char aPrevChar = text[endOfPrevWordLoc - 1]; // Subract 1 because text is zero based
            //while ((endOfPrevWordLoc - 1) > 0 && char.IsWhiteSpace(aPrevChar))
            //{
                //endOfPrevWordLoc--;
                //aPrevChar = text[endOfPrevWordLoc - 1];
            //}

            char aPrevChar = doc.GetCharAt(endOfPrevWordLoc);
            while (endOfPrevWordLoc >= 0 && char.IsWhiteSpace(aPrevChar))
            {
                endOfPrevWordLoc--;
                aPrevChar = doc.GetCharAt(endOfPrevWordLoc);
            }




            if (endOfPrevWordLoc < 0)
            {
                // No previous word on this line, try previous line
                if (lineNumber == 1)
                {
                    return;
                }

                line = doc.GetLineByNumber(lineNumber - 1);
                lineNumber = line.LineNumber;
                text = doc.GetText(line);
                endOfPrevWordLoc = text.Length - 1;

                // Skip trailing whitespace
                while (endOfPrevWordLoc >= 0 && char.IsWhiteSpace(text[endOfPrevWordLoc]))
                {
                    endOfPrevWordLoc--;
                }

                if (endOfPrevWordLoc < 0)
                {
                    return; // previous line is empty
                }
            }

            // Now `i` is at the last character of the previous word
            int wordEnd = endOfPrevWordLoc;

            // Find the start of the word
            int startOfPrevWordLoc = wordEnd; // At this point, this is the end of the previous word
            //aPrevChar = text[startOfPrevWordLoc -1]; // Minuse 1 since text is zero based but everything is 1 based
            aPrevChar = doc.GetCharAt(startOfPrevWordLoc);
            while (startOfPrevWordLoc > 1 && !char.IsWhiteSpace(aPrevChar))
            {
                startOfPrevWordLoc--;
                aPrevChar = doc.GetCharAt(startOfPrevWordLoc);
            }

            // We ended on a space or return
            if (char.IsWhiteSpace(aPrevChar))
            {
                startOfPrevWordLoc++;
            }

            int wordStart = startOfPrevWordLoc;

            //----
            // Update TypingColorizer offsets
            //_typingColorizer.CurrentWordStartCharacterLoc = line.Offset + wordStart;
            //_typingColorizer.CurrentWordEndCharacterLoc = line.Offset + wordEnd;
            _typingColorizer.CurrentWordStartCharacterLoc = wordStart;
            _typingColorizer.CurrentWordEndCharacterLoc = wordEnd;

            //_typingColorizer.NextCharacterLoc = _typingColorizer.CurrentWordEndCharacterLoc;
            _typingColorizer.NextCharacterLoc = wordEnd + 1;

            // Save the new current word
            int start = _typingColorizer.CurrentWordStartCharacterLoc;
            int end = _typingColorizer.CurrentWordEndCharacterLoc;
            char theStartChar = doc.GetCharAt(start);
            char theEndChar = doc.GetCharAt(end);
            Debug.WriteLine("Star char: " + theStartChar + ", End char: " + theEndChar);

            if (start >= 0 && end >= start && end <= doc.TextLength)
            {
                int wordLength = (end - start) + 1;
                _typingColorizer.CurrentWord = doc.GetText(start, wordLength);
            }
            //----


            // Update current line
            _typingColorizer.CurrentLine = lineNumber;

            Debug.WriteLine($"Moved back to previous word on line {lineNumber}, offsets {wordStart}-{wordEnd}");
        }

        private void CodeEditor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Only zoom when Ctrl is pressed
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                e.Handled = true; // Prevent normal scrolling

                double delta = e.Delta > 0 ? 1 : -1;

                double newValue = FontSizeSlider.Value + delta;

                // Clamp manually to slider bounds
                newValue = Math.Max(FontSizeSlider.Minimum,
                                    Math.Min(FontSizeSlider.Maximum, newValue));

                FontSizeSlider.Value = newValue;
            }
        }

        private void Logo_Click(object sender, RoutedEventArgs e)
        {
            //synthesizer.Speak("Hello! I am using the first available voice.");
            synthesizer.SpeakAsync("This will speak in the background.");

            string theMsg = "TypingTempest" + Environment.NewLine + Environment.NewLine;
            theMsg += "By David S. Shelley - (2026)" + Environment.NewLine + Environment.NewLine;
            System.Windows.MessageBox.Show(theMsg);
        }


        private void CodeEditor_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var editor = sender as TextEditor;

            // Get the position of the click relative to the text view
            var pos = e.GetPosition(editor.TextArea.TextView);

            // Map point to a text offset
            var logicalPosition = editor.TextArea.TextView.GetPositionFloor(pos);

            if (logicalPosition != null)
            {
                int offset = editor.Document.GetOffset(logicalPosition.Value.Line, logicalPosition.Value.Column);

                // offset is the character index in the document
                string clickedChar = editor.Document.GetText(offset, 1);

                clickedCharOffset.Text = offset.ToString();

                //Debug.WriteLine($"Clicked character: '{clickedChar}' at offset {offset}");
            }
        }


        private void Debug_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void Debug_Unchecked(object sender, RoutedEventArgs e)
        {

        }
    }



}
