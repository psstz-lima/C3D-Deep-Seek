using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AcAp = Autodesk.AutoCAD.ApplicationServices;
using Microsoft.Win32;

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
        private Button _attachButton;
        private Button _commandsButton;
        private TextBlock _attachStatus;
        private Grid _commandsPanel;
        private bool _commandsVisible;

        // Lista de arquivos anexados na conversa atual
        private readonly List<AttachedFile> _attachedFiles = new List<AttachedFile>();

        private class AttachedFile
        {
            public string FilePath { get; set; } = "";
            public string FileName { get; set; } = "";
            public string FileType { get; set; } = ""; // "image", "document", "other"
            public long FileSize { get; set; }
        }

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
            // Container principal com 5 linhas (toolbar + chat + status + input + exec)
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // toolbar
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // chat
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // status
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // input
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // exec button
            mainGrid.Margin = new Thickness(0);
            mainGrid.Background = new SolidColorBrush(Color.FromRgb(40, 40, 45));

            // ═══ Row 0: Toolbar com botão Anexar ═══
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 58)),
                Margin = new Thickness(0)
            };

            _attachButton = new Button
            {
                Content = "📎 Anexar",
                Background = new SolidColorBrush(Color.FromRgb(70, 70, 80)),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 210)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
                Cursor = Cursors.Hand,
                ToolTip = "Anexar arquivo ou imagem (ou Ctrl+V para colar do clipboard)",
                Margin = new Thickness(4, 2, 0, 2)
            };
            _attachButton.Click += OnAttachClick;

            _commandsButton = new Button
            {
                Content = "🧭 Comandos",
                Background = new SolidColorBrush(Color.FromRgb(80, 80, 140)),
                Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 250)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
                Cursor = Cursors.Hand,
                ToolTip = "Mostrar/ocultar lista de comandos disponíveis",
                Margin = new Thickness(2, 2, 0, 2)
            };
            _commandsButton.Click += OnCommandsClick;

            _attachStatus = new TextBlock
            {
                Text = "",
                Foreground = new SolidColorBrush(Color.FromRgb(140, 200, 140)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            toolbar.Children.Add(_attachButton);
            toolbar.Children.Add(_commandsButton);
            toolbar.Children.Add(_attachStatus);
            Grid.SetRow(toolbar, 0);

            // ═══ Painel de Comandos (flutuante sobre o chat) ═══
            _commandsPanel = CreateCommandsPanel();
            _commandsPanel.Visibility = Visibility.Collapsed;
            Grid.SetRow(_commandsPanel, 1);
            Panel.SetZIndex(_commandsPanel, 100);

            // ═══ Row 1: Historico do chat ═══
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
            Grid.SetRow(_chatHistory, 1);

            // Row 2: Barra de status
            _statusText = new TextBlock
            {
                Text = "DeepSeek pronto. Digite sua pergunta abaixo.",
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 145)),
                Background = new SolidColorBrush(Color.FromRgb(50, 50, 55)),
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 11
            };
            Grid.SetRow(_statusText, 2);

            // Row 3: Input area
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            inputGrid.Background = new SolidColorBrush(Color.FromRgb(50, 50, 55));
            Grid.SetRow(inputGrid, 3);

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
            Grid.SetRow(_execButton, 4);

            mainGrid.Children.Add(toolbar);
            mainGrid.Children.Add(_chatHistory);
            mainGrid.Children.Add(_commandsPanel);
            mainGrid.Children.Add(_statusText);
            mainGrid.Children.Add(inputGrid);
            mainGrid.Children.Add(_execButton);

            Content = mainGrid;
        }

        private Grid CreateCommandsPanel()
        {
            var panel = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(245, 30, 30, 45)),
                Margin = new Thickness(4)
            };

            // Barra de título
            var titleBar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 120)),
                Margin = new Thickness(0)
            };

            var titleText = new TextBlock
            {
                Text = "🧭 COMANDOS DISPONÍVEIS (28)",
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 255)),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center
            };

            var closeBtn = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 220)),
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Width = 28, Height = 28,
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeBtn.Click += (s, e) => { _commandsPanel.Visibility = Visibility.Collapsed; _commandsVisible = false; };

            titleBar.Children.Add(titleText);
            titleBar.Children.Add(closeBtn);
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(titleBar, 0);
            panel.Children.Add(titleBar);

            // Lista de comandos
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0)
            };

            var cmdList = new TextBlock
            {
                Text = GetCommandList(),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 225)),
                Background = new SolidColorBrush(Color.FromRgb(35, 35, 48)),
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                FontSize = 11,
                Padding = new Thickness(8),
                TextWrapping = TextWrapping.NoWrap
            };

            scroll.Content = cmdList;
            Grid.SetRow(scroll, 1);
            panel.Children.Add(scroll);

            return panel;
        }

        private void OnCommandsClick(object sender, RoutedEventArgs e)
        {
            _commandsVisible = !_commandsVisible;
            _commandsPanel.Visibility = _commandsVisible ? Visibility.Visible : Visibility.Collapsed;

            if (_commandsVisible)
                _statusText.Text = "🧭 Lista de comandos aberta — clique ✕ para fechar.";
        }

        private static string GetCommandList()
        {
            return
@"╔══════════════════════════════════════════════════════════╗
║           🧭 C3D DeepSeek — 28 COMANDOS               ║
╠══════════════════════════════════════════════════════════╣
║                                                        ║
║  💬 IA / CHAT                                         ║
║   DEEPSEEK     Abre painel de chat lateral            ║
║   DSASK        Pergunta rápida na linha de comando    ║
║   CONFIGDS     Configura chave API DeepSeek           ║
║                                                        ║
║  🔍 ANÁLISE                                           ║
║   DSANALYZE    Análise inteligente do projeto         ║
║   DSCHECK      Análise crítica (qualidade/BIM)        ║
║   DSCOMPARE    Compara 2+ projetos abertos            ║
║   DSCLASH      Detecta interferências                 ║
║                                                        ║
║  📊 RELATÓRIOS / EXPORTAÇÃO                           ║
║   DSREPORT     Relatórios (9 tipos)                   ║
║   DSEXPORT     Exporta relatório Excel (.xlsx)        ║
║   DSBIM        Dashboard BIM com progresso            ║
║                                                        ║
║  🏗️ MODELAGEM / PROJETO                               ║
║   DSMODEL      Modelagem por linguagem natural        ║
║   DSWORKFLOW   Fluxos guiados (7 tipos)               ║
║   DSDESIGN     Verificação normas DNIT/AASHTO         ║
║   DSTEMPLATE   Novo projeto por template (5)          ║
║                                                        ║
║  🛤️ TERRAPLENAGEM                                     ║
║   DSEARTH      Análise corte/aterro                   ║
║   DSBRUCKNER   Diagrama de Brückner (mass haul)       ║
║   DSOPTIMIZE   Otimizador de superfície p/ drone      ║
║                                                        ║
║  📐 DESENHO / FOLHAS                                  ║
║   DSSHEETS     Folhas A0-A4 + DWT                    ║
║   DSSECTIONS   Seções transversais (5 presets)        ║
║   DSDIM        Labels e cotagem inteligente           ║
║   DSASSEMBLY   Subassemblies para corredores          ║
║                                                        ║
║  🧮 CÁLCULOS                                          ║
║   DSCALC       24 cálculos (hidráulica, pav, etc)     ║
║   DSTRANSFORM  UTM↔Topográfico, Lat/Lon→UTM          ║
║                                                        ║
║  🌊 INFRAESTRUTURA                                    ║
║   DSDRAINAGE   Drenagem inteligente + bacias          ║
║   DSIMPORT     Importa CSV/LandXML/SHP/KML/IFC        ║
║                                                        ║
║  📍 PONTOS / CONEXÃO                                  ║
║   DSGOOGLEMAPS Google Maps/Earth → COGO points        ║
║   DSCONNECT    Auto-conexão inteligente de pontos     ║
║                                                        ║
║  💻 DESENVOLVIMENTO                                   ║
║   DSCODE       Gera código LISP/.NET                  ║
║                                                        ║
╚══════════════════════════════════════════════════════════╝";
        }

        private void OnAttachClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Anexar arquivo ao chat",
                Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Documentos|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.dwg;*.dxf|Todos|*.*",
                Multiselect = true
            };

            if (dlg.ShowDialog() == true)
            {
                foreach (var filePath in dlg.FileNames)
                {
                    AttachFile(filePath);
                }
            }
        }

        private void AttachFile(string filePath)
        {
            try
            {
                var fi = new FileInfo(filePath);
                var ext = fi.Extension.ToLower();
                string fileType = "other";
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".gif")
                    fileType = "image";
                else if (ext == ".pdf" || ext == ".doc" || ext == ".docx" || ext == ".dwg" || ext == ".dxf")
                    fileType = "document";

                var attached = new AttachedFile
                {
                    FilePath = filePath,
                    FileName = fi.Name,
                    FileType = fileType,
                    FileSize = fi.Length
                };
                _attachedFiles.Add(attached);

                string icon = fileType == "image" ? "🖼️" : fileType == "document" ? "📄" : "📎";
                AppendToChat($"📎 Anexo:", $"{icon} {fi.Name} ({fi.Length / 1024} KB)", Color.FromRgb(180, 180, 200));
                UpdateAttachStatus();
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Erro ao anexar: {ex.Message}";
            }
        }

        private void HandlePaste()
        {
            try
            {
                // Tenta colar imagem do clipboard
                if (Clipboard.ContainsImage())
                {
                    var image = Clipboard.GetImage();
                    if (image != null)
                    {
                        // Salva a imagem em temp
                        string tempDir = Path.GetTempPath();
                        string fileName = $"C3DDeepSeek_Clipboard_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                        string filePath = Path.Combine(tempDir, fileName);

                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(image));
                        using (var fs = new FileStream(filePath, FileMode.Create))
                            encoder.Save(fs);

                        AttachFile(filePath);
                        return;
                    }
                }

                // Tenta colar arquivos do clipboard
                if (Clipboard.ContainsFileDropList())
                {
                    var files = Clipboard.GetFileDropList();
                    foreach (var filePath in files)
                    {
                        if (File.Exists(filePath))
                            AttachFile(filePath);
                    }
                    return;
                }
            }
            catch
            {
                // Fallback: não faz nada se falhar
            }
        }

        private void UpdateAttachStatus()
        {
            if (_attachedFiles.Count == 0)
                _attachStatus.Text = "";
            else
                _attachStatus.Text = $"📎 {_attachedFiles.Count} anexo(s)";

            _attachStatus.Foreground = new SolidColorBrush(
                _attachedFiles.Count > 0
                    ? Color.FromRgb(140, 220, 140)
                    : Color.FromRgb(140, 140, 145));
        }

        private void ClearAttachments()
        {
            _attachedFiles.Clear();
            UpdateAttachStatus();
        }

        private string GetAttachmentDescription()
        {
            if (_attachedFiles.Count == 0) return "";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[ARQUIVOS ANEXADOS PELO USUÁRIO]");
            foreach (var file in _attachedFiles)
            {
                sb.AppendLine($"• {file.FileName} ({file.FileType}, {file.FileSize / 1024} KB)");
                if (file.FileType == "image")
                    sb.AppendLine($"  (Imagem — analise o que pode ser este elemento para criar no Civil 3D)");
            }
            return sb.ToString();
        }

        private async void OnSendClick(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl+V = colar imagem/arquivo do clipboard
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                HandlePaste();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter && !_isProcessing)
            {
                e.Handled = true;
                _ = SendMessageAsync();
            }
        }

        private async Task SendMessageAsync()
        {
            if (_isProcessing) return;

            var message = _inputBox?.Text?.Trim();

            // Se tem anexos mas sem texto, usa mensagem padrão
            if (_attachedFiles.Count > 0 && string.IsNullOrWhiteSpace(message))
            {
                message = "Analise o(s) arquivo(s) anexado(s) e me ajude com o Civil 3D.";
            }

            if (string.IsNullOrWhiteSpace(message) && _attachedFiles.Count == 0) return;

            AppendToChat("👷 Voce:", message ?? "(anexos)", Colors.CornflowerBlue);
            _inputBox.Text = "";
            _inputBox.IsEnabled = false;
            _sendButton.IsEnabled = false;
            _isProcessing = true;

            _statusText.Text = "DeepSeek esta pensando...";

            try
            {
                // Inclui descrição dos anexos na pergunta
                string attachDesc = GetAttachmentDescription();
                string fullMessage = message ?? "";

                // Envia a pergunta com informações sobre anexos
                // O DeepSeekClient já adiciona contexto do desenho
                var response = await _client.AskAsync(attachDesc + "\n" + fullMessage);

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

            // Limpa anexos após enviar
            ClearAttachments();

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

            try
            {
                // Usa o motor premium para execução inteligente
                var response = new DeepSeekResponse { Success = true, Command = command };
                var result = DeepSeekEngine.Execute(response);
                DeepSeekEngine.DisplayResult(result);
                _statusText.Text = $"Comando executado: {command}";
            }
            catch (Exception ex)
            {
                _statusText.Text = $"Erro: {ex.Message}";
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
