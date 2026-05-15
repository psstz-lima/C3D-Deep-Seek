using System;
using System.Collections.Generic;
using System.Text;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Geração de relatórios premium do Civil 3D:
    /// quantitativos, análise de superfícies, alinhamentos, volumes, BIM.
    /// </summary>
    public static class C3DReports
    {
        /// <summary>
        /// Gera relatório conforme o tipo solicitado
        /// </summary>
        public static string Generate(string reportType)
        {
            switch (reportType.ToUpper())
            {
                case "FULL": return GenerateFullReport();
                case "SURFACES": return GenerateSurfaceReport();
                case "ALIGNMENTS": return GenerateAlignmentReport();
                case "CORRIDORS": return GenerateCorridorReport();
                case "PIPES": return GeneratePipeNetworkReport();
                case "VOLUMES": return GenerateVolumeReport();
                case "LAYERS": return GenerateLayerReport();
                case "QUANTITIES": return GenerateQuantitiesReport();
                case "BIM": return GenerateBimReport();
                default: return GenerateFullReport();
            }
        }

        private static string GenerateFullReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("  📊 RELATÓRIO COMPLETO DO PROJETO");
            sb.AppendLine("═══════════════════════════════════════════");

            var context = DeepSeekContext.CollectContext();
            sb.AppendLine(context);

            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine(GenerateLayerReport());
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine(GenerateQuantitiesReport());

            sb.AppendLine("═══════════════════════════════════════════");
            return sb.ToString();
        }

        private static string GenerateSurfaceReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("⛰️  RELATÓRIO DE SUPERFÍCIES");
            sb.AppendLine("───────────────────────────────────────────");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                dynamic surfaces = civilDoc.Surfaces;
                sb.AppendLine($"Total de superfícies: {surfaces.Count}");

                foreach (dynamic surf in surfaces)
                {
                    sb.AppendLine($"  ▸ {surf.Name}");
                    sb.AppendLine($"    Tipo: {surf.Type}");
                    try { sb.AppendLine($"    Estilo: {surf.Style.Name}"); } catch { }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ Erro ao ler superfícies: {ex.Message}");
            }

            return sb.ToString();
        }

        private static string GenerateAlignmentReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("🛤️  RELATÓRIO DE ALINHAMENTOS");
            sb.AppendLine("───────────────────────────────────────────");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                dynamic alignments = civilDoc.Alignments;
                sb.AppendLine($"Total de alinhamentos: {alignments.Count}");

                foreach (dynamic align in alignments)
                {
                    sb.AppendLine($"  ▸ {align.Name}");
                    try { sb.AppendLine($"    Estações: {align.StartingStation:0.00} → {align.EndingStation:0.00}"); } catch { }
                    try { sb.AppendLine($"    Comprimento: {align.Length:0.00}m"); } catch { }
                    try { sb.AppendLine($"    Estilo: {align.Style.Name}"); } catch { }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ Erro ao ler alinhamentos: {ex.Message}");
            }

            return sb.ToString();
        }

        private static string GenerateCorridorReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("🛣️  RELATÓRIO DE CORREDORES");
            sb.AppendLine("───────────────────────────────────────────");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                dynamic corridors = civilDoc.Corridors;
                sb.AppendLine($"Total de corredores: {corridors.Count}");

                foreach (dynamic corr in corridors)
                {
                    sb.AppendLine($"  ▸ {corr.Name}");
                    try { sb.AppendLine($"    Baseline: {corr.Baselines.Count}"); } catch { }
                    try { sb.AppendLine($"    Regiões: {corr.BaselineRegions.Count}"); } catch { }
                    try { sb.AppendLine($"    Superfícies geradas: {corr.CorridorSurfaces.Count}"); } catch { }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ Erro ao ler corredores: {ex.Message}");
            }

            return sb.ToString();
        }

        private static string GeneratePipeNetworkReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("🔧 RELATÓRIO DE REDES DE TUBULAÇÃO");
            sb.AppendLine("───────────────────────────────────────────");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                dynamic pipeNets = civilDoc.PipeNetworks;
                sb.AppendLine($"Total de redes: {pipeNets.Count}");

                foreach (dynamic net in pipeNets)
                {
                    sb.AppendLine($"  ▸ {net.Name}");
                    try { sb.AppendLine($"    Tubos: {net.Pipes.Count}"); } catch { }
                    try { sb.AppendLine($"    Estruturas: {net.Structures.Count}"); } catch { }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ Erro ao ler redes: {ex.Message}");
            }

            return sb.ToString();
        }

        private static string GenerateVolumeReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("📦 RELATÓRIO DE VOLUMES (CORTE/ATERRO)");
            sb.AppendLine("───────────────────────────────────────────");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                dynamic surfaces = civilDoc.Surfaces;
                if (surfaces.Count >= 2)
                {
                    sb.AppendLine("⚠️ Para cálculo de volumes, use o comando:");
                    sb.AppendLine("   COMANDO: _AeccComputeVolumes");
                    sb.AppendLine($"   Superfícies disponíveis: {surfaces.Count}");
                    foreach (dynamic surf in surfaces)
                        sb.AppendLine($"     • {surf.Name}");
                }
                else
                {
                    sb.AppendLine("⚠️ São necessárias 2 superfícies para cálculo de volumes.");
                    sb.AppendLine($"   Superfícies atuais: {surfaces.Count}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ {ex.Message}");
            }

            return sb.ToString();
        }

        private static string GenerateLayerReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("📐 RELATÓRIO DE LAYERS");
            sb.AppendLine("───────────────────────────────────────────");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic doc = acadApp.ActiveDocument;
                dynamic layers = doc.Layers;

                int total = 0, frozen = 0, off = 0, locked = 0;
                var activeLayers = new List<string>();

                foreach (dynamic layer in layers)
                {
                    total++;
                    if (layer.Freeze) frozen++;
                    if (!layer.LayerOn) off++;
                    if (layer.Lock) locked++;
                    if (!layer.Freeze && layer.LayerOn)
                        activeLayers.Add(layer.Name);
                }

                sb.AppendLine($"Total: {total} layers");
                sb.AppendLine($"Ativas: {total - frozen - off} | Congeladas: {frozen} | Desligadas: {off} | Bloqueadas: {locked}");
                sb.AppendLine($"Layer atual: {doc.ActiveLayer.Name}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ {ex.Message}");
            }

            return sb.ToString();
        }

        private static string GenerateQuantitiesReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("📏 QUANTITATIVOS DO MODEL SPACE");
            sb.AppendLine("───────────────────────────────────────────");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic doc = acadApp.ActiveDocument;
                dynamic ms = doc.ModelSpace;

                int lines = 0, plines = 0, circles = 0, arcs = 0, texts = 0, mtexts = 0;
                int blocks = 0, hatches = 0, dims = 0, points = 0, solids = 0;

                foreach (dynamic entity in ms)
                {
                    switch (entity.EntityName)
                    {
                        case "AcDbLine": lines++; break;
                        case "AcDbPolyline": plines++; break;
                        case "AcDb2dPolyline": plines++; break;
                        case "AcDb3dPolyline": plines++; break;
                        case "AcDbCircle": circles++; break;
                        case "AcDbArc": arcs++; break;
                        case "AcDbText": texts++; break;
                        case "AcDbMText": mtexts++; break;
                        case "AcDbBlockReference": blocks++; break;
                        case "AcDbHatch": hatches++; break;
                        case "AcDbRotatedDimension": dims++; break;
                        case "AcDbAlignedDimension": dims++; break;
                        case "AcDbPoint": points++; break;
                        case "AcDb3dSolid": solids++; break;
                    }
                }

                sb.AppendLine($"Linhas: {lines}     | Polilinhas: {plines}");
                sb.AppendLine($"Círculos: {circles} | Arcos: {arcs}");
                sb.AppendLine($"Textos: {texts}     | MTexts: {mtexts}");
                sb.AppendLine($"Blocos: {blocks}    | Hachuras: {hatches}");
                sb.AppendLine($"Cotas: {dims}       | Pontos COGO: {points}");
                sb.AppendLine($"Sólidos 3D: {solids}");
                sb.AppendLine($"───────────────────────────────────────────");
                sb.AppendLine($"TOTAL DE ENTIDADES: {lines + plines + circles + arcs + texts + mtexts + blocks + hatches + dims + points + solids}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ {ex.Message}");
            }

            return sb.ToString();
        }

        private static string GenerateBimReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("🏗️  RELATÓRIO BIM / COMPATIBILIZAÇÃO");
            sb.AppendLine("───────────────────────────────────────────");

            sb.AppendLine("✅ IFC Export: _.EXPORTIFC");
            sb.AppendLine("✅ IFC Import: _.IMPORTIFC");
            sb.AppendLine("✅ Coordenadas compartilhadas: _AeccSetCoordinateSystem");
            sb.AppendLine("✅ Vinculação Revit: _AeccImportRevitModel");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic doc = acadApp.ActiveDocument;

                sb.AppendLine($"📄 Desenho: {doc.Name}");
                sb.AppendLine($"📐 Unidades: {doc.GetVariable("INSUNITS")}");

                // Verifica XREFs (vínculos BIM)
                try
                {
                    dynamic blocks = doc.Blocks;
                    int xrefCount = 0;
                    foreach (dynamic blk in blocks)
                    {
                        if (blk.IsXRef) xrefCount++;
                    }
                    sb.AppendLine($"🔗 XREFs vinculados: {xrefCount}");
                }
                catch { }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"⚠️ {ex.Message}");
            }

            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("💡 Dica: Use REPORT:BIM para validar compatibilidade BIM");
            return sb.ToString();
        }
    }
}
