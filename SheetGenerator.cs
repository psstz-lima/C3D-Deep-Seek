using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace C3DDeepSeek
{
    /// <summary>
    /// Gerador de folhas técnicas — Planta, Perfil, Seções.
    /// Formatos: A0, A1, A2, A3, A4 e personalizado.
    /// Suporte a templates DWT para carimbo e configurações.
    /// </summary>
    public static class SheetGenerator
    {
        public class SheetConfig
        {
            public string Format { get; set; } = "A1";
            public double Width { get; set; }   // mm
            public double Height { get; set; }  // mm
            public string TemplatePath { get; set; } = "";
            public double Scale { get; set; } = 1000;
            public string AlignmentName { get; set; } = "";
            public bool IncludeProfile { get; set; } = true;
            public bool IncludeSections { get; set; } = false;
            public double StationStart { get; set; }
            public double StationEnd { get; set; }
        }

        private static readonly Dictionary<string, (double w, double h)> Formats = new Dictionary<string, (double, double)>
        {
            ["A0"] = (1189, 841),
            ["A1"] = (841, 594),
            ["A2"] = (594, 420),
            ["A3"] = (420, 297),
            ["A4"] = (297, 210),
        };

        /// <summary>
        /// Gera folhas de Planta e Perfil para um alinhamento
        /// </summary>
        public static string GeneratePlanProfileSheets(SheetConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  📐 GERAÇÃO DE FOLHAS — PLANTA & PERFIL");
            sb.AppendLine("═══════════════════════════════════════");

            // Determina dimensões
            double width, height;
            if (Formats.ContainsKey(config.Format.ToUpper()))
            {
                (width, height) = Formats[config.Format.ToUpper()];
                sb.AppendLine($"Formato: {config.Format.ToUpper()} ({width}×{height}mm)");
            }
            else if (config.Width > 0 && config.Height > 0)
            {
                width = config.Width;
                height = config.Height;
                sb.AppendLine($"Formato: Personalizado ({width}×{height}mm)");
            }
            else
            {
                (width, height) = Formats["A1"];
                sb.AppendLine($"Formato: A1 (default)");
            }

            // Template DWT
            if (!string.IsNullOrWhiteSpace(config.TemplatePath) && File.Exists(config.TemplatePath))
            {
                sb.AppendLine($"Template: {config.TemplatePath}");
                DeepSeekEngine.SendToAutoCAD($"(command \"_.LAYOUT\" \"_T\" \"{config.TemplatePath.Replace("\\", "/")}\")");
            }
            else
            {
                sb.AppendLine("Template: Padrão (sem DWT)");
            }

            sb.AppendLine($"Escala: 1:{config.Scale}");
            sb.AppendLine($"Alinhamento: {(string.IsNullOrWhiteSpace(config.AlignmentName) ? "Principal" : config.AlignmentName)}");

            // Comandos
            sb.AppendLine("\n🔧 EXECUTANDO...");

            if (!string.IsNullOrWhiteSpace(config.AlignmentName))
            {
                // Cria viewport de planta
                DeepSeekEngine.SendToAutoCAD(
                    $"(command \"_AeccCreatePlanProductionSheets\" \"{config.AlignmentName}\" \"{config.Scale}\")");
            }

            if (config.IncludeProfile)
            {
                DeepSeekEngine.SendToAutoCAD(
                    $"(command \"_AeccCreateProfileView\" \"{config.Scale * 10}\" \"{config.Scale}\")");
            }

            if (config.IncludeSections)
            {
                DeepSeekEngine.SendToAutoCAD(
                    $"(command \"_AeccCreateMultipleSectionViews\" \"{config.Scale}\" \"{config.Scale}\")");
            }

            // Ajusta layout
            DeepSeekEngine.SendToAutoCAD(
                $"(command \"_.PAGESETUP\" \"{config.Format.ToUpper()}\")");

            sb.AppendLine("\n✅ Folhas geradas com sucesso!");
            sb.AppendLine($"   Alinhamento: {config.AlignmentName}");
            sb.AppendLine($"   Formato: {config.Format.ToUpper()} | Escala: 1:{config.Scale}");
            sb.AppendLine("═══════════════════════════════════════");

            return sb.ToString();
        }

        /// <summary>
        /// Lista formatos disponíveis
        /// </summary>
        public static string ListFormats()
        {
            var sb = new StringBuilder();
            sb.AppendLine("📐 FORMATOS DE FOLHA DISPONÍVEIS");
            sb.AppendLine("──────────────────────────────────");
            foreach (var kv in Formats)
                sb.AppendLine($"  {kv.Key}: {kv.Value.w}×{kv.Value.h} mm");
            sb.AppendLine("  PERSONALIZADO: defina largura e altura");
            return sb.ToString();
        }

        /// <summary>
        /// Gera folhas de seções transversais
        /// </summary>
        public static string GenerateSectionSheets(string alignmentName, double scale = 500, string format = "A1")
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  📐 GERAÇÃO DE FOLHAS DE SEÇÕES");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"Alinhamento: {alignmentName}");
            sb.AppendLine($"Escala: 1:{scale} | Formato: {format}");

            // Cria seções em folha
            DeepSeekEngine.SendToAutoCAD(
                $"_AeccCreateMultipleSectionViews \"{alignmentName}\" {scale} {scale}");

            // Aplica formato de folha
            DeepSeekEngine.SendToAutoCAD($"(command \"_.PAGESETUP\" \"{format}\")");

            sb.AppendLine("✅ Folhas de seções geradas.");
            return sb.ToString();
        }
    }
}
