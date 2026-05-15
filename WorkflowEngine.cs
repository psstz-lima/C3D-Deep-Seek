using System;
using System.Collections.Generic;
using System.Text;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Motor de workflows guiados — sequências completas de engenharia.
    /// Rodovia, Loteamento, Terraplenagem, Drenagem, Sinalização.
    /// </summary>
    public static class WorkflowEngine
    {
        public class WorkflowStep
        {
            public string Description { get; set; } = "";
            public string Command { get; set; } = "";
            public bool IsAuto { get; set; } = true;   // true = executa automático
            public string Notes { get; set; } = "";
        }

        public class Workflow
        {
            public string Name { get; set; } = "";
            public string Description { get; set; } = "";
            public List<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
        }

        /// <summary>
        /// Retorna o workflow correspondente ao tipo solicitado
        /// </summary>
        public static Workflow GetWorkflow(string type)
        {
            switch (type.ToUpper())
            {
                case "RODOVIA": return WorkflowRodovia();
                case "LOTEAMENTO": return WorkflowLoteamento();
                case "TERRAPLENAGEM": return WorkflowTerraplenagem();
                case "DRENAGEM": return WorkflowDrenagem();
                case "SINALIZACAO": return WorkflowSinalizacao();
                case "CORREDOR": return WorkflowCorredor();
                case "SECOES": return WorkflowSecoes();
                default: return null;
            }
        }

        public static List<string> GetAvailableWorkflows()
        {
            return new List<string> { "RODOVIA", "LOTEAMENTO", "TERRAPLENAGEM", "DRENAGEM", "SINALIZACAO", "CORREDOR", "SECOES" };
        }

        /// <summary>
        /// Executa um workflow completo passo a passo
        /// </summary>
        public static string ExecuteWorkflow(string type)
        {
            var wf = GetWorkflow(type);
            if (wf == null)
                return $"❌ Workflow '{type}' não encontrado.\nDisponíveis: {string.Join(", ", GetAvailableWorkflows())}";

            var sb = new StringBuilder();
            sb.AppendLine($"🚀 INICIANDO WORKFLOW: {wf.Name}");
            sb.AppendLine($"📋 {wf.Description}");
            sb.AppendLine("═══════════════════════════════════════");

            int stepNum = 1;
            foreach (var step in wf.Steps)
            {
                sb.AppendLine($"\n▶ PASSO {stepNum}: {step.Description}");
                if (!string.IsNullOrWhiteSpace(step.Notes))
                    sb.AppendLine($"   ℹ️ {step.Notes}");

                if (step.IsAuto && !string.IsNullOrWhiteSpace(step.Command))
                {
                    try
                    {
                        DeepSeekEngine.SendToAutoCAD(step.Command);
                        sb.AppendLine($"   ✅ Executado: {step.Command}");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"   ❌ Erro: {ex.Message}");
                    }
                }
                else if (!string.IsNullOrWhiteSpace(step.Command))
                {
                    sb.AppendLine($"   📝 Comando manual: {step.Command}");
                }

                stepNum++;
            }

            sb.AppendLine("\n═══════════════════════════════════════");
            sb.AppendLine($"✅ Workflow '{wf.Name}' concluído!");
            return sb.ToString();
        }

        // ═══════════════════════════════════
        // WORKFLOW: RODOVIA COMPLETA
        // ═══════════════════════════════════
        private static Workflow WorkflowRodovia()
        {
            return new Workflow
            {
                Name = "Projeto Rodoviário Completo",
                Description = "Alinhamento → Perfil → Corredor → Seções → Volumes → Notas de Serviço",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep { Description = "Criar superfície do terreno natural",
                        Command = "_AeccCreateSurface", Notes = "Selecione TIN e adicione pontos/curvas" },
                    new WorkflowStep { Description = "Criar alinhamento horizontal",
                        Command = "_AeccCreateAlignment", Notes = "Desenhe ou selecione polilinha do eixo" },
                    new WorkflowStep { Description = "Criar perfil do terreno",
                        Command = "_AeccCreateProfileFromSurface",
                        Notes = "Gera perfil a partir da superfície TIN" },
                    new WorkflowStep { Description = "Criar perfil de projeto (greide)",
                        Command = "_AeccCreateProfileLayout",
                        Notes = "Desenhe o greide com PIVs" },
                    new WorkflowStep { Description = "Criar montagem típica",
                        Command = "_AeccCreateAssembly",
                        Notes = "Crie a seção tipo da rodovia" },
                    new WorkflowStep { Description = "Criar corredor",
                        Command = "_AeccCreateCorridor",
                        Notes = "Combine alinhamento + perfil + montagem" },
                    new WorkflowStep { Description = "Gerar superfícies do corredor",
                        Command = "_AeccCreateCorridorSurface",
                        Notes = "Top, Datum e demais superfícies" },
                    new WorkflowStep { Description = "Criar linhas de amostra (seções)",
                        Command = "_AeccCreateSampleLines",
                        Notes = "A cada 20m nas tangentes, 10m nas curvas" },
                    new WorkflowStep { Description = "Gerar seções transversais",
                        Command = "_AeccCreateSectionViews",
                        Notes = "Múltiplas seções em folha" },
                    new WorkflowStep { Description = "Calcular volumes (corte/aterro)",
                        Command = "_AeccComputeMaterials",
                        Notes = "Tabela de volumes por estaca" },
                    new WorkflowStep { Description = "Exportar notas de serviço",
                        Command = "RELATORIO:VOLUMES", IsAuto = true,
                        Notes = "Relatório automático via C3DReports" }
                }
            };
        }

        // ═══════════════════════════════════
        // WORKFLOW: LOTEAMENTO
        // ═══════════════════════════════════
        private static Workflow WorkflowLoteamento()
        {
            return new Workflow
            {
                Name = "Projeto de Loteamento",
                Description = "Parcelas → Arruamento → Redes → Quantitativos",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep { Description = "Importar/desenhar poligonal do terreno",
                        Command = "_.PLINE", Notes = "Desenhe o contorno do terreno" },
                    new WorkflowStep { Description = "Criar alinhamentos das vias",
                        Command = "_AeccCreateAlignment",
                        Notes = "Crie alinhamentos para cada rua do loteamento" },
                    new WorkflowStep { Description = "Criar perfis das vias",
                        Command = "_AeccCreateProfileFromSurface",
                        Notes = "Gere perfis para cada alinhamento de via" },
                    new WorkflowStep { Description = "Criar parcelas (lotes)",
                        Command = "_AeccCreateParcel",
                        Notes = "Crie os lotes a partir das vias" },
                    new WorkflowStep { Description = "Criar rede de água",
                        Command = "_AeccCreatePipeNetwork",
                        Notes = "Rede de distribuição de água" },
                    new WorkflowStep { Description = "Criar rede de esgoto",
                        Command = "_AeccCreatePipeNetwork",
                        Notes = "Rede coletora de esgoto" },
                    new WorkflowStep { Description = "Criar rede de drenagem",
                        Command = "_AeccCreatePipeNetwork",
                        Notes = "Galeria de águas pluviais" },
                    new WorkflowStep { Description = "Gerar tabela de lotes",
                        Command = "_AeccCreateParcelTable",
                        Notes = "Área, perímetro, testada de cada lote" },
                    new WorkflowStep { Description = "Gerar relatório de quantitativos",
                        Command = "RELATORIO:FULL", IsAuto = true }
                }
            };
        }

        // ═══════════════════════════════════
        // WORKFLOW: TERRAPLENAGEM
        // ═══════════════════════════════════
        private static Workflow WorkflowTerraplenagem()
        {
            return new Workflow
            {
                Name = "Terraplenagem e Movimento de Terra",
                Description = "Superfície natural → Plataforma → Volumes → Notas",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep { Description = "Criar superfície do terreno natural",
                        Command = "_AeccCreateSurface", Notes = "TIN a partir de pontos/curvas" },
                    new WorkflowStep { Description = "Criar superfície de projeto (plataforma)",
                        Command = "_AeccCreateSurface",
                        Notes = "Crie a superfície da plataforma final" },
                    new WorkflowStep { Description = "Criar superfície de volumes",
                        Command = "API:CREATE_SURFACE nome=Volume tipo=TIN",
                        Notes = "Superfície para análise de volumes" },
                    new WorkflowStep { Description = "Calcular volumes",
                        Command = "_AeccComputeVolumes",
                        Notes = "Selecione terreno x plataforma" },
                    new WorkflowStep { Description = "Gerar mapa de volumes",
                        Command = "_AeccVolumesDashboard",
                        Notes = "Visualização de corte/aterro" },
                    new WorkflowStep { Description = "Gerar nota de serviço",
                        Command = "CALC:NOTA_TERRAPLENAGEM", IsAuto = true },
                    new WorkflowStep { Description = "Calcular empolamento e transporte",
                        Command = "CALC:EMPOLAMENTO", IsAuto = true }
                }
            };
        }

        // ═══════════════════════════════════
        // WORKFLOW: DRENAGEM
        // ═══════════════════════════════════
        private static Workflow WorkflowDrenagem()
        {
            return new Workflow
            {
                Name = "Projeto de Drenagem",
                Description = "Bacias → Galerias → Bocas de lobo → Dimensionamento",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep { Description = "Delimitar bacias de contribuição",
                        Command = "_AeccCreateCatchment",
                        Notes = "Crie as áreas de captação" },
                    new WorkflowStep { Description = "Criar rede de drenagem",
                        Command = "_AeccCreatePipeNetwork",
                        Notes = "Galeria de águas pluviais" },
                    new WorkflowStep { Description = "Inserir bocas de lobo",
                        Command = "_AeccCreateStructureFromNetwork",
                        Notes = "Posicione as bocas de lobo nas sarjetas" },
                    new WorkflowStep { Description = "Calcular vazão das bacias",
                        Command = "CALC:BACIA", IsAuto = true },
                    new WorkflowStep { Description = "Dimensionar tubulação",
                        Command = "CALC:DIAMETRO_TUBO", IsAuto = true },
                    new WorkflowStep { Description = "Gerar perfil da rede",
                        Command = "_AeccCreateProfileView",
                        Notes = "Visualize o perfil longitudinal da galeria" }
                }
            };
        }

        // ═══════════════════════════════════
        // WORKFLOW: SINALIZAÇÃO
        // ═══════════════════════════════════
        private static Workflow WorkflowSinalizacao()
        {
            return new Workflow
            {
                Name = "Projeto de Sinalização Viária",
                Description = "Sinalização horizontal + vertical + dispositivos de segurança",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep { Description = "Obter parâmetros da via",
                        Command = "API:LIST_ALIGNMENTS", IsAuto = true },
                    new WorkflowStep { Description = "Calcular distância de frenagem",
                        Command = "CALC:DIST_FRENAGEM", IsAuto = true },
                    new WorkflowStep { Description = "Dimensionar sinalização horizontal",
                        Command = "CALC:SINALIZACAO", IsAuto = true },
                    new WorkflowStep { Description = "Desenhar faixas de sinalização",
                        Command = "_.PLINE", Notes = "Desenhe as faixas conforme norma DNIT" },
                    new WorkflowStep { Description = "Inserir placas de sinalização",
                        Command = "_.INSERT", Notes = "Insira blocos de placas R-19, A-14, etc." },
                    new WorkflowStep { Description = "Inserir defensas metálicas",
                        Command = "_.INSERT", Notes = "Defensas em curvas e aterros altos" },
                    new WorkflowStep { Description = "Gerar quadro de sinalização",
                        Command = "RELATORIO:FULL", IsAuto = true }
                }
            };
        }

        // ═══════════════════════════════════
        // WORKFLOW: CORREDOR AUTOMÁTICO
        // ═══════════════════════════════════
        private static Workflow WorkflowCorredor()
        {
            return new Workflow
            {
                Name = "Geração Automática de Corredor",
                Description = "Montagem padrão → Corredor → Superfícies",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep { Description = "Verificar alinhamento base",
                        Command = "API:LIST_ALIGNMENTS", IsAuto = true },
                    new WorkflowStep { Description = "Criar montagem padrão DNIT",
                        Command = "_AeccCreateAssembly",
                        Notes = "Crie montagem com pista+acostamento+talude 2:3" },
                    new WorkflowStep { Description = "Vincular montagem ao corredor",
                        Command = "_AeccCreateCorridor",
                        Notes = "Selecione alinhamento, perfil e montagem" },
                    new WorkflowStep { Description = "Criar targets (opcional)",
                        Command = "_AeccSetCorridorTargets",
                        Notes = "Defina alvos de largura e elevação" },
                    new WorkflowStep { Description = "Gerar superfície de topo",
                        Command = "_AeccCreateCorridorSurface", Notes = "Surface: Top" },
                    new WorkflowStep { Description = "Gerar superfície de datum",
                        Command = "_AeccCreateCorridorSurface", Notes = "Surface: Datum" }
                }
            };
        }

        // ═══════════════════════════════════
        // WORKFLOW: SEÇÕES TÉCNICAS
        // ═══════════════════════════════════
        private static Workflow WorkflowSecoes()
        {
            return new Workflow
            {
                Name = "Geração de Seções Técnicas",
                Description = "Sample Lines → Seções → Diagramas → Folhas",
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep { Description = "Criar linhas de amostra",
                        Command = "_AeccCreateSampleLines",
                        Notes = "Sample lines a cada 20m + pontos notáveis" },
                    new WorkflowStep { Description = "Criar múltiplas seções",
                        Command = "_AeccCreateMultipleSectionViews",
                        Notes = "Layout de folha A1 com 5 seções por coluna" },
                    new WorkflowStep { Description = "Adicionar diagramas de volume",
                        Command = "_AeccAddMaterialSection",
                        Notes = "Diagrama de corte/aterro nas seções" },
                    new WorkflowStep { Description = "Exportar folhas de seção",
                        Command = "_AeccCreateSheets",
                        Notes = "Gera folhas prontas para plotagem" }
                }
            };
        }
    }
}
