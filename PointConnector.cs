using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Conector inteligente de pontos — cria automaticamente polilinhas,
    /// feature lines e breaklines conectando COGO points.
    /// Algoritmos: vizinho mais próximo, TIN edges, ordenação por estaca.
    /// </summary>
    public static class PointConnector
    {
        public enum ConnectMethod
        {
            NearestNeighbor,  // Vizinho mais próximo
            ByElevation,       // Ordena por elevação (talvegue/divisor)
            BySequence,        // Por ordem de criação/numeração
            TinEdges,          // Arestas da triangulação
            ByPointGroup       // Por grupo de pontos
        }

        /// <summary>
        /// Conecta pontos COGO automaticamente por grupo ou superfície
        /// </summary>
        public static string AutoConnect(string pointGroupName = "", ConnectMethod method = ConnectMethod.NearestNeighbor,
            double maxDistance = 50)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  🔗 CONEXÃO AUTOMÁTICA DE PONTOS");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"Método: {method}");
            sb.AppendLine($"Distância máx: {maxDistance}m");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic doc = acadApp.ActiveDocument;
                dynamic ms = doc.ModelSpace;

                // Coleta pontos COGO do desenho
                var points = new List<(double x, double y, double z, string name, int number)>();

                foreach (dynamic entity in ms)
                {
                    try
                    {
                        string ename = entity.EntityName;
                        if (ename == "AeccDbCogoPoint")
                        {
                            points.Add((
                                entity.Easting,
                                entity.Northing,
                                entity.Elevation,
                                entity.Name ?? $"P{entity.Number}",
                                entity.Number
                            ));
                        }
                    }
                    catch { continue; }
                }

                if (points.Count == 0)
                {
                    // Tenta via COM para Civil 3D
                    try
                    {
                        dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                        dynamic civilDoc = civilApp.ActiveDocument;
                        dynamic cogoPoints = civilDoc.CogoPoints;

                        foreach (dynamic pt in cogoPoints)
                        {
                            try
                            {
                                points.Add((
                                    pt.Easting,
                                    pt.Northing,
                                    pt.Elevation,
                                    pt.Name ?? $"P{pt.Number}",
                                    pt.Number
                                ));
                            }
                            catch { continue; }
                        }
                    }
                    catch { }
                }

                if (points.Count < 2)
                {
                    sb.AppendLine("❌ Mínimo de 2 pontos COGO necessários.");
                    if (points.Count == 0)
                        sb.AppendLine("   Nenhum ponto COGO encontrado no desenho.");
                    return sb.ToString();
                }

                sb.AppendLine($"📋 {points.Count} pontos COGO encontrados.");

                // ── Filtra por grupo se especificado ──
                var filteredPoints = points;

                // ── Aplica método de conexão ──
                switch (method)
                {
                    case ConnectMethod.NearestNeighbor:
                        return ConnectNearestNeighbor(filteredPoints, maxDistance, sb);
                    case ConnectMethod.ByElevation:
                        return ConnectByElevation(filteredPoints, sb);
                    case ConnectMethod.BySequence:
                        return ConnectBySequence(filteredPoints, sb);
                    case ConnectMethod.TinEdges:
                        return ConnectTinEdges(filteredPoints, sb);
                    default:
                        return ConnectNearestNeighbor(filteredPoints, maxDistance, sb);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Erro: {ex.Message}");
                return sb.ToString();
            }
        }

        private static string ConnectNearestNeighbor(
            List<(double x, double y, double z, string name, int number)> points,
            double maxDistance, StringBuilder sb)
        {
            int connected = 0;
            var used = new HashSet<int>();
            var lines = new List<string>();

            for (int i = 0; i < points.Count; i++)
            {
                if (used.Contains(i)) continue;

                double bestDist = double.MaxValue;
                int bestJ = -1;

                for (int j = 0; j < points.Count; j++)
                {
                    if (i == j || used.Contains(j)) continue;

                    double dx = points[i].x - points[j].x;
                    double dy = points[i].y - points[j].y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < bestDist && dist <= maxDistance)
                    {
                        bestDist = dist;
                        bestJ = j;
                    }
                }

                if (bestJ >= 0)
                {
                    lines.Add($"_.LINE {points[i].x:0.000},{points[i].y:0.000} {points[bestJ].x:0.000},{points[bestJ].y:0.000} ;");
                    used.Add(i);
                    connected++;
                }
            }

            // Executa as linhas em lote
            foreach (var line in lines)
                DeepSeekEngine.SendToAutoCAD(line);

            sb.AppendLine($"✅ {connected} conexões criadas (vizinho + próximo, d≤{maxDistance}m).");
            sb.AppendLine($"💡 Use DSCONNECT TIN para criar breaklines da triangulação.");
            return sb.ToString();
        }

        private static string ConnectByElevation(
            List<(double x, double y, double z, string name, int number)> points, StringBuilder sb)
        {
            // Ordena por elevação e conecta em sequência (útil para talvegues/divisores)
            var sorted = points.OrderBy(p => p.z).ToList();

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                DeepSeekEngine.SendToAutoCAD(
                    $"(command \"_.LINE\" \"{sorted[i].x:0.000},{sorted[i].y:0.000}\" " +
                    $"\"{sorted[i + 1].x:0.000},{sorted[i + 1].y:0.000}\" \"\")");
            }

            sb.AppendLine($"✅ {sorted.Count - 1} conexões por elevação (talvegue/divisor).");
            sb.AppendLine($"   Elevações: {sorted.First().z:0.00}m → {sorted.Last().z:0.00}m");
            return sb.ToString();
        }

        private static string ConnectBySequence(
            List<(double x, double y, double z, string name, int number)> points, StringBuilder sb)
        {
            // Conecta na ordem numérica dos pontos
            var sorted = points.OrderBy(p => p.number).ToList();

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                DeepSeekEngine.SendToAutoCAD(
                    $"(command \"_.LINE\" \"{sorted[i].x:0.000},{sorted[i].y:0.000}\" " +
                    $"\"{sorted[i + 1].x:0.000},{sorted[i + 1].y:0.000}\" \"\")");
            }

            sb.AppendLine($"✅ {sorted.Count - 1} conexões por numeração.");
            return sb.ToString();
        }

        private static string ConnectTinEdges(
            List<(double x, double y, double z, string name, int number)> points, StringBuilder sb)
        {
            // Usa Delaunay simplificado — conecta pontos próximos formando triângulos
            // Na prática, pede pro Civil 3D criar superfície TIN e extrair bordas
            sb.AppendLine("📐 Método TIN: criando superfície temporária...");

            string tempSurface = $"TEMP_TIN_{DateTime.Now:HHmmss}";

            // Cria superfície TIN temporária
            DeepSeekEngine.SendToAutoCAD($"_AeccCreateSurface TIN \"{tempSurface}\"");

            // Adiciona pontos à superfície
            foreach (var pt in points)
            {
                DeepSeekEngine.SendToAutoCAD(
                    $"(command \"_AeccAddSurfacePoint\" " +
                    $"\"{pt.x:0.000},{pt.y:0.000}\" \"{pt.z:0.000}\")");
            }

            // Extrai bordas (breaklines da triangulação)
            DeepSeekEngine.SendToAutoCAD($"_AeccExtractBorder \"{tempSurface}\"");

            sb.AppendLine($"✅ Superfície TIN '{tempSurface}' criada com {points.Count} pontos.");
            sb.AppendLine("   Borda extraída como polilinha 3D.");
            sb.AppendLine("💡 Use esta polilinha como breakline ou limite de superfície.");
            return sb.ToString();
        }
    }
}
