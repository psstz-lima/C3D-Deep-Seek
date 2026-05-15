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
        /// Executa um comando no Civil 3D usando COM SendCommand (confiável, igual ao PowerShell)
        /// </summary>
        public static void ExecuteInAutoCAD(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            try
            {
                // Método COM: mesmo que o PowerShell usa, comprovadamente funciona
                dynamic acadApp = AcAp.Application.AcadApplication;
                acadApp.ActiveDocument.SendCommand(command + "\n");
            }
            catch
            {
                // Fallback: SendStringToExecute (menos confiável mas não depende de COM)
                var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
                doc?.SendStringToExecute(command + "\n", true, false, true);
            }
        }

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
        public void AskDeepSeekCommandLine()
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
            // Síncrono: evita async void que quebra o contexto do comando AutoCAD
            var response = System.Threading.Tasks.Task.Run(() => client.AskAsync(question)).Result;

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
                        // Usa o motor premium para execução inteligente
                        var execResult2 = DeepSeekEngine.Execute(response);
                        DeepSeekEngine.DisplayResult(execResult2);
                    }
                }
            }
            else
            {
                ed.WriteMessage($"\n❌ Erro: {response.Text}");
            }
        }

        /// <summary>
        /// COMANDO: DSREPORT
        /// Gera relatórios do projeto: superfícies, alinhamentos, quantitativos, BIM
        /// Uso: DSREPORT [FULL|SURFACES|ALIGNMENTS|CORRIDORS|PIPES|VOLUMES|LAYERS|QUANTITIES|BIM]
        /// </summary>
        [CommandMethod("DSREPORT", CommandFlags.Modal)]
        public void GenerateReport()
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

            var result = ed.GetString("\nTipo de relatório [FULL/SURFACES/ALIGNMENTS/CORRIDORS/PIPES/VOLUMES/LAYERS/QUANTITIES/BIM]: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return;

            var reportType = string.IsNullOrWhiteSpace(result.StringResult) ? "FULL" : result.StringResult.Trim().ToUpper();

            ed.WriteMessage($"\n📊 Gerando relatório '{reportType}'...");

            var response = new DeepSeekResponse
            {
                Success = true,
                Command = $"RELATORIO:{reportType}"
            };

            var execResult = DeepSeekEngine.Execute(response);
            DeepSeekEngine.DisplayResult(execResult);
        }

        /// <summary>
        /// COMANDO: DSANALYZE
        /// Analisa o desenho atual usando IA — envia contexto completo para o DeepSeek
        /// e retorna uma análise inteligente do projeto.
        /// </summary>
        [CommandMethod("DSANALYZE", CommandFlags.Modal)]
        public void AnalyzeProject()
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

            ed.WriteMessage("\n🔍 Analisando projeto com DeepSeek AI...");

            var client = new DeepSeekClient(apiKey);
            var response = System.Threading.Tasks.Task.Run(() =>
                client.AskAsync("Faça uma análise completa deste projeto Civil 3D: " +
                    "identifique o tipo de obra (rodovia, loteamento, drenagem, terraplenagem), " +
                    "liste os elementos principais, aponte possíveis inconsistências ou melhorias, " +
                    "e sugira próximos passos no fluxo de trabalho BIM. " +
                    "Seja técnico e objetivo.")).Result;

            if (response.Success)
            {
                ed.WriteMessage($"\n\n🤖 ANÁLISE DO PROJETO:\n{response.Text}");

                if (response.HasCommand)
                {
                    ed.WriteMessage($"\n⚙️ Ação sugerida: {response.Command}");
                }
            }
            else
            {
                ed.WriteMessage($"\n❌ Erro na análise: {response.Text}");
            }
        }

        /// <summary>
        /// COMANDO: DSMODEL
        /// Operações de modelagem inteligente via linguagem natural.
        /// Ex: DSMODEL → "crie uma superfície TIN com os pontos da layer PONTOS"
        /// </summary>
        [CommandMethod("DSMODEL", CommandFlags.Modal)]
        public void SmartModeling()
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

            ed.WriteMessage("\n🏗️ DSMODEL — Modelagem Inteligente via IA");
            ed.WriteMessage("\nDescreva o que você quer criar/modificar no projeto:");

            var result = ed.GetString("\n> ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                return;

            var description = result.StringResult.Trim();
            if (string.IsNullOrWhiteSpace(description)) return;

            ed.WriteMessage("\n🧠 Processando instrução de modelagem...");

            var client = new DeepSeekClient(apiKey);
            var response = System.Threading.Tasks.Task.Run(() =>
                client.AskAsync($"Execute a seguinte operação de modelagem no Civil 3D: {description}. " +
                    "Use comandos diretos, sequências ou API conforme necessário. " +
                    "Seja preciso nos comandos gerados.")).Result;

            if (response.Success)
            {
                ed.WriteMessage($"\n🤖 DeepSeek: {response.Text}");

                if (response.HasCommand)
                {
                    ed.WriteMessage($"\n⚙️ Comando: {response.Command}");
                    var execResult2 = DeepSeekEngine.Execute(response);
                    DeepSeekEngine.DisplayResult(execResult2);
                }
            }
            else
            {
                ed.WriteMessage($"\n❌ Erro: {response.Text}");
            }
        }
        /// <summary>
        /// COMANDO: DSCHECK
        /// Analisa criticamente o projeto — detecta erros, inconsistências e sugere melhorias
        /// </summary>
        [CommandMethod("DSCHECK", CommandFlags.Modal)]
        public void CheckProject()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n🔍 DSCHECK — Analisando projeto...");
            var analysis = ProjectAnalyzer.AnalyzeCurrentProject();
            ed.WriteMessage($"\n{analysis.Report}");
        }

        /// <summary>
        /// COMANDO: DSCOMPARE
        /// Compara 2 ou mais projetos abertos — compatibilização, diferenças
        /// </summary>
        [CommandMethod("DSCOMPARE", CommandFlags.Modal)]
        public void CompareProjects()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n⚖️ DSCOMPARE — Comparando projetos...");
            var analysis = ProjectAnalyzer.CompareProjects();
            ed.WriteMessage($"\n{analysis.Report}");
        }

        /// <summary>
        /// COMANDO: DSWORKFLOW
        /// Executa fluxos de trabalho guiados (rodovia, loteamento, terraplenagem, etc.)
        /// </summary>
        [CommandMethod("DSWORKFLOW", CommandFlags.Modal)]
        public void RunWorkflow()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            var workflows = WorkflowEngine.GetAvailableWorkflows();
            ed.WriteMessage("\n🚀 DSWORKFLOW — Workflows disponíveis:");
            foreach (var w in workflows)
                ed.WriteMessage($"\n   ▸ {w}");

            var result = ed.GetString("\nWorkflow: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            var wfName = result.StringResult.Trim().ToUpper();
            var output = WorkflowEngine.ExecuteWorkflow(wfName);
            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSCALC
        /// Calculadora de engenharia: hidráulica, pavimento, terraplenagem, drenagem
        /// </summary>
        [CommandMethod("DSCALC", CommandFlags.Modal)]
        public void EngineeringCalc()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n📐 DSCALC — Calculadora de Engenharia");
            ed.WriteMessage("\nTipos: MANNING, VAZAO, DIAMETRO_TUBO, GALERIA, BOCA_LOBO, SARJETA, CANAL");
            ed.WriteMessage("\n       PAV_DNER, CAIG, ESPESSURAS, VOLUME_CORTE, EMPOLAMENTO");
            ed.WriteMessage("\n       TEMPO_CONC, INTENSIDADE, BACIA, DIST_FRENAGEM, CAPACIDADE");
            ed.WriteMessage("\nFormato: TIPO param1=valor1 param2=valor2");

            var result = ed.GetString("\nCálculo: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            var calcCmd = "CALC:" + result.StringResult.Trim();
            var calcResult = C3DCalculations.Execute(calcCmd);
            ed.WriteMessage($"\n{calcResult.Result}");
        }

        /// <summary>
        /// COMANDO: DSIMPORT
        /// Importa arquivos: CSV, LandXML, SHP, KML, IFC
        /// </summary>
        [CommandMethod("DSIMPORT", CommandFlags.Modal)]
        public void ImportFile()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n📥 DSIMPORT — Formatos: CSV, XML (LandXML), SHP, KML/KMZ, IFC, DWG, DXF");

            var result = ed.GetString("\nCaminho do arquivo: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            var filePath = result.StringResult.Trim();
            var output = DataImporter.SmartImport(filePath);
            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSEXPORT
        /// Exporta relatório completo para Excel (.xlsx)
        /// </summary>
        [CommandMethod("DSEXPORT", CommandFlags.Modal)]
        public void ExportToExcel()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n📊 DSEXPORT — Gerando relatório Excel...");
            var output = ExcelExporter.ExportFullReport();
            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSOPTIMIZE
        /// Otimiza superfícies — remove outliers, copas de árvores, ruído de drone
        /// Ideal para DTM gerado por WebODM, Pix4D, DJI Terra
        /// </summary>
        [CommandMethod("DSOPTIMIZE", CommandFlags.Modal)]
        public void OptimizeSurface()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n🔧 DSOPTIMIZE — Otimizador de Superfície para Drone");
            ed.WriteMessage("\nRemove outliers, copas de árvores e ruídos do DTM.");
            ed.WriteMessage("\n");

            // Lista superfícies disponíveis
            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                dynamic surfaces = civilDoc.Surfaces;

                if (surfaces.Count == 0)
                {
                    ed.WriteMessage("\n⚠️ Nenhuma superfície encontrada no projeto.");
                    return;
                }

                ed.WriteMessage("\n📋 Superfícies disponíveis:");
                foreach (dynamic s in surfaces)
                    ed.WriteMessage($"\n   ▸ {s.Name} (Tipo: {s.Type})");
            }
            catch
            {
                ed.WriteMessage("\n⚠️ Não foi possível listar superfícies.");
                return;
            }

            var nameResult = ed.GetString("\nNome da superfície (ou ALL para todas): ");
            if (nameResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            var surfaceName = nameResult.StringResult.Trim();
            if (string.IsNullOrWhiteSpace(surfaceName)) return;

            // Pergunta se quer preview (análise) ou otimização completa
            var modeResult = ed.GetString("\nModo [A]nálise (sem modificar) ou [O]timizar: ");
            bool optimizeMode = modeResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                                modeResult.StringResult.Trim().ToUpper() == "O";

            if (surfaceName.ToUpper() == "ALL")
            {
                ed.WriteMessage("\n🔧 Otimizando TODAS as superfícies...");
                var result = SurfaceOptimizer.OptimizeAll();
                ed.WriteMessage($"\n{result.Report}");
            }
            else if (optimizeMode)
            {
                // Parâmetros avançados
                var stdDevResult = ed.GetString("\nDesvio padrão para outliers [2.5]: ");
                double stdDev = 2.5;
                if (stdDevResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                    double.TryParse(stdDevResult.StringResult.Trim().Replace(".", ","), out double sd))
                    stdDev = sd;

                var vegResult = ed.GetString("\nThreshold vegetação (m) [1.5]: ");
                double vegThresh = 1.5;
                if (vegResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                    double.TryParse(vegResult.StringResult.Trim().Replace(".", ","), out double vt))
                    vegThresh = vt;

                ed.WriteMessage($"\n🔧 Otimizando '{surfaceName}' (σ={stdDev}, veg={vegThresh}m)...");
                var result = SurfaceOptimizer.Optimize(surfaceName, stdDev, vegThresh);
                ed.WriteMessage($"\n{result.Report}");
            }
            else
            {
                // Modo análise
                ed.WriteMessage($"\n🔍 Analisando '{surfaceName}'...");
                var result = SurfaceOptimizer.Analyze(surfaceName);
                ed.WriteMessage($"\n{result.Report}");
            }
        }

        /// <summary>
        /// COMANDO: DSCODE
        /// Gera código LISP ou .NET para tarefas personalizadas
        /// </summary>
        [CommandMethod("DSCODE", CommandFlags.Modal)]
        public void GenerateCode()
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

            ed.WriteMessage("\n💻 DSCODE — Gerador de Código");
            ed.WriteMessage("\nDescreva o que o código deve fazer:");

            var result = ed.GetString("\n> ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            var description = result.StringResult.Trim();
            if (string.IsNullOrWhiteSpace(description)) return;

            ed.WriteMessage("\n🧠 Gerando código...");

            var client = new DeepSeekClient(apiKey);
            var response = System.Threading.Tasks.Task.Run(() =>
                client.AskAsync($"Gere código AutoCAD (AutoLISP ou C# .NET) para: {description}. " +
                    "Forneça o código pronto para uso, com comentários explicativos. " +
                    "Formate como código dentro de um bloco de comando.")).Result;

            if (response.Success)
            {
                ed.WriteMessage($"\n🤖 Código gerado:\n{response.Text}");
            }
            else
            {
                ed.WriteMessage($"\n❌ Erro: {response.Text}");
            }
        }
    }
}
