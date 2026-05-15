using System;
using System.Collections.Generic;
using System.Text;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Projetista de drenagem inteligente — cria redes de drenagem automaticamente
    /// a partir de corredores ou superfícies. Gera bacias de contribuição,
    /// bocas de lobo, galerias e PVs com dimensionamento hidráulico integrado.
    /// </summary>
    public static class DrainageDesigner
    {
        public class DrainageConfig
        {
            public string SurfaceName { get; set; } = "";
            public string CorridorName { get; set; } = "";
            public string AlignmentName { get; set; } = "";
            public double BocaloboSpacing { get; set; } = 40; // metros
            public double PipeDiameter { get; set; } = 0.40;  // 400mm
            public double MinSlope { get; set; } = 0.005;     // 0.5%
            public double MaxSlope { get; set; } = 0.10;      // 10%
            public double CatchmentWidth { get; set; } = 50;  // largura de captação
            public bool IncludeCatchments { get; set; } = true;
            public bool IncludeInlets { get; set; } = true;
            public bool IncludePipes { get; set; } = true;
            public bool IncludeStructures { get; set; } = true;
        }

        /// <summary>
        /// Cria sistema de drenagem completo a partir de um corredor
        /// </summary>
        public static string CreateFromCorridor(DrainageConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  🌊 DRENAGEM INTELIGENTE — CORREDOR");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"Corredor: {config.CorridorName}");
            sb.AppendLine($"Superfície: {config.SurfaceName}");
            sb.AppendLine($"Espaçamento bocas de lobo: {config.BocaloboSpacing}m");
            sb.AppendLine("───────────────────────────────────────────");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                // ── PASSO 1: Criar rede de drenagem ──
                string networkName = $"Drenagem_{config.CorridorName}";
                if (config.IncludePipes)
                {
                    DeepSeekEngine.SendToAutoCAD($"_AeccCreatePipeNetwork \"{networkName}\"");
                    sb.AppendLine($"✅ Rede '{networkName}' criada.");
                }

                // ── PASSO 2: Criar bacias de contribuição ──
                if (config.IncludeCatchments)
                {
                    // Cria bacias ao longo do corredor
                    DeepSeekEngine.SendToAutoCAD($"_AeccCreateCatchmentGroup \"Bacias_{config.CorridorName}\"");

                    // Tenta obter dados do alinhamento para posicionar bacias
                    if (!string.IsNullOrWhiteSpace(config.AlignmentName))
                    {
                        try
                        {
                            dynamic alignment = null;
                            foreach (dynamic al in civilDoc.Alignments)
                            {
                                if (al.Name.Equals(config.AlignmentName, StringComparison.OrdinalIgnoreCase))
                                { alignment = al; break; }
                            }

                            if (alignment != null)
                            {
                                double startSta = alignment.StartingStation;
                                double endSta = alignment.EndingStation;
                                double spacing = config.BocaloboSpacing;

                                sb.AppendLine($"✅ Alinhamento: {config.AlignmentName}");
                                sb.AppendLine($"   Estacas: {startSta:0} → {endSta:0}");

                                // Cria bacias a cada espaçamento
                                int numCatchments = (int)((endSta - startSta) / spacing) + 1;
                                for (int i = 0; i < numCatchments; i++)
                                {
                                    double sta = startSta + i * spacing;
                                    if (sta > endSta) sta = endSta;

                                    DeepSeekEngine.SendToAutoCAD(
                                        $"_AeccCreateCatchment \"Bacia_E{sta:0}\" \"{config.SurfaceName}\" {sta}");
                                }

                                sb.AppendLine($"✅ {numCatchments} bacias criadas ao longo do alinhamento.");
                            }
                        }
                        catch
                        {
                            sb.AppendLine("⚠️ Criação automática de bacias parcial. Crie manualmente.");
                        }
                    }
                }

                // ── PASSO 3: Inserir bocas de lobo ──
                if (config.IncludeInlets)
                {
                    DeepSeekEngine.SendToAutoCAD(
                        $"(command \"_AeccAddInletsToNetwork\" \"{networkName}\")");
                    sb.AppendLine($"✅ Bocas de lobo adicionadas à rede.");
                }

                // ── PASSO 4: Inserir estruturas (PVs) ──
                if (config.IncludeStructures)
                {
                    DeepSeekEngine.SendToAutoCAD(
                        $"(command \"_AeccAddStructuresToNetwork\" \"{networkName}\")");
                    sb.AppendLine($"✅ Estruturas (PVs) adicionadas à rede.");
                }

                // ── PASSO 5: Dimensionamento hidráulico ──
                sb.AppendLine("\n📐 DIMENSIONAMENTO:");
                sb.AppendLine($"   Diâmetro sugerido: {config.PipeDiameter * 1000:0}mm");
                sb.AppendLine($"   Declividade mín: {config.MinSlope * 100:0.0}%");
                sb.AppendLine($"   Declividade máx: {config.MaxSlope * 100:0.0}%");

                // Calcula vazão estimada (Método Racional simplificado)
                double area = config.CatchmentWidth * 1000 / 10000; // hectares (faixa de 50m x 1km)
                double C = 0.7;
                double I = 120; // mm/h
                double Q = C * I * area / 360; // m³/s

                sb.AppendLine($"\n🌧️ VAZÃO ESTIMADA:");
                sb.AppendLine($"   Área de captação: {area:0.0} ha");
                sb.AppendLine($"   Vazão de pico: {Q * 1000:0} L/s");

                double dCalc = Math.Pow(Q * 0.013 / (0.3117 * Math.Pow(config.MinSlope, 0.5)), 3.0 / 8.0);
                sb.AppendLine($"   Diâmetro calculado: {dCalc * 100:0} cm");

            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Erro: {ex.Message}");
            }

            sb.AppendLine("\n═══════════════════════════════════════");
            sb.AppendLine("💡 Use DSCALC DIAMETRO_TUBO para dimensionamento detalhado.");
            sb.AppendLine("💡 Use DSWORKFLOW DRENAGEM para o fluxo completo.");
            return sb.ToString();
        }

        /// <summary>
        /// Cria sistema de drenagem baseado em superfície de escoamento
        /// </summary>
        public static string CreateFromSurface(DrainageConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  🌊 DRENAGEM INTELIGENTE — SUPERFÍCIE");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"Superfície: {config.SurfaceName}");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                string networkName = $"Drenagem_{config.SurfaceName}";

                // Cria rede
                DeepSeekEngine.SendToAutoCAD($"_AeccCreatePipeNetwork \"{networkName}\"");
                sb.AppendLine($"✅ Rede '{networkName}' criada.");

                // Análise de fluxo da superfície
                DeepSeekEngine.SendToAutoCAD($"_AeccWaterDrop \"{config.SurfaceName}\"");
                sb.AppendLine("✅ Análise de fluxo iniciada.");
                sb.AppendLine("   Selecione pontos na superfície para traçar o caminho da água.");

                // Sugere bacias baseadas nos pontos baixos
                if (config.IncludeCatchments)
                {
                    DeepSeekEngine.SendToAutoCAD($"_AeccCreateCatchmentFromSurface \"{config.SurfaceName}\"");
                    sb.AppendLine("✅ Criação de bacias por superfície iniciada.");
                }

                sb.AppendLine("\n💡 DICA: Use o Water Drop para identificar caminhos naturais de escoamento.");
                sb.AppendLine("   Posicione bocas de lobo nos pontos baixos identificados.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Erro: {ex.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Detecta automaticamente pontos baixos da superfície para posicionar drenagem
        /// </summary>
        public static string DetectLowPoints(string surfaceName, int topN = 10)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  🔍 DETECÇÃO DE PONTOS BAIXOS");
            sb.AppendLine("═══════════════════════════════════════");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                dynamic surface = null;
                foreach (dynamic s in civilDoc.Surfaces)
                {
                    if (s.Name.Equals(surfaceName, StringComparison.OrdinalIgnoreCase))
                    { surface = s; break; }
                }

                if (surface == null)
                {
                    sb.AppendLine($"❌ Superfície '{surfaceName}' não encontrada.");
                    return sb.ToString();
                }

                // Tenta acessar estatísticas para identificar range
                try
                {
                    double minElev = surface.Statistics.MinElevation;
                    double maxElev = surface.Statistics.MaxElevation;
                    double range = maxElev - minElev;

                    sb.AppendLine($"Superfície: {surfaceName}");
                    sb.AppendLine($"Elevação mín: {minElev:0.000}m");
                    sb.AppendLine($"Elevação máx: {maxElev:0.000}m");
                    sb.AppendLine($"Range: {range:0.000}m");
                    sb.AppendLine("\n📋 SUGESTÕES DE POSICIONAMENTO:");
                    sb.AppendLine("   • Bocas de lobo nos pontos baixos");
                    sb.AppendLine("   • Galerias seguindo talvegues");
                    sb.AppendLine("   • Dissipadores em pontos de alta declividade");

                    // Sugere locais baseados em percentis
                    double bottom10 = minElev + range * 0.10;
                    sb.AppendLine($"\n   Pontos baixos (< {bottom10:0.000}m): concentre drenagem");
                    sb.AppendLine($"   Pontos altos (> {minElev + range * 0.90:0.000}m): divisores de bacia");
                }
                catch
                {
                    sb.AppendLine("⚠️ Não foi possível acessar estatísticas da superfície.");
                    sb.AppendLine("   Execute _AeccSurfaceProperties para análise detalhada.");
                }

                sb.AppendLine("\n💡 Use o comando _AeccWaterDrop para traçar escoamento.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Erro: {ex.Message}");
            }

            return sb.ToString();
        }
    }
}
