using System;
using System.Drawing;
using AcAp = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows;

// Linha de assembly: entry point para AutoCAD
[assembly: ExtensionApplication(typeof(C3DDeepSeek.PluginLoader))]
[assembly: CommandClass(typeof(C3DDeepSeek.PluginCommands))]

namespace C3DDeepSeek
{
    /// <summary>
    /// Entry point do plugin. Carregado quando o AutoCAD/Civil 3D inicia.
    /// </summary>
    public class PluginLoader : IExtensionApplication
    {
        public void Initialize()
        {
            try
            {
                var apiKey = DeepSeekConfig.LoadApiKey();

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    AcAp.Application.DocumentManager.MdiActiveDocument?.Editor
                        .WriteMessage("\n[C3D DeepSeek] ⚠ Chave API não configurada. Use COMANDO: CONFIGDS");
                }
                else
                {
                    AcAp.Application.DocumentManager.MdiActiveDocument?.Editor
                        .WriteMessage("\n[C3D DeepSeek] ✅ Plugin carregado. Use COMANDO: DEEPSEEK para abrir o painel.");
                }
            }
            catch (System.Exception ex)
            {
                AcAp.Application.DocumentManager.MdiActiveDocument?.Editor
                    .WriteMessage($"\n[C3D DeepSeek] Erro ao iniciar: {ex.Message}");
            }
        }

        public void Terminate()
        {
            // Cleanup se necessário
        }
    }

    /// <summary>
    /// Comandos expostos na linha de comando do Civil 3D/AutoCAD
    /// </summary>
    public class PluginCommands
    {
        private static PaletteSet _paletteSet;

        /// <summary>
        /// COMANDO: DEEPSEEK
        /// Abre o painel de chat com DeepSeek
        /// </summary>
        [CommandMethod("DEEPSEEK", CommandFlags.Modal)]
        public void ShowDeepSeekPanel()
        {
            try
            {
                var apiKey = DeepSeekConfig.LoadApiKey();

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    AcAp.Application.ShowAlertDialog(
                        "DeepSeek API Key não configurada.\n\n" +
                        "Crie um arquivo .env na pasta da DLL com:\n" +
                        "DEEPSEEK_API_KEY=sk-sua-chave-aqui\n\n" +
                        "Ou use o comando CONFIGDS para configurar.");
                    return;
                }

                if (_paletteSet == null)
                {
                    _paletteSet = new PaletteSet("DeepSeek AI", new Guid("D3E5E3E5-7A8B-4C9D-9E1F-2A3B4C5D6E7F"))
                    {
                        Style = PaletteSetStyles.ShowCloseButton |
                                PaletteSetStyles.ShowPropertiesMenu,
                        MinimumSize = new Size(350, 400),
                        TitleBarLocation = PaletteSetTitleBarLocation.Left,
                        Opacity = 95
                    };

                    var panel = new DeepSeekChatPanel(apiKey);
                    _paletteSet.AddVisual("Chat", panel);
                }

                _paletteSet.Visible = true;
                _paletteSet.Size = new Size(400, 500);

                // Dock à direita
                _paletteSet.Dock = DockSides.Right;
            }
            catch (System.Exception ex)
            {
                AcAp.Application.ShowAlertDialog($"Erro ao abrir painel DeepSeek:\n{ex.Message}");
            }
        }

        /// <summary>
        /// COMANDO: CONFIGDS
        /// Configura a chave da API DeepSeek
        /// </summary>
        [CommandMethod("CONFIGDS", CommandFlags.Modal)]
        public void ConfigureApiKey()
        {
            try
            {
                var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return;

                var ed = doc.Editor;

                var result = ed.GetString("\nDigite sua chave API DeepSeek: ");
                if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    return;

                var apiKey = result.StringResult.Trim();

                // Salvar no .env
                var dllPath = typeof(DeepSeekConfig).Assembly.Location;
                var dllDir = System.IO.Path.GetDirectoryName(dllPath);
                var envPath = System.IO.Path.Combine(dllDir, ".env");
                System.IO.File.WriteAllText(envPath, $"DEEPSEEK_API_KEY={apiKey}");

                ed.WriteMessage("\n[C3D DeepSeek] ✅ Chave API salva com sucesso!");
            }
            catch (System.Exception ex)
            {
                AcAp.Application.ShowAlertDialog($"Erro ao configurar chave:\n{ex.Message}");
            }
        }

        /// <summary>
        /// COMANDO: DSASK
        /// Envia uma pergunta direta ao DeepSeek via linha de comando (sem abrir painel)
        /// Uso: DSASK Sua pergunta aqui
        /// </summary>
        [CommandMethod("DSASK", CommandFlags.Modal)]
        public async void AskDeepSeekCommandLine()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;

            var apiKey = DeepSeekConfig.LoadApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ed.WriteMessage("\n[C3D DeepSeek] ⚠ Chave API não configurada. Use CONFIGDS.");
                return;
            }

            var result = ed.GetString("\nPergunta para o DeepSeek: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return;

            var question = result.StringResult.Trim();
            if (string.IsNullOrWhiteSpace(question)) return;

            ed.WriteMessage("\n[C3D DeepSeek] Aguardando resposta...");

            var client = new DeepSeekClient(apiKey);
            var response = await client.AskAsync(question);

            if (response.Success)
            {
                ed.WriteMessage($"\n🤖 DeepSeek: {response.Text}");

                if (response.HasCommand)
                {
                    ed.WriteMessage($"\n⚙️ Comando sugerido: {response.Command}");
                    var execResult = ed.GetString("\nExecutar este comando? (S/N): ");
                    if (execResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                        execResult.StringResult.Trim().ToUpper() == "S")
                    {
                        doc.SendStringToExecute(response.Command + "\n", true, false, false);
                    }
                }
            }
            else
            {
                ed.WriteMessage($"\n❌ Erro: {response.Text}");
            }
        }
    }
}
