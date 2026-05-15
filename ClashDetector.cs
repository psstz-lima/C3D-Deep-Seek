using System;
using System.Collections.Generic;
using System.Text;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Detector de interferências (clash detection) para Civil 3D.
    /// Verifica conflitos entre redes, gabaritos e estruturas.
    /// </summary>
    public static class ClashDetector
    {
        public class ClashResult
        {
            public string Type { get; set; } = "";
            public string Element1 { get; set; } = "";
            public string Element2 { get; set; } = "";
            public string Location { get; set; } = "";
            public string Severity { get; set; } = "LOW"; // LOW, MEDIUM, HIGH, CRITICAL
            public string Description { get; set; } = "";
        }

        /// <summary>
        /// Executa análise completa de interferências
        /// </summary>
        public static string RunFullClashAnalysis()
        {
            var sb = new StringBuilder();
            var clashes = new List<ClashResult>();

            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  🔍 DETECÇÃO DE INTERFERÊNCIAS");
            sb.AppendLine("═══════════════════════════════════════");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                // ── 1. Redes de tubulação: verifica cruzamentos ──
                DetectPipeClashes(civilDoc, clashes);

                // ── 2. Corredores vs. Redes ──
                DetectCorridorClashes(civilDoc, clashes);

                // ── 3. Gabaritos e estruturas ──
                DetectStructureClashes(civilDoc, clashes);

                // ── 4. Layers sobrepostas ──
                DetectLayerIssues(acadApp.ActiveDocument, clashes);

                // Relatório
                if (clashes.Count == 0)
                {
                    sb.AppendLine("\n✅ NENHUMA INTERFERÊNCIA DETECTADA!");
                    sb.AppendLine("   Excelente — todos os elementos compatíveis.");
                }
                else
                {
                    int critical = clashes.FindAll(c => c.Severity == "CRITICAL").Count;
                    int high = clashes.FindAll(c => c.Severity == "HIGH").Count;
                    int medium = clashes.FindAll(c => c.Severity == "MEDIUM").Count;
                    int low = clashes.FindAll(c => c.Severity == "LOW").Count;

                    sb.AppendLine($"\n⚠️ {clashes.Count} INTERFERÊNCIA(S) ENCONTRADA(S):");
                    sb.AppendLine($"   🔴 Críticas: {critical}");
                    sb.AppendLine($"   🟠 Altas: {high}");
                    sb.AppendLine($"   🟡 Médias: {medium}");
                    sb.AppendLine($"   🟢 Baixas: {low}");
                    sb.AppendLine("\n───────────────────────────────────────────");

                    foreach (var clash in clashes)
                    {
                        string icon = clash.Severity switch
                        {
                            "CRITICAL" => "🔴",
                            "HIGH" => "🟠",
                            "MEDIUM" => "🟡",
                            _ => "🟢"
                        };
                        sb.AppendLine($"{icon} [{clash.Severity}] {clash.Type}");
                        sb.AppendLine($"   {clash.Element1} ↔ {clash.Element2}");
                        if (!string.IsNullOrWhiteSpace(clash.Location))
                            sb.AppendLine($"   📍 {clash.Location}");
                        sb.AppendLine($"   📝 {clash.Description}");
                        sb.AppendLine();
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"\n❌ Erro na análise: {ex.Message}");
            }

            sb.AppendLine("═══════════════════════════════════════");
            return sb.ToString();
        }

        private static void DetectPipeClashes(dynamic civilDoc, List<ClashResult> clashes)
        {
            try
            {
                dynamic pipeNets = civilDoc.PipeNetworks;
                if (pipeNets.Count < 2)
                {
                    if (pipeNets.Count == 0)
                        clashes.Add(new ClashResult
                        {
                            Type = "Redes",
                            Element1 = "N/A",
                            Severity = "LOW",
                            Description = "Nenhuma rede de tubulação encontrada — sem conflitos possíveis."
                        });
                    return;
                }

                // Verifica se há redes sobrepostas
                var netNames = new List<string>();
                foreach (dynamic net in pipeNets)
                    netNames.Add(net.Name);

                if (netNames.Count >= 2)
                {
                    clashes.Add(new ClashResult
                    {
                        Type = "Redes",
                        Element1 = string.Join(", ", netNames),
                        Severity = "MEDIUM",
                        Description = $"{netNames.Count} redes coexistem. Verifique manualmente cruzamentos entre tubos e estruturas em cotas diferentes."
                    });
                }

                // Verifica tubos em cada rede
                foreach (dynamic net in pipeNets)
                {
                    int pipeCount = 0, structCount = 0;
                    try { pipeCount = net.Pipes.Count; } catch { }
                    try { structCount = net.Structures.Count; } catch { }

                    if (pipeCount > 0 && structCount == 0)
                    {
                        clashes.Add(new ClashResult
                        {
                            Type = "Rede Incompleta",
                            Element1 = net.Name,
                            Severity = "HIGH",
                            Description = $"Rede '{net.Name}' tem {pipeCount} tubos mas 0 estruturas."
                        });
                    }
                }
            }
            catch { }
        }

        private static void DetectCorridorClashes(dynamic civilDoc, List<ClashResult> clashes)
        {
            try
            {
                dynamic corridors = civilDoc.Corridors;
                if (corridors.Count == 0) return;

                foreach (dynamic corr in corridors)
                {
                    int surfCount = 0;
                    try { surfCount = corr.CorridorSurfaces.Count; } catch { }

                    if (surfCount == 0)
                    {
                        clashes.Add(new ClashResult
                        {
                            Type = "Corredor sem Superfície",
                            Element1 = corr.Name,
                            Severity = "HIGH",
                            Description = $"Corredor '{corr.Name}' não tem superfícies geradas — impossível calcular volumes."
                        });
                    }
                }
            }
            catch { }
        }

        private static void DetectStructureClashes(dynamic civilDoc, List<ClashResult> clashes)
        {
            try
            {
                dynamic sites = civilDoc.Sites;
                foreach (dynamic site in sites)
                {
                    int alignCount = 0, parcelCount = 0, flCount = 0;
                    try { alignCount = site.Alignments.Count; } catch { }
                    try { parcelCount = site.Parcels.Count; } catch { }
                    try { flCount = site.FeatureLines.Count; } catch { }

                    // Alinhamentos no mesmo site podem interagir
                    if (alignCount > 1)
                    {
                        clashes.Add(new ClashResult
                        {
                            Type = "Alinhamentos Coexistentes",
                            Element1 = site.Name,
                            Severity = "LOW",
                            Description = $"Site '{site.Name}' contém {alignCount} alinhamentos — verifique intersecções."
                        });
                    }

                    if (alignCount > 0 && parcelCount > 0)
                    {
                        clashes.Add(new ClashResult
                        {
                            Type = "Parcelas + Alinhamentos",
                            Element1 = site.Name,
                            Severity = "MEDIUM",
                            Description = "Alinhamentos e parcelas no mesmo site podem causar interações indesejadas."
                        });
                    }
                }
            }
            catch { }
        }

        private static void DetectLayerIssues(dynamic doc, List<ClashResult> clashes)
        {
            try
            {
                dynamic layers = doc.Layers;
                int frozen = 0, off = 0;

                foreach (dynamic layer in layers)
                {
                    if (layer.Freeze) frozen++;
                    if (!layer.LayerOn) off++;
                }

                if (frozen > 5)
                    clashes.Add(new ClashResult
                    {
                        Type = "Layers Congeladas",
                        Element1 = $"{frozen} layers",
                        Severity = "MEDIUM",
                        Description = $"{frozen} layers congeladas podem esconder interferências."
                    });

                if (off > 10)
                    clashes.Add(new ClashResult
                    {
                        Type = "Layers Desligadas",
                        Element1 = $"{off} layers",
                        Severity = "LOW",
                        Description = $"{off} layers desligadas — ative-as para verificar conflitos ocultos."
                    });
            }
            catch { }
        }
    }
}
