using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    public partial class DeepSeekChatPanel : UserControl
    {
        private readonly DeepSeekClient _client;
        private bool _isProcessing;

        // Referencias diretas aos controles (sem FindName)
        private RichTextBox _chatHistory;
        private TextBlock _statusText;
        private TextBox _inputBox;
        private Button _sendButton;
        private Button _execButton;

        public DeepSeekChatPanel(string apiKey)
        {
            InitializeComponent();
            _client = new DeepSeekClient(apiKey);
        }

        public DeepSeekChatPanel() : this(DeepSeekConfig.LoadApiKey())
        {
        }

        private void InitializeComponent()
        {
            // Container principal com 4 linhas
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // chat
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // status
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // input
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // exec button
            mainGrid.Margin = new Thickness(0);
            mainGrid.Background = new SolidColorBrush(Color.FromRgb(40, 40, 45));

            // Row 0: Historico do chat
            _chatHistory = new RichTextBox
            {
                IsReadOnly = true,
                Background = new SolidColorBrush(Color.FromRgb(33, 33, 38)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                Padding = new Thickness(8),
                Margin = new Thickness(0)
            };
            Grid.SetRow(_chatHistory, 0);

            // Row 1: Barra de status
            _statusText = new TextBlock
            {
                Text = "DeepSeek pronto. Digite sua pergunta abaixo.",
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 145)),
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 55)),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11
            };
            Grid.SetRow(_statusText, 1);

            // Row 2: Input area
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputGrid.Background = new SolidColorBrush(Color.FromRgb(50, 50, 55));
            Grid.SetRow(inputGrid, 2);

            _inputBox = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(55, 55, 60)),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                AcceptsReturn = false,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                ToolTip = "Digite sua pergunta sobre Civil 3D. Pressione Enter para enviar."
            };
            _inputBox.KeyDown += OnInputKeyDown;
            Grid.SetColumn(_inputBox, 0);

            _sendButton = new Button
            {
                Content = "Enviar",
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 6, 14, 6),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0)
            };
            _sendButton.Click += OnSendClick;
            Grid.SetColumn(_sendButton, 1);

            inputGrid.Children.Add(_inputBox);
            inputGrid.Children.Add(_sendButton);

            // Row 3: Botao executar comando
            _execButton = new Button
            {
                Content = "▶ Executar Comando",
                Background = new SolidColorBrush(Color.FromRgb(0, 150, 80)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0),
                Height = 32,
                ToolTip = "Executa o ultimo comando sugerido pelo DeepSeek no Civil 3D"
            };
            _execButton.Click += OnExecClick;
            Grid.SetRow(_execButton, 3);

            mainGrid.Children.Add(_chatHistory);
            mainGrid.Children.Add(_statusText);
            mainGrid.Children.Add(inputGrid);
            mainGrid.Children.Add(_execButton);

            Content = mainGrid;
        }

        private async void OnSendClick(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !_isProcessing)
            {
                e.Handled = true;
                await SendMessageAsync();
            }
        }

        private async Task SendMessageAsync()
        {
            if (_isProcessing) return;

            var message = _inputBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(message)) return;

            AppendToChat("👷 Voce:", message, Colors.CornflowerBlue);
            _inputBox.Text = "";
            _inputBox.IsEnabled = false;
            _sendButton.IsEnabled = false;
            _isProcessing = true;

            _statusText.Text = "DeepSeek esta pensando...";

            try
            {
                var response = await _client.AskAsync(message);

                if (response.Success)
                {
                    AppendToChat("🤖 DeepSeek:", response.Text, Color.FromRgb(100, 210, 140));

                    if (response.HasCommand)
                    {
                        AppendToChat("⚙️ Comando:", response.Command, Color.FromRgb(255, 200, 100));
                        _execButton.Visibility = Visibility.Visible;
                        _execButton.Tag = response.Command;
                    }
                    else
                    {
                        _execButton.Visibility = Visibility.Collapsed;
                    }

                    _statusText.Text = "DeepSeek pronto.";
                }
                else
                {
                    AppendToChat("❌ Erro:", response.Text, Colors.Red);
                    _statusText.Text = "Erro na comunicacao. Verifique a conexao.";
                }
            }
            catch (Exception ex)
            {
                AppendToChat("❌ Erro:", ex.Message, Colors.Red);
                _statusText.Text = "Erro inesperado.";
            }

            _inputBox.IsEnabled = true;
            _sendButton.IsEnabled = true;
            _inputBox.Focus();
            _isProcessing = false;
        }

        private void OnExecClick(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var command = button?.Tag as string;
            if (string.IsNullOrWhiteSpace(command)) return;

            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc != null)
            {
                doc.SendStringToExecute(command + "\n", true, false, false);
                _statusText.Text = $"Comando executado: {command}";
            }
            else
            {
                _statusText.Text = "Nenhum documento ativo.";
            }
        }

        private void AppendToChat(string sender, string message, Color color)
        {
            if (_chatHistory == null) return;

            _chatHistory.Dispatcher.Invoke(() =>
            {
                var doc = _chatHistory.Document;

                if (doc.Blocks.Count == 1 &&
                    doc.Blocks.FirstBlock is Paragraph firstPara &&
                    string.IsNullOrWhiteSpace(new TextRange(firstPara.ContentStart, firstPara.ContentEnd).Text))
                {
                    doc.Blocks.Clear();
                }

                var senderPara = new Paragraph
                {
                    Margin = new Thickness(0, 4, 0, 0),
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(color),
                    FontSize = 12
                };
                senderPara.Inlines.Add(sender);
                doc.Blocks.Add(senderPara);

                var msgPara = new Paragraph
                {
                    Margin = new Thickness(8, 2, 0, 6),
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 205)),
                    FontSize = 12,
                    TextAlignment = TextAlignment.Left
                };
                msgPara.Inlines.Add(message);
                doc.Blocks.Add(msgPara);

                _chatHistory.ScrollToEnd();
            }, DispatcherPriority.Background);
        }
    }
}
