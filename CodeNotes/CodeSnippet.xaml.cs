using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace CodeNotes
{
    /// <summary>
    /// Interaction logic for CodeSnippet.xaml
    /// </summary>
    public partial class CodeSnippet : UserControl
    {
        internal enum RunType
        {
            Standard,
            Syntax,
            String
        }

        internal readonly ref struct RunBuilder
        {
            private readonly ReadOnlySpan<char> _content;
            private readonly Stack<RunInfo> _runInfos;
            private readonly List<Run> _runs;

            public RunBuilder(ReadOnlySpan<char> content)
            {
                _content = content;
                _runInfos = new Stack<RunInfo>();
                _runs = new List<Run>();
            }

            public void Add(int startIndex, int endIndex)
            {
                // Optimize by combining contiguous non-highlighted runs.
                RunInfo newInfo = new(_content, startIndex, endIndex, false);
                if (_runInfos.Count > 0 && newInfo.RunType == RunType.Standard && _runInfos.Peek().RunType == RunType.Standard)
                {
                    RunInfo oldInfo = _runInfos.Pop();
                    _runInfos.Push(new RunInfo(_content, oldInfo.StartIndex, newInfo.EndIndex, false));
                }
                else
                {
                    _runInfos.Push(newInfo);
                }
            }

            public void AddString(int startIndex, int endIndex)
            {
                _runInfos.Push(new RunInfo(_content, startIndex, endIndex, true));
            }

            public List<Run> BuildResult()
            {
                int currentIndex = 0;
                int finalIndex = _content.Length;

                foreach (RunInfo info in _runInfos.Reverse())
                {
                    // Everything before the syntax
                    if (info.StartIndex > currentIndex)
                    {
                        _runs.Add(new Run()
                        {
                            Foreground = DefaultBrush,
                            Text = _content[currentIndex..info.StartIndex].ToString()
                        });
                    }

                    // Syntax
                    _runs.Add(info.ToRun());
                    currentIndex = info.EndIndex + 1;
                }

                return _runs;
            }
        }

        internal readonly struct RunInfo
        {
            public int StartIndex { get; }
            public int EndIndex { get; }
            public string Content { get; }
            public SolidColorBrush Brush { get; }
            public RunType RunType { get; }

            public RunInfo(ReadOnlySpan<char> parentSpan, int startIndex, int endIndex, bool isString)
            {
                StartIndex = startIndex;
                EndIndex = endIndex;
                Content = parentSpan[startIndex..(endIndex + 1)].ToString();

                if (isString)
                {
                    Brush = StringBrush;
                    RunType = RunType.String;
                }
                else if (ColorDict.TryGetValue(Content, out SolidColorBrush brush))
                {
                    Brush = brush;
                    RunType = RunType.Syntax;
                }
                else
                {
                    Brush = DefaultBrush;
                    RunType = RunType.Standard;
                }
            }

            public Run ToRun()
            {
                return new()
                {
                    Foreground = Brush,
                    Text = Content
                };
            }
        }

        private static readonly BrushConverter BrushConverter = new();

        private static readonly SolidColorBrush DefaultBrush = (SolidColorBrush)BrushConverter.ConvertFromString("#EEEEEE");
        private static readonly SolidColorBrush BlueBrush = (SolidColorBrush)BrushConverter.ConvertFromString("#3B99C9");
        private static readonly SolidColorBrush MagentaBrush = (SolidColorBrush)BrushConverter.ConvertFromString("#C685BE");
        private static readonly SolidColorBrush GreenBrush = (SolidColorBrush)BrushConverter.ConvertFromString("#49C9B1");
        private static readonly SolidColorBrush StringBrush = (SolidColorBrush)BrushConverter.ConvertFromString("#CF9279");

        private static Dictionary<string, SolidColorBrush> ColorDict;

        private static readonly string[] BlueKeywords = new string[]
        {
            "class"
        };

        private static readonly string[] MagentaKeywords = new string[]
        {
            "import",
            "from",
            "export"
        };

        private static readonly string[] Classes = new string[]
        {
            "TestComponent",
            "Component",
        };

        private static readonly HashSet<char> SymbolChars = new()
        {
            '.',
            ',',
            ';',
            '=',
            '+',
            '-',
            '*',
            '{',
            '}',
            '(',
            ')',
            '[',
            ']',
            '@'
        };

        public CodeSnippet()
        {
            InitializeComponent();
            InitColorDict();
            txtInput.TextChanged += TxtInput_TextChanged;
        }

        private static void InitColorDict()
        {
            if (ColorDict != null)
                return;

            ColorDict = new Dictionary<string, SolidColorBrush>();
            AddWordsToColorDict(BlueKeywords, BlueBrush);
            AddWordsToColorDict(MagentaKeywords, MagentaBrush);
            AddWordsToColorDict(Classes, GreenBrush);
        }

        private static void AddWordsToColorDict(IEnumerable<string> words, SolidColorBrush brush)
        {
            foreach (string word in words)
            {
                ColorDict.Add(word, brush);
            }
        }

        private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ReadOnlySpan<char> charSpan = txtInput.Text.AsSpan();
            int startIndex = 0;
            int lastIndex = charSpan.Length - 1;
            int startStringIndex = 0;

            bool insideString = false;

            RunBuilder runBuilder = new(charSpan);

            for (int i = 0; i < charSpan.Length; i++)
            {
                if (insideString)
                {
                    if (charSpan[i] == '\\')
                    {
                        // Ignore escape character in string
                        i += 1;
                    }
                    else if (charSpan[i] is '\'' or '\"')
                    {
                        // Open string
                        runBuilder.AddString(startStringIndex, i);
                        insideString = false;
                        startIndex = i + 1;
                    }

                    continue;
                }

                // Close string
                if (charSpan[i] is '\'' or '\"')
                {
                    insideString = true;
                    startStringIndex = i;
                    continue;
                }

                if (char.IsWhiteSpace(charSpan[i]) || SymbolChars.Contains(charSpan[i]))
                {
                    runBuilder.Add(startIndex, i - 1);
                    runBuilder.Add(i, i);
                    startIndex = i + 1;
                }
            }

            if (startIndex <= lastIndex)
            {
                if (insideString)
                {
                    runBuilder.Add(startIndex, startStringIndex - 1);
                    runBuilder.AddString(startStringIndex, lastIndex);
                }
                else
                    runBuilder.Add(startIndex, lastIndex);
            }

            txtDisplay.Inlines.Clear();
            txtDisplay.Inlines.AddRange(runBuilder.BuildResult());
        }
    }
}
