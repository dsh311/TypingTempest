using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace TypingTempest.Controls.GameBoardView
{
    public class TypingColorizer : DocumentColorizingTransformer
    {


        private readonly TextDocument _document;

        public TypingColorizer(TextDocument document)
        {
            _document = document;
        }


        public struct TextRange
        {
            public int Start; // absolute offset
            public int End;   // absolute offset
            public Brush Brush;

            public TextRange(int start, int end, Brush brush)
            {
                Start = start;
                End = end;
                Brush = brush;
            }
        }


        // Map line number to all mistyped characters for that line
        public Dictionary<int, List<TextRange>> Mistakes { get; } = new Dictionary<int, List<TextRange>>();

        public Dictionary<int, List<TextRange>> Underlines { get; } = new Dictionary<int, List<TextRange>>();

        public string CurrentWord;
        public int CurrentWordStartCharacterLoc;
        public int CurrentWordEndCharacterLoc;
        public int NextCharacterLoc;   // progress inside current word


        public int CurrentLine { get; set; } = 0;      // 1-based, 0 means not set
                                                       //public int CurrentCharIndex { get; set; } = 0; // 0-based


        //public int StartCharSelectionLineIndex { get; set; } = 0; // 0-based
        //public int EndCharSelectionLineIndex { get; set; } = 0; // 0-based


        public void SelectFirstWordOfLine(int lineNumber)
        {
            if (_document == null)
            {
                return;
            }

            if (lineNumber < 1 || lineNumber > _document.LineCount)
            {
                return;
            }

            var line = _document.GetLineByNumber(lineNumber);
            string text = _document.GetText(line);

            int index = 0;

            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            int wordStart = index;

            while (index < text.Length && !char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            int wordEnd = index;

            CurrentWordStartCharacterLoc = line.Offset + wordStart;
            
            // Subtract 1 since the first line could start at offset 1
            CurrentWordEndCharacterLoc = (line.Offset + wordEnd) - 1;
            NextCharacterLoc = CurrentWordStartCharacterLoc;
        }


        protected override void ColorizeLine(DocumentLine line)
        {
            //Debug.WriteLine($"LINE #{line.LineNumber}, Length:{line.Length}");

            int lineStart = line.Offset;
            int lineEnd = line.EndOffset;

            // If the line is empty, return
            if (lineStart >= lineEnd)
            {
                // Debug.WriteLine("Line start >= line end, start=" + lineStart + "," + " end=" + lineEnd + ", like number: " + line.LineNumber + ", returning");
                return;
            }

            // Highlight full current word background
            int wordStart = Math.Max(CurrentWordStartCharacterLoc, lineStart);
            int wordEnd = Math.Min(CurrentWordEndCharacterLoc, lineEnd);

            //Debug.WriteLine($"Word start from {wordStart} to {wordEnd}");
            if (wordStart < wordEnd)
            {
                var brush = new SolidColorBrush(Color.FromRgb(38, 79, 120));
                //Debug.WriteLine("Calling ChangeLinePart for word background");

                // Add one to wordEnd because the endOffset is exclusive
                ChangeLinePart(wordStart, wordEnd + 1, element =>
                {
                    element.TextRunProperties.SetBackgroundBrush(brush);
                });
            }

            // Highlight typed portion foreground
            int typedStart = Math.Max(CurrentWordStartCharacterLoc, lineStart);
            int typedEnd = Math.Min(NextCharacterLoc, lineEnd); // Use next char loc because the endOffset is exclusive

            //Debug.WriteLine($"Typed start from {typedStart} to {typedEnd}");
            if (typedStart < typedEnd)
            {
                //Debug.WriteLine("Calling ChangeLinePart for typed characters (lime)");
                ChangeLinePart(typedStart, typedEnd, element =>
                {
                    element.TextRunProperties.SetForegroundBrush(Brushes.Lime);
                });
            }

            // Highlight mistyped characters in red
            if (Mistakes.TryGetValue(line.LineNumber, out var mistakes))
            {
                // Debug.WriteLine($"   Found {mistakes.Count} mistakes for line {line.LineNumber}");
                foreach (var mistake in mistakes)
                {
                    int start = Math.Max(mistake.Start, lineStart);
                    int end = Math.Min(mistake.End, lineEnd);
                    // Debug.WriteLine($"   Mistake from {start} to {end}, color: {mistake.Brush}");

                    if (start < end)
                    {
                        ChangeLinePart(start, end, element =>
                        {
                            // Colorize character
                            element.TextRunProperties.SetForegroundBrush(mistake.Brush);

                            // Add underline looked bad
                            //element.TextRunProperties.SetTextDecorations(TextDecorations.Underline);
                        });
                    }
                }
            }
            else
            {
                //Debug.WriteLine($"No mistakes recorded for line {line.LineNumber}");
            }


            // Highlight underlined characters
            if (Underlines.TryGetValue(line.LineNumber, out var underlines))
            {
                foreach (var underline in underlines)
                {
                    int start = Math.Max(underline.Start, lineStart);
                    int end = Math.Min(underline.End, lineEnd);

                    if (start < end)
                    {
                        ChangeLinePart(start, end, element =>
                        {
                            var underline = new TextDecoration
                            {
                                Location = TextDecorationLocation.Underline,
                                Pen = new Pen(Brushes.Green, 1),
                                PenThicknessUnit = TextDecorationUnit.FontRecommended
                            };

                            element.TextRunProperties.SetTextDecorations(
                                new TextDecorationCollection { underline });
                        });
                    }
                }
            }



        } // End of ColorizeLine





        // AvalonEdit calls this for each visible DocumentLine,
        /*
        protected override void ColorizeLine(DocumentLine line)
        {
            Debug.WriteLine($"LINE #{line.LineNumber}, Length:{line.Length}");

            
            // Since ColorizeLine is called for every line, skip lines we aren't interested in
            if (line.LineNumber != CurrentLine)
            {
                return;
            }

            // Skip empty lines
            if (line.Length == 0)
            {
                return;
            }


            int start = line.Offset + StartCharSelectionLineIndex - 1;
            int end = line.Offset + EndCharSelectionLineIndex;

            // Clamp to line boundaries
            start = Math.Max(start, line.Offset);
            end = Math.Min(end, line.EndOffset);


            Debug.WriteLine($"Start from {start} to {end}");

            if (start >= end)
            {
                return;
            }

            Debug.WriteLine($"Coloring from {start} to {end}");

            ChangeLinePart(start, end, element =>
            {
                element.TextRunProperties.SetForegroundBrush(Brushes.Lime);
            });
        }
        */


    }



}
