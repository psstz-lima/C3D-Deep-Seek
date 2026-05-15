using System;
using System.Text;

namespace C3DDeepSeek
{
    /// <summary>
    /// Rotulador inteligente — labels, cotas, tabelas e estacas automáticas.
    /// </summary>
    public static class SmartLabeler
    {
        /// <summary>
        /// Rotula todas as estacas de um alinhamento
        /// </summary>
        public static string LabelAlignmentStations(string alignmentName, double interval = 20)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🏷️ ROTULANDO ALINHAMENTO: {alignmentName}");
            sb.AppendLine($"   Intervalo de estacas: {interval}m");

            DeepSeekEngine.SendToAutoCAD(
                $"_AeccAddAlignmentLabels \"{alignmentName}\" \"Station\" {interval}");

            sb.AppendLine("✅ Estacas rotuladas.");
            return sb.ToString();
        }

        /// <summary>
        /// Rotula curvas de nível com elevação
        /// </summary>
        public static string LabelContours(string surfaceName, double majorInterval = 5, double minorInterval = 1)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🏷️ ROTULANDO CURVAS: {surfaceName}");
            sb.AppendLine($"   Mestras: {majorInterval}m | Intermediárias: {minorInterval}m");

            DeepSeekEngine.SendToAutoCAD(
                $"_AeccAddSurfaceContourLabels \"{surfaceName}\" {majorInterval}");

            sb.AppendLine("✅ Curvas de nível rotuladas.");
            return sb.ToString();
        }

        /// <summary>
        /// Gera tabela de curvas horizontais
        /// </summary>
        public static string GenerateCurveTable(string alignmentName)
        {
            DeepSeekEngine.SendToAutoCAD(
                $"_AeccAddAlignmentTable \"{alignmentName}\" \"Curve\"");

            return $"✅ Tabela de curvas do alinhamento '{alignmentName}' gerada.";
        }

        /// <summary>
        /// Gera tabela de PIVs (greide)
        /// </summary>
        public static string GeneratePIVTable(string alignmentName)
        {
            DeepSeekEngine.SendToAutoCAD(
                $"_AeccAddProfileTable \"{alignmentName}\"");

            return $"✅ Tabela de PIVs do alinhamento '{alignmentName}' gerada.";
        }

        /// <summary>
        /// Rotula taludes com declividade
        /// </summary>
        public static string LabelSlopes()
        {
            DeepSeekEngine.SendToAutoCAD(
                "_AeccAddSlopeLabel");

            return "✅ Ferramenta de label de talude ativada — selecione os taludes.";
        }

        /// <summary>
        /// Rotula tubulações com diâmetro e material
        /// </summary>
        public static string LabelPipes(string networkName)
        {
            DeepSeekEngine.SendToAutoCAD(
                $"_AeccAddPipeLabel \"{networkName}\"");

            return $"✅ Labels de tubulação da rede '{networkName}' gerados.";
        }

        /// <summary>
        /// Rotula estruturas (bocas de lobo, PVs)
        /// </summary>
        public static string LabelStructures(string networkName)
        {
            DeepSeekEngine.SendToAutoCAD(
                $"_AeccAddStructureLabel \"{networkName}\"");

            return $"✅ Labels de estruturas da rede '{networkName}' gerados.";
        }

        /// <summary>
        /// Comando rápido: rotula tudo automaticamente
        /// </summary>
        public static string LabelAll(string alignmentName, string surfaceName, string pipeNetworkName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  🏷️ ROTULAGEM AUTOMÁTICA COMPLETA");
            sb.AppendLine("═══════════════════════════════════════");

            if (!string.IsNullOrWhiteSpace(alignmentName))
            {
                sb.AppendLine($"\n{LabelAlignmentStations(alignmentName)}");
                sb.AppendLine(GenerateCurveTable(alignmentName));
            }

            if (!string.IsNullOrWhiteSpace(surfaceName))
            {
                sb.AppendLine($"\n{LabelContours(surfaceName)}");
            }

            if (!string.IsNullOrWhiteSpace(pipeNetworkName))
            {
                sb.AppendLine($"\n{LabelPipes(pipeNetworkName)}");
                sb.AppendLine(LabelStructures(pipeNetworkName));
            }

            sb.AppendLine("\n═══════════════════════════════════════");
            sb.AppendLine("✅ Rotulagem completa concluída!");
            return sb.ToString();
        }
    }
}
