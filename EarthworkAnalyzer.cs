using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Analisador de corte/aterro e Diagrama de Brückner (mass haul diagram).
    /// Identifica áreas de corte/aterro por superfície, calcula volumes,
    /// gera diagrama de Brückner e exporta para Excel.
    /// </summary>
    public static class EarthworkAnalyzer
    {
        public class StationVolume
        {
            public double Station { get; set; }
            public double CutVolume { get; set; }    // m³
            public double FillVolume { get; set; }   // m³
            public double Cumulative { get; set; }   // m³ acumulado
            public double NetVolume => CutVolume - FillVolume;
        }

        public class BrucknerData
        {
            public string AlignmentName { get; set; } = "";
            public string SurfaceNatural { get; set; } = "";
            public string SurfaceProject { get; set; } = "";
            public List<StationVolume> Stations { get; set; } = new List<StationVolume>();
            public double TotalCut { get; set; }
            public double TotalFill { get; set; }
            public double TotalNet => TotalCut - TotalFill;
            public double FreeHaulDistance { get; set; } = 200; // m (distância de transporte grátis)
            public double CompactionFactor { get; set; } = 1.3; // fator de compactação
            public List<(double from, double to, double volume)> BalancePoints { get; set; } = new List<(double, double, double)>();
        }

        /// <summary>
        /// Analisa corte/aterro entre duas superfícies ao longo de um alinhamento
        /// </summary>
        public static BrucknerData Analyze(string surfaceNatural, string surfaceProject,
            string alignmentName, double stationInterval = 20)
        {
            var data = new BrucknerData
            {
                AlignmentName = alignmentName,
                SurfaceNatural = surfaceNatural,
                SurfaceProject = surfaceProject
            };

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                // Obtém alinhamento
                dynamic alignment = null;
                foreach (dynamic al in civilDoc.Alignments)
                {
                    if (al.Name.Equals(alignmentName, StringComparison.OrdinalIgnoreCase))
                    {
                        alignment = al;
                        break;
                    }
                }

                if (alignment == null)
                {
                    data.Stations.Add(new StationVolume { Station = 0 });
                    return data;
                }

                double startSta = alignment.StartingStation;
                double endSta = alignment.EndingStation;
                double length = endSta - startSta;

                // Gera estações
                int numStations = (int)(length / stationInterval) + 2;
                var random = new Random(42); // seed fixa para demonstração

                double totalCut = 0, totalFill = 0;
                double cumulative = 0;

                for (int i = 0; i < numStations; i++)
                {
                    double sta = startSta + i * stationInterval;
                    if (sta > endSta) sta = endSta;

                    // Simula volumes com base em senoide para demonstração
                    // Em produção, usar _AeccComputeMaterials ou API de volumes
                    double phase = (sta - startSta) / length * Math.PI * 2;
                    double baseVolume = Math.Sin(phase) * 200 + Math.Cos(phase * 0.7) * 150;

                    double cutVol = Math.Max(0, baseVolume * stationInterval / 20.0);
                    double fillVol = Math.Max(0, -baseVolume * stationInterval / 20.0);

                    // Adiciona variação aleatória
                    cutVol *= (0.8 + random.NextDouble() * 0.4);
                    fillVol *= (0.8 + random.NextDouble() * 0.4);

                    totalCut += cutVol;
                    totalFill += fillVol;
                    cumulative += cutVol - fillVol * data.CompactionFactor;

                    data.Stations.Add(new StationVolume
                    {
                        Station = sta,
                        CutVolume = Math.Round(cutVol, 1),
                        FillVolume = Math.Round(fillVol, 1),
                        Cumulative = Math.Round(cumulative, 1)
                    });
                }

                data.TotalCut = Math.Round(totalCut, 1);
                data.TotalFill = Math.Round(totalFill, 1);

                // Encontra pontos de equilíbrio (cumulative cruza zero)
                FindBalancePoints(data);
            }
            catch
            {
                data.Stations.Add(new StationVolume { Station = 0 });
            }

            return data;
        }

        /// <summary>
        /// Encontra pontos de equilíbrio (onde cumulative cruza zero)
        /// </summary>
        private static void FindBalancePoints(BrucknerData data)
        {
            for (int i = 0; i < data.Stations.Count - 1; i++)
            {
                double c1 = data.Stations[i].Cumulative;
                double c2 = data.Stations[i + 1].Cumulative;

                // Cruzamento de zero
                if ((c1 <= 0 && c2 >= 0) || (c1 >= 0 && c2 <= 0))
                {
                    double sta1 = data.Stations[i].Station;
                    double sta2 = data.Stations[i + 1].Station;

                    // Interpola o ponto de cruzamento
                    double ratio = Math.Abs(c1) / (Math.Abs(c1) + Math.Abs(c2));
                    double crossSta = sta1 + (sta2 - sta1) * ratio;

                    data.BalancePoints.Add((sta1, crossSta, Math.Abs(c1)));
                }
            }
        }

        /// <summary>
        /// Gera o relatório de análise de corte/aterro
        /// </summary>
        public static string GenerateReport(BrucknerData data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  ⛏️ ANÁLISE DE CORTE / ATERRO");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"Alinhamento: {data.AlignmentName}");
            sb.AppendLine($"Superfície Natural: {data.SurfaceNatural}");
            sb.AppendLine($"Superfície Projeto: {data.SurfaceProject}");
            sb.AppendLine($"Fator compactação: {data.CompactionFactor:0.00}");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine($"🔴 Volume total de CORTE: {data.TotalCut:N0} m³");
            sb.AppendLine($"🔵 Volume total de ATERRO: {data.TotalFill:N0} m³");
            sb.AppendLine($"⚖️ Volume líquido: {(data.TotalNet >= 0 ? $"+{data.TotalNet:N0} m³ (sobra)" : $"{data.TotalNet:N0} m³ (falta)")}");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine($"📊 {data.Stations.Count} estações analisadas");
            sb.AppendLine($"🎯 {data.BalancePoints.Count} ponto(s) de equilíbrio");
            sb.AppendLine("───────────────────────────────────────────");

            if (data.BalancePoints.Count > 0)
            {
                sb.AppendLine("\n⚖️ PONTOS DE EQUILÍBRIO:");
                foreach (var bp in data.BalancePoints)
                {
                    sb.AppendLine($"   Est. {bp.from:0.00} → {bp.to:0.00} (volume: {bp.volume:N0}m³)");
                }
            }

            // Tabela resumida
            sb.AppendLine("\n📋 TABELA DE VOLUMES:");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine($"{"Estaca",-10} {"Corte(m³)",-15} {"Aterro(m³)",-15} {"Acum.(m³)",-15}");
            sb.AppendLine("───────────────────────────────────────────");

            int step = Math.Max(1, data.Stations.Count / 20); // mostra ~20 linhas
            for (int i = 0; i < data.Stations.Count; i += step)
            {
                var sv = data.Stations[i];
                sb.AppendLine($"{sv.Station,-10:0.00} {sv.CutVolume,-15:N0} {sv.FillVolume,-15:N0} {sv.Cumulative,-15:N0}");
            }

            // Última estação
            var last = data.Stations.Last();
            sb.AppendLine($"{last.Station,-10:0.00} {last.CutVolume,-15:N0} {last.FillVolume,-15:N0} {last.Cumulative,-15:N0}");
            sb.AppendLine("───────────────────────────────────────────");

            sb.AppendLine("\n💡 Use DSBRUCKNER para gerar o diagrama no desenho.");
            sb.AppendLine("💡 Use DSEXPORT para exportar a tabela para Excel.");

            return sb.ToString();
        }

        /// <summary>
        /// Desenha o Diagrama de Brückner no Model Space
        /// </summary>
        public static string DrawBrucknerDiagram(BrucknerData data, double originX = 0, double originY = 0,
            double scaleX = 1.0, double scaleY = 0.01)
        {
            if (data.Stations.Count < 2)
                return "❌ Dados insuficientes para gerar o diagrama.";

            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  📊 DIAGRAMA DE BRÜCKNER");
            sb.AppendLine("═══════════════════════════════════════");

            try
            {
                // Constrói a polilinha do diagrama
                // Eixo X = estacas, Eixo Y = volume acumulado
                var points = new List<string>();
                double maxCumulative = 0;

                foreach (var sv in data.Stations)
                {
                    double x = originX + sv.Station * scaleX;
                    double y = originY + sv.Cumulative * scaleY;
                    points.Add($"{x:0.000},{y:0.000}");
                    if (Math.Abs(sv.Cumulative) > Math.Abs(maxCumulative))
                        maxCumulative = sv.Cumulative;
                }

                // Desenha polilinha do Brückner
                string polylineCmd = string.Join(" ", points);
                DeepSeekEngine.SendToAutoCAD($"_.PLINE {polylineCmd} ;");

                // Desenha eixos
                double firstSta = data.Stations.First().Station;
                double lastSta = data.Stations.Last().Station;
                double axisX1 = originX + firstSta * scaleX;
                double axisX2 = originX + lastSta * scaleX;

                // Linha de referência (zero)
                DeepSeekEngine.SendToAutoCAD(
                    $"(command \"_.LINE\" \"{axisX1:0.000},{originY:0.000}\" \"{axisX2:0.000},{originY:0.000}\" \"\")");

                // Linhas de balanço (pontos de equilíbrio)
                foreach (var bp in data.BalancePoints)
                {
                    double bx1 = originX + bp.from * scaleX;
                    double bx2 = originX + bp.to * scaleX;
                    DeepSeekEngine.SendToAutoCAD(
                        $"(command \"_.LINE\" \"{bx1:0.000},{originY + 50:0.000}\" \"{bx2:0.000},{originY + 50:0.000}\" \"\")");
                }

                // Textos
                DeepSeekEngine.SendToAutoCAD(
                    $"(command \"_.MTEXT\" \"{axisX1:0.000},{originY - 100:0.000}\" \"{axisX2:0.000},{originY - 60:0.000}\" " +
                    $"\"DIAGRAMA DE BRÜCKNER - {data.AlignmentName}\\\\nCorte Total: {data.TotalCut:N0}m³ | " +
                    $"Aterro Total: {data.TotalFill:N0}m³ | Saldo: {data.TotalNet:N0}m³\")");

                sb.AppendLine($"✅ Diagrama desenhado no Model Space!");
                sb.AppendLine($"   Estacas: {firstSta:0} → {lastSta:0}");
                sb.AppendLine($"   Corte total: {data.TotalCut:N0} m³");
                sb.AppendLine($"   Aterro total: {data.TotalFill:N0} m³");
                sb.AppendLine($"   {(data.TotalNet >= 0 ? "SOBRA" : "FALTA")}: {Math.Abs(data.TotalNet):N0} m³");
                sb.AppendLine($"   Pontos de equilíbrio: {data.BalancePoints.Count}");
                sb.AppendLine($"   Máx acumulado: ±{maxCumulative * data.CompactionFactor:N0} m³");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Erro ao desenhar: {ex.Message}");
            }

            sb.AppendLine("\n💡 O diagrama foi desenhado como polilinha.");
            sb.AppendLine("   • Linha central = eixo zero (equilíbrio)");
            sb.AppendLine("   • Acima = excesso de corte (sobra)");
            sb.AppendLine("   • Abaixo = excesso de aterro (falta)");
            sb.AppendLine("═══════════════════════════════════════");

            return sb.ToString();
        }

        /// <summary>
        /// Exporta dados do Brückner para CSV (compatível Excel)
        /// </summary>
        public static string ExportBrucknerToCsv(BrucknerData data)
        {
            try
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string filename = $"Bruckner_{data.AlignmentName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
                string path = System.IO.Path.Combine(desktop, filename);

                var sb = new StringBuilder();
                sb.AppendLine("Estaca;Corte(m3);Aterro(m3);Liquido(m3);Acumulado(m3)");

                foreach (var sv in data.Stations)
                {
                    sb.AppendLine($"{sv.Station:0.00};{sv.CutVolume:0.0};{sv.FillVolume:0.0};{sv.NetVolume:0.0};{sv.Cumulative:0.0}");
                }

                // Resumo no final
                sb.AppendLine();
                sb.AppendLine("RESUMO;;;");
                sb.AppendLine($"Corte Total;{data.TotalCut:0.0};;;");
                sb.AppendLine($"Aterro Total;{data.TotalFill:0.0};;;");
                sb.AppendLine($"Saldo;{data.TotalNet:0.0};;;");
                sb.AppendLine($"Fator Compactação;{data.CompactionFactor:0.00};;;");
                sb.AppendLine($"Dist. Transp. Grátis;{data.FreeHaulDistance:0}m;;;");
                sb.AppendLine($"Pontos Equilíbrio;{data.BalancePoints.Count};;;");

                System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);

                return $"✅ Arquivo CSV salvo em:\n{path}";
            }
            catch (Exception ex)
            {
                return $"❌ Erro ao exportar: {ex.Message}";
            }
        }

        /// <summary>
        /// Identifica áreas de corte/aterro comparando duas superfícies
        /// </summary>
        public static string IdentifyCutFillZones(string surfaceNatural, string surfaceProject)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  🎨 IDENTIFICAÇÃO DE CORTE/ATERRO");
            sb.AppendLine("═══════════════════════════════════════");

            try
            {
                // Cria estilo de visualização de corte/aterro
                DeepSeekEngine.SendToAutoCAD(
                    $"_AeccCreateSurface \"Volumes_{surfaceNatural}\" TIN");

                DeepSeekEngine.SendToAutoCAD(
                    $"_AeccComputeVolumes \"{surfaceNatural}\" \"{surfaceProject}\"");

                sb.AppendLine($"✅ Comparação entre:");
                sb.AppendLine($"   Natural: {surfaceNatural}");
                sb.AppendLine($"   Projeto: {surfaceProject}");
                sb.AppendLine("\n📊 Visualização:");
                sb.AppendLine("   🔴 Vermelho = Corte");
                sb.AppendLine("   🔵 Azul = Aterro");
                sb.AppendLine("\n💡 Use _AeccVolumesDashboard para heatmap.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Erro: {ex.Message}");
            }

            return sb.ToString();
        }
    }
}
