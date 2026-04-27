using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TypingTempest.Controls.MainMenuView
{
    class RainColumn
    {
        public TextBlock TextBlock { get; init; }
        public int MaxLines { get; set; }
        public int RainLength { get; set; }
        public double CharHeight { get; set; }
    }


    /// <summary>
    /// Interaction logic for MainMenuView.xaml
    /// </summary>
    public partial class MainMenuView : UserControl
    {
        private readonly List<RainColumn> _columns = new();
        private readonly Random _rand = new();
        private DispatcherTimer _timer;

        private double _lastWidth;
        private double _lastHeight;

        private const string CharPool = "abcdefghijklmnopqrstuvwxyz0123456789";


        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CreateRainColumns();
            StartRain();
        }


        public MainMenuView()
        {
            InitializeComponent();

            Loaded += (_, __) => StartRain();
            Unloaded += (_, __) => StopRain();

            SizeChanged += MainMenuView_SizeChanged;
        }

        private void StopRain()
        {
            _timer.Stop();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            // Call StartGame on MainWindow
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.StartGame();
            }
        }

        private void MainMenuView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Ignore first layout pass
            if (ActualWidth < 10 || ActualHeight < 10)
                return;

            // Avoid excessive rebuilds
            if (Math.Abs(ActualWidth - _lastWidth) < 10 &&
                Math.Abs(ActualHeight - _lastHeight) < 10)
                return;

            _lastWidth = ActualWidth;
            _lastHeight = ActualHeight;

            CreateRainColumns();
        }

        private void StartRain()
        {
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(40)
            };
            _timer.Tick += (_, _) => MakeRain();
            _timer.Start();
        }

        private void MakeRain()
        {
            foreach (var col in _columns)
            {
                Rain(col);
            }
        }

        /*
        private void Rain(RainColumn col)
        {
            var lines = col.TextBlock.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

            string nextChar = CharPool[_rand.Next(CharPool.Length)].ToString();

            if (lines.Count < col.RainLength)
                lines.Insert(0, nextChar);
            else
                lines.Insert(0, " ");

            if (lines.Count > col.MaxLines)
                lines.RemoveAt(lines.Count - 1);

            if (lines.LastOrDefault() == " ")
                lines.Clear();

            // Clear previous inlines
            col.TextBlock.Inlines.Clear();

            for (int i = 0; i < lines.Count; i++)
            {
                byte green = (byte)Math.Max(50, 255 - i * 8);
                var run = new Run(lines[i] + "\n")
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(0, green, 0))
                };
                col.TextBlock.Inlines.Add(run);
            }
        }
        */

        
        private void Rain(RainColumn col)
        {
            var lines = col.TextBlock.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();

            string nextChar = CharPool[_rand.Next(CharPool.Length)].ToString();

            if (lines.Count < col.RainLength)
                lines.Insert(0, nextChar);
            else
                lines.Insert(0, " ");

            if (lines.Count > col.MaxLines)
                lines.RemoveAt(lines.Count - 1);

            if (lines.LastOrDefault() == " ")
                lines.Clear();

            col.TextBlock.Text = string.Join("\n", lines);

            ApplyGradient(col, lines.Count);
        }

        private void ApplyGradient(RainColumn col, int depth)
        {
            byte green = (byte)Math.Max(50, 255 - depth * 8);
            col.TextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0, green, 0));
        }
        

        private void CreateRainColumns()
        {
            RainCanvas.Children.Clear();
            _columns.Clear();

            double columnWidth = 20; // spacing between columns
            int columnCount = (int)(ActualWidth / columnWidth);

            for (int i = 0; i < columnCount; i++)
            {
                // TextBlocks are lightweight, read-only display elements
                // TextBlocks meant for rendering text faster than TextBox.
                var tb = new TextBlock
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = _rand.Next(14, 22),
                    Foreground = Brushes.LimeGreen,
                    TextAlignment = TextAlignment.Center
                };

                Canvas.SetLeft(tb, i * columnWidth);
                Canvas.SetTop(tb, 0);

                RainCanvas.Children.Add(tb);

                double charHeight = tb.FontSize * 1.2;


                int maxLines = (int)(ActualHeight / charHeight);

                int rainMin = Math.Min(8, maxLines); // or 1, if you want at least one line
                int rainMax = Math.Max(rainMin, maxLines);

                _columns.Add(new RainColumn
                {
                    TextBlock = tb,
                    MaxLines = maxLines,
                    RainLength = _rand.Next(rainMin, rainMax + 1), // +1 because Next max is exclusive
                    CharHeight = charHeight
                });

            }
        }



    }
}
