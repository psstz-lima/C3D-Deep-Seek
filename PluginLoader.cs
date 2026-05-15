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
        /// COMANDO: DSASSEMBLY
        /// Catálogo de subassemblies e criação de montagens para corredores
        /// </summary>
        [CommandMethod("DSASSEMBLY", CommandFlags.Modal)]
        public void SubassemblyTools()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n🧩 DSASSEMBLY — Subassemblies e Montagens para Corredores");
            ed.WriteMessage("\n───────────────────────────────────────────");
            ed.WriteMessage("\n[1] Listar todos os subassemblies disponíveis");
            ed.WriteMessage("\n[2] Criar montagem rodoviária padrão (DNIT)");
            ed.WriteMessage("\n[3] Criar montagem urbana (avenida)");
            ed.WriteMessage("\n[4] Iniciar ferramenta nativa de montagem");

            var result = ed.GetString("\nOpção [1-4]: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            switch (result.StringResult.Trim())
            {
                case "1":
                    ed.WriteMessage($"\n{SubassemblyBuilder.ListAllTemplates()}");
                    break;
                case "2":
                    var nameR = ed.GetString("\nNome da montagem rodoviária: ");
                    if (nameR.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    {
                        var name = string.IsNullOrWhiteSpace(nameR.StringResult) ? "Montagem_Rodovia" : nameR.StringResult.Trim();
                        ed.WriteMessage($"\n{SubassemblyBuilder.BuildHighwayAssembly(name)}");
                        DeepSeekEngine.SendToAutoCAD($"_AeccCreateAssembly \"{name}\"");
                    }
                    break;
                case "3":
                    var nameU = ed.GetString("\nNome da montagem urbana: ");
                    if (nameU.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    {
                        var name = string.IsNullOrWhiteSpace(nameU.StringResult) ? "Montagem_Urbana" : nameU.StringResult.Trim();
                        ed.WriteMessage($"\n{SubassemblyBuilder.BuildUrbanAssembly(name)}");
                        DeepSeekEngine.SendToAutoCAD($"_AeccCreateAssembly \"{name}\"");
                    }
                    break;
                case "4":
                    DeepSeekEngine.SendToAutoCAD("_AeccCreateAssembly");
                    ed.WriteMessage("\n✅ Ferramenta de montagem nativa aberta.");
                    break;
                default:
                    ed.WriteMessage("\n⚠️ Opção inválida.");
                    break;
            }
        }

        /// <summary>
        /// COMANDO: DSSHEETS
        /// Gera folhas de Planta/Perfil — A0, A1, A2, A3, A4, Personalizado + DWT
        /// </summary>
        [CommandMethod("DSSHEETS", CommandFlags.Modal)]
        public void GenerateSheets()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n📐 DSSHEETS — Gerador de Folhas Técnicas");
            ed.WriteMessage($"\n{SheetGenerator.ListFormats()}");

            var config = new SheetGenerator.SheetConfig();

            var fmtResult = ed.GetString("\nFormato [A0/A1/A2/A3/A4/Personalizado]: ");
            if (fmtResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;
            config.Format = string.IsNullOrWhiteSpace(fmtResult.StringResult) ? "A1" : fmtResult.StringResult.Trim();

            var scaleResult = ed.GetString("\nEscala (ex: 1000 para 1:1000): ");
            if (scaleResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                double.TryParse(scaleResult.StringResult.Replace(".", ","), out double sc))
                config.Scale = sc;

            var alignResult = ed.GetString("\nNome do alinhamento (ou ENTER para principal): ");
            config.AlignmentName = alignResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK
                ? alignResult.StringResult.Trim() : "";

            var templateResult = ed.GetString("\nCaminho do DWT (ou ENTER para padrão): ");
            if (templateResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                config.TemplatePath = templateResult.StringResult.Trim();

            var profileResult = ed.GetString("\nIncluir perfil? [S/N]: ");
            config.IncludeProfile = profileResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK ||
                                    profileResult.StringResult.Trim().ToUpper() != "N";

            var output = SheetGenerator.GeneratePlanProfileSheets(config);
            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSCLASH
        /// Detecta interferências entre redes, corredores, estruturas
        /// </summary>
        [CommandMethod("DSCLASH", CommandFlags.Modal)]
        public void ClashDetection()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n🔍 DSCLASH — Detector de Interferências...");
            var result = ClashDetector.RunFullClashAnalysis();
            ed.WriteMessage($"\n{result}");
        }

        /// <summary>
        /// COMANDO: DSDIM
        /// Rotulagem inteligente: estacas, curvas, tubulações, tabelas
        /// </summary>
        [CommandMethod("DSDIM", CommandFlags.Modal)]
        public void SmartDimension()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n🏷️ DSDIM — Rotulador Inteligente");
            ed.WriteMessage("\n[1] Rotular estacas de alinhamento");
            ed.WriteMessage("\n[2] Rotular curvas de nível");
            ed.WriteMessage("\n[3] Tabela de curvas horizontais");
            ed.WriteMessage("\n[4] Tabela de PIVs");
            ed.WriteMessage("\n[5] Rotular tubulações");
            ed.WriteMessage("\n[6] Rotular estruturas");
            ed.WriteMessage("\n[7] Rotular TUDO automático");

            var result = ed.GetString("\nOpção [1-7]: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            string output = "";
            switch (result.StringResult.Trim())
            {
                case "1":
                    var a1 = ed.GetString("\nNome do alinhamento: ");
                    if (a1.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                        output = SmartLabeler.LabelAlignmentStations(a1.StringResult.Trim());
                    break;
                case "2":
                    var s2 = ed.GetString("\nNome da superfície: ");
                    if (s2.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                        output = SmartLabeler.LabelContours(s2.StringResult.Trim());
                    break;
                case "3":
                    var a3 = ed.GetString("\nNome do alinhamento: ");
                    if (a3.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                        output = SmartLabeler.GenerateCurveTable(a3.StringResult.Trim());
                    break;
                case "4":
                    var a4 = ed.GetString("\nNome do alinhamento: ");
                    if (a4.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                        output = SmartLabeler.GeneratePIVTable(a4.StringResult.Trim());
                    break;
                case "5":
                    var n5 = ed.GetString("\nNome da rede: ");
                    if (n5.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                        output = SmartLabeler.LabelPipes(n5.StringResult.Trim());
                    break;
                case "6":
                    var n6 = ed.GetString("\nNome da rede: ");
                    if (n6.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                        output = SmartLabeler.LabelStructures(n6.StringResult.Trim());
                    break;
                case "7":
                    var a7 = ed.GetString("\nAlinhamento: ");
                    var s7 = ed.GetString("\nSuperfície: ");
                    var p7 = ed.GetString("\nRede de tubulação: ");
                    output = SmartLabeler.LabelAll(
                        a7.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK ? a7.StringResult.Trim() : "",
                        s7.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK ? s7.StringResult.Trim() : "",
                        p7.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK ? p7.StringResult.Trim() : "");
                    break;
                default: output = "⚠️ Opção inválida."; break;
            }
            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSDESIGN
        /// Verifica conformidade com normas DNIT/AASHTO
        /// </summary>
        [CommandMethod("DSDESIGN", CommandFlags.Modal)]
        public void DesignCheck()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n📏 DSDESIGN — Verificação de Normas");
            ed.WriteMessage("\n[1] Verificar por velocidade diretriz");
            ed.WriteMessage("\n[2] Classificar via por VDM");

            var result = ed.GetString("\nOpção [1-2]: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            string output = "";
            if (result.StringResult.Trim() == "1")
            {
                var spd = ed.GetString("\nVelocidade diretriz (km/h) [40/60/80/100/120]: ");
                if (spd.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                    double.TryParse(spd.StringResult.Replace(".", ","), out double v))
                    output = DesignChecker.CheckByDesignSpeed(v);
            }
            else if (result.StringResult.Trim() == "2")
            {
                var vdm = ed.GetString("\nVDM (veículos/dia): ");
                if (vdm.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                    double.TryParse(vdm.StringResult.Replace(".", ","), out double v))
                    output = DesignChecker.GetRoadClassification(v);
            }
            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSBIM
        /// Dashboard BIM com indicadores, progresso e checklist
        /// </summary>
        [CommandMethod("DSBIM", CommandFlags.Modal)]
        public void ShowBimDashboard()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n📊 DSBIM — Gerando Dashboard...");
            var result = BimDashboard.Generate();
            ed.WriteMessage($"\n{result}");
        }

        /// <summary>
        /// COMANDO: DSTRANSFORM
        /// Conversão de coordenadas: UTM ↔ Topográfico, Geográfico → UTM
        /// </summary>
        [CommandMethod("DSTRANSFORM", CommandFlags.Modal)]
        public void CoordinateTransform()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n🗺️ DSTRANSFORM — Transformação de Coordenadas");
            ed.WriteMessage("\n[1] UTM → Topográfico Local");
            ed.WriteMessage("\n[2] Topográfico → UTM");
            ed.WriteMessage("\n[3] Geográfico (Lat/Lon) → UTM");
            ed.WriteMessage("\n[4] Definir sistema de coordenadas EPSG");
            ed.WriteMessage("\n[5] Listar sistemas do Brasil");

            var result = ed.GetString("\nOpção [1-5]: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            string output = "";
            switch (result.StringResult.Trim())
            {
                case "1":
                    var e1 = ed.GetString("\nEasting UTM: "); double.TryParse(e1.StringResult.Replace(".", ","), out double east);
                    var n1 = ed.GetString("\nNorthing UTM: "); double.TryParse(n1.StringResult.Replace(".", ","), out double north);
                    var az = ed.GetString("\nAzimute do eixo (°): "); double.TryParse(az.StringResult.Replace(".", ","), out double azimuth);
                    var xo = ed.GetString("\nX origem (opcional): "); double.TryParse(xo.StringResult.Replace(".", ","), out double xOrig);
                    var yo = ed.GetString("\nY origem (opcional): "); double.TryParse(yo.StringResult.Replace(".", ","), out double yOrig);
                    output = CoordinateTransformer.UtmToTopographic(east, north, azimuth, xOrig, yOrig);
                    break;
                case "2":
                    var x2 = ed.GetString("\nX Topográfico: "); double.TryParse(x2.StringResult.Replace(".", ","), out double xTopo);
                    var y2 = ed.GetString("\nY Topográfico: "); double.TryParse(y2.StringResult.Replace(".", ","), out double yTopo);
                    output = CoordinateTransformer.TopographicToUtm(xTopo, yTopo);
                    break;
                case "3":
                    var lat = ed.GetString("\nLatitude (°): "); double.TryParse(lat.StringResult.Replace(".", ","), out double la);
                    var lon = ed.GetString("\nLongitude (°): "); double.TryParse(lon.StringResult.Replace(".", ","), out double lo);
                    output = CoordinateTransformer.GeoToUtm(la, lo);
                    break;
                case "4":
                    var epsg = ed.GetString("\nCódigo EPSG: ");
                    if (epsg.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                        output = CoordinateTransformer.SetCoordinateSystem(epsg.StringResult.Trim());
                    break;
                case "5":
                    output = CoordinateTransformer.ListBrazilSystems();
                    break;
            }
            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSTEMPLATE
        /// Cria projeto novo com layers e configurações padrão por tipo
        /// </summary>
        [CommandMethod("DSTEMPLATE", CommandFlags.Modal)]
        public void CreateFromTemplate()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n🏗️ DSTEMPLATE — Novo Projeto por Template");
            ed.WriteMessage($"\n{TemplateManager.ListAvailableTemplates()}");

            var result = ed.GetString("\nTipo [RODOVIA/LOTEAMENTO/SANEAMENTO/DRENAGEM/TERRAPLENAGEM]: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            ed.WriteMessage("\n🏗️ Criando projeto...");
            var output = TemplateManager.CreateFromTemplate(result.StringResult.Trim());
            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSEARTH
        /// Análise de corte/aterro — identifica zonas, calcula volumes entre superfícies
        /// </summary>
        [CommandMethod("DSEARTH", CommandFlags.Modal)]
        public void EarthworkAnalysis()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n⛏️ DSEARTH — Análise de Corte/Aterro");

            var natResult = ed.GetString("\nNome da superfície NATURAL: ");
            if (natResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            var projResult = ed.GetString("\nNome da superfície de PROJETO: ");
            if (projResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            ed.WriteMessage("\n🔍 Analisando corte/aterro...");
            var output = EarthworkAnalyzer.IdentifyCutFillZones(
                natResult.StringResult.Trim(), projResult.StringResult.Trim());
            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSBRUCKNER
        /// Diagrama de Brückner (mass haul) — exibe no model e exporta Excel
        /// </summary>
        [CommandMethod("DSBRUCKNER", CommandFlags.Modal)]
        public void BrucknerDiagram()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n📊 DSBRUCKNER — Diagrama de Brückner (Mass Haul)");
            ed.WriteMessage("\n═══════════════════════════════════════");

            // Lista alinhamentos e superfícies
            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                ed.WriteMessage("\n📋 Alinhamentos disponíveis:");
                foreach (dynamic al in civilDoc.Alignments)
                    ed.WriteMessage($"\n   ▸ {al.Name}");

                ed.WriteMessage("\n📋 Superfícies disponíveis:");
                foreach (dynamic s in civilDoc.Surfaces)
                    ed.WriteMessage($"\n   ▸ {s.Name}");
            }
            catch { }

            var alignResult = ed.GetString("\nNome do ALINHAMENTO: ");
            if (alignResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            var natResult = ed.GetString("\nSuperfície NATURAL: ");
            if (natResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            var projResult = ed.GetString("\nSuperfície PROJETO: ");
            if (projResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            var intervalResult = ed.GetString("\nIntervalo entre estações (m) [20]: ");
            double interval = 20;
            if (intervalResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                double.TryParse(intervalResult.StringResult.Replace(".", ","), out interval);

            ed.WriteMessage("\n⏳ Calculando volumes...");

            var data = EarthworkAnalyzer.Analyze(
                natResult.StringResult.Trim(),
                projResult.StringResult.Trim(),
                alignResult.StringResult.Trim(),
                interval);

            // Relatório
            ed.WriteMessage($"\n{EarthworkAnalyzer.GenerateReport(data)}");

            // Desenhar diagrama?
            var drawResult = ed.GetString("\nDesenhar diagrama no Model Space? [S/N]: ");
            if (drawResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                drawResult.StringResult.Trim().ToUpper() == "S")
            {
                var oxResult = ed.GetString("\nOrigem X (ou ENTER para 0): ");
                double ox = 0, oy = 0;
                if (oxResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    double.TryParse(oxResult.StringResult.Replace(".", ","), out ox);

                ed.WriteMessage("\n🎨 Desenhando Brückner...");
                var drawOutput = EarthworkAnalyzer.DrawBrucknerDiagram(data, ox, oy);
                ed.WriteMessage($"\n{drawOutput}");
            }

            // Exportar CSV?
            var csvResult = ed.GetString("\nExportar para Excel/CSV? [S/N]: ");
            if (csvResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                csvResult.StringResult.Trim().ToUpper() == "S")
            {
                var exportOutput = EarthworkAnalyzer.ExportBrucknerToCsv(data);
                ed.WriteMessage($"\n{exportOutput}");
            }
        }

        /// <summary>
        /// COMANDO: DSSECTIONS
        /// Gera seções transversais com presets customizáveis (ROAD, URBAN, EARTHWORK...)
        /// </summary>
        [CommandMethod("DSSECTIONS", CommandFlags.Modal)]
        public void GenerateSections()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n📐 DSSECTIONS — Gerador de Seções Transversais");
            ed.WriteMessage($"\n{SectionGenerator.ListPresets()}");

            var config = new SectionGenerator.SectionConfig();

            // Preset
            var presetResult = ed.GetString("\nPreset [ROAD/ROAD_DETAIL/URBAN/EARTHWORK/OVERVIEW/CUSTOM]: ");
            if (presetResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            string presetChoice = presetResult.StringResult.Trim().ToUpper();
            var presets = SectionGenerator.GetPresets();

            if (presets.ContainsKey(presetChoice))
            {
                config = presets[presetChoice];
                ed.WriteMessage($"\n✅ Preset '{presetChoice}' carregado.");
            }
            else
            {
                // CUSTOM — pergunta cada parâmetro
                var alignResult = ed.GetString("\nNome do ALINHAMENTO: ");
                if (alignResult.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;
                config.AlignmentName = alignResult.StringResult.Trim();

                var hScaleResult = ed.GetString("\nEscala HORIZONTAL [500]: ");
                if (hScaleResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    double.TryParse(hScaleResult.StringResult.Replace(".", ","), out double hs);
                // preserva o default do config

                var vScaleResult = ed.GetString("\nEscala VERTICAL [500]: ");
                if (vScaleResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    double.TryParse(vScaleResult.StringResult.Replace(".", ","), out double dummy);

                var fmtResult = ed.GetString("\nFormato [A0/A1/A2/A3/A4]: ");
                if (fmtResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    config.Format = fmtResult.StringResult.Trim().ToUpper();

                var dwtResult = ed.GetString("\nCaminho do DWT (ou ENTER para padrão): ");
                if (dwtResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    config.TemplatePath = dwtResult.StringResult.Trim();
            }

            // Sempre pergunta alinhamento e DWT (podem sobrescrever o preset)
            var alignResult2 = ed.GetString($"\nAlinhamento {(string.IsNullOrWhiteSpace(config.AlignmentName) ? "" : $"[{config.AlignmentName}]")}: ");
            if (alignResult2.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                !string.IsNullOrWhiteSpace(alignResult2.StringResult))
                config.AlignmentName = alignResult2.StringResult.Trim();

            if (string.IsNullOrWhiteSpace(config.AlignmentName))
            {
                ed.WriteMessage("\n⚠️ Nome do alinhamento é obrigatório.");
                return;
            }

            var dwtResult2 = ed.GetString($"\nDWT {(string.IsNullOrWhiteSpace(config.TemplatePath) ? "(padrão)" : $"[{config.TemplatePath}]")}: ");
            if (dwtResult2.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                !string.IsNullOrWhiteSpace(dwtResult2.StringResult))
                config.TemplatePath = dwtResult2.StringResult.Trim();

            // Confirma
            ed.WriteMessage($"\n📋 CONFIGURAÇÃO:");
            ed.WriteMessage($"\n   Alinhamento: {config.AlignmentName}");
            ed.WriteMessage($"\n   Escala: H=1:{config.HorizontalScale} V=1:{config.VerticalScale}");
            ed.WriteMessage($"\n   Formato: {config.Format} | {config.SectionsPerColumn}×{config.ColumnsPerSheet}");
            ed.WriteMessage($"\n   Sample Lines: {config.SampleInterval}m");
            ed.WriteMessage($"\n   Offsets: -{config.LeftOffset}/+{config.RightOffset}m");
            ed.WriteMessage($"\n   Volumes: {(config.IncludeVolumeTable ? "Sim" : "Não")}");
            ed.WriteMessage($"\n   Grid: {(config.IncludeGrid ? "Sim" : "Não")}");

            var confirm = ed.GetString("\nGerar seções? [S/N]: ");
            if (confirm.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK ||
                confirm.StringResult.Trim().ToUpper() != "S")
            {
                ed.WriteMessage("\n⚠️ Cancelado.");
                return;
            }

            ed.WriteMessage("\n⏳ Gerando seções...");
            var output = SectionGenerator.GenerateFromSampleLines(config);
            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSDRAINAGE

        /// <summary>
        /// COMANDO: DSDRAINAGE
        /// Projetista de drenagem inteligente com bacias automáticas
        /// </summary>
        [CommandMethod("DSDRAINAGE", CommandFlags.Modal)]
        public void DrainageDesign()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n🌊 DSDRAINAGE — Drenagem Inteligente");
            ed.WriteMessage("\n[1] A partir de CORREDOR");
            ed.WriteMessage("\n[2] A partir de SUPERFÍCIE");
            ed.WriteMessage("\n[3] Detectar pontos baixos");

            var result = ed.GetString("\nOpção [1-3]: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            var config = new DrainageDesigner.DrainageConfig();

            string output;
            switch (result.StringResult.Trim())
            {
                case "1":
                    var corr = ed.GetString("\nNome do corredor: ");
                    if (corr.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;
                    config.CorridorName = corr.StringResult.Trim();

                    var surf1 = ed.GetString("\nSuperfície base: ");
                    config.SurfaceName = surf1.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK
                        ? surf1.StringResult.Trim() : "";

                    var align1 = ed.GetString("\nAlinhamento (opcional): ");
                    config.AlignmentName = align1.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK
                        ? align1.StringResult.Trim() : "";

                    output = DrainageDesigner.CreateFromCorridor(config);
                    break;
                case "2":
                    var surf2 = ed.GetString("\nNome da superfície: ");
                    if (surf2.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;
                    config.SurfaceName = surf2.StringResult.Trim();
                    output = DrainageDesigner.CreateFromSurface(config);
                    break;
                case "3":
                    var surf3 = ed.GetString("\nNome da superfície: ");
                    if (surf3.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;
                    output = DrainageDesigner.DetectLowPoints(surf3.StringResult.Trim());
                    break;
                default:
                    output = "⚠️ Opção inválida.";
                    break;
            }

            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSGOOGLEMAPS
        /// Importa pontos do Google Maps/Earth como COGO points
        /// </summary>
        [CommandMethod("DSGOOGLEMAPS", CommandFlags.Modal)]
        public void ImportFromGoogleMaps()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n🌍 DSGOOGLEMAPS — Importar Pontos do Google Maps/Earth");
            ed.WriteMessage("\n───────────────────────────────────────────");
            ed.WriteMessage("\nFormatos suportados: KML, KMZ (Google Earth), CSV, GPX");

            ed.WriteMessage("\n🔹 Exporte do Google My Maps como KML");
            ed.WriteMessage("\n🔹 Exporte do Google Earth como KML/KMZ");
            ed.WriteMessage("\n🔹 Use CSV com coordenadas Lat,Lon,Elev");

            var result = ed.GetString("\nCaminho do arquivo (ou ENTER para abrir diálogo): ");

            string output;
            if (result.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                !string.IsNullOrWhiteSpace(result.StringResult))
            {
                string filePath = result.StringResult.Trim();
                string ext = System.IO.Path.GetExtension(filePath).ToLower();

                if (ext == ".kml" || ext == ".kmz")
                    output = GoogleMapsImporter.ImportKmlPoints(filePath);
                else if (ext == ".csv" || ext == ".txt")
                {
                    var geoResult = ed.GetString("\nCoordenadas geográficas (Lat/Lon)? [S/N]: ");
                    bool isGeo = geoResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK &&
                                 geoResult.StringResult.Trim().ToUpper() == "S";
                    output = GoogleMapsImporter.ImportCsvAsCogo(filePath, isGeo);
                }
                else
                    output = GoogleMapsImporter.SmartImport();
            }
            else
            {
                output = GoogleMapsImporter.SmartImport();
            }

            ed.WriteMessage($"\n{output}");
        }

        /// <summary>
        /// COMANDO: DSCONNECT
        /// Conecta pontos COGO automaticamente com algoritmos inteligentes
        /// </summary>
        [CommandMethod("DSCONNECT", CommandFlags.Modal)]
        public void ConnectPoints()
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            ed.WriteMessage("\n🔗 DSCONNECT — Conexão Inteligente de Pontos COGO");
            ed.WriteMessage("\n───────────────────────────────────────────");
            ed.WriteMessage("\n[1] Vizinho mais próximo (linhas)");
            ed.WriteMessage("\n[2] Por elevação (talvegue/divisor)");
            ed.WriteMessage("\n[3] Por numeração (sequência)");
            ed.WriteMessage("\n[4] TIN edges (triangulação → polilinha)");

            var result = ed.GetString("\nMétodo [1-4]: ");
            if (result.Status != Autodesk.AutoCAD.EditorInput.PromptStatus.OK) return;

            PointConnector.ConnectMethod method;
            switch (result.StringResult.Trim())
            {
                case "1": method = PointConnector.ConnectMethod.NearestNeighbor; break;
                case "2": method = PointConnector.ConnectMethod.ByElevation; break;
                case "3": method = PointConnector.ConnectMethod.BySequence; break;
                case "4": method = PointConnector.ConnectMethod.TinEdges; break;
                default: ed.WriteMessage("\n⚠️ Opção inválida."); return;
            }

            double maxDist = 50;
            if (method == PointConnector.ConnectMethod.NearestNeighbor)
            {
                var distResult = ed.GetString("\nDistância máxima de conexão (m) [50]: ");
                if (distResult.Status == Autodesk.AutoCAD.EditorInput.PromptStatus.OK)
                    double.TryParse(distResult.StringResult.Replace(".", ","), out maxDist);
            }

            ed.WriteMessage("\n🔗 Conectando pontos...");
            var output = PointConnector.AutoConnect("", method, maxDist);
            ed.WriteMessage($"\n{output}");
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
