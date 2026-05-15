using System;
using System.IO;
using System.Text;

namespace C3DDeepSeek
{
    /// <summary>
    /// Gerador de seções transversais a partir de Sample Lines.
    /// Suporte a DWT, múltiplos formatos, escalas e estilos.
    /// </summary>
    public static class SectionGenerator
    {
        public class SectionConfig
        {
            public string AlignmentName { get; set; } = "";
            public string SampleLineGroup { get; set; } = "";
            public double HorizontalScale { get; set; } = 500;
            public double VerticalScale { get; set; } = 500;
            public string Format { get; set; } = "A1";
            public string TemplatePath { get; set; } = "";
            public int SectionsPerColumn { get; set; } = 5;
            public int ColumnsPerSheet { get; set; } = 2;
            public bool IncludeVolumeTable { get; set; } = true;
            public bool IncludeOffsetElevation { get; set; } = true;
        }

        /// <summary>
        /// Gera seções a partir de Sample Lines existentes
        /// </summary>
        public static string GenerateFromSampleLines(SectionConfig config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  📐 GERAÇÃO DE SEÇÕES TRANSVERSAIS");
            sb.AppendLine("═══════════════════════════════════════");

            try
            {
                // DWT
                if (!string.IsNullOrWhiteSpace(config.TemplatePath) && File.Exists(config.TemplatePath))
                {
                    sb.AppendLine($"📄 Template: {config.TemplatePath}");
                    DeepSeekEngine.SendToAutoCAD(
                        $"(command \"_.LAYOUT\" \"_T\" \"{config.TemplatePath.Replace("\\", "/")}\")");
                }
                else
                {
                    sb.AppendLine("📄 Template: Padrão");
                }

                sb.AppendLine($"🛤️ Alinhamento: {config.AlignmentName}");
                sb.AppendLine($"📏 Escala: H=1:{config.HorizontalScale} V=1:{config.VerticalScale}");
                sb.AppendLine($"📋 Formato: {config.Format} | {config.SectionsPerColumn} seções/coluna | {config.ColumnsPerSheet} colunas");

                // ── PASSO 1: Criar Sample Lines (se não existirem) ──
                if (string.IsNullOrWhiteSpace(config.SampleLineGroup))
                {
                    DeepSeekEngine.SendToAutoCAD(
                        $"_AeccCreateSampleLines \"{config.AlignmentName}\"");
                    sb.AppendLine("✅ Sample Lines criadas.");
                }

                // ── PASSO 2: Criar Multiple Section Views ──
                DeepSeekEngine.SendToAutoCAD(
                    $"_AeccCreateMultipleSectionViews " +
                    $"\"{config.AlignmentName}\" " +
                    $"{config.HorizontalScale} {config.VerticalScale}");

                sb.AppendLine("✅ Múltiplas seções criadas.");

                // ── PASSO 3: Diagrama de volumes ──
                if (config.IncludeVolumeTable)
                {
                    DeepSeekEngine.SendToAutoCAD(
                        $"_AeccAddMaterialSection \"{config.AlignmentName}\"");
                    sb.AppendLine("✅ Diagrama de corte/aterro adicionado.");
                }

                // ── PASSO 4: Páginas ──
                DeepSeekEngine.SendToAutoCAD(
                    $"_AeccCreateSectionSheets \"{config.AlignmentName}\" " +
                    $"\"{config.Format}\" " +
                    $"{config.HorizontalScale} {config.VerticalScale}");

                sb.AppendLine("✅ Folhas de seção geradas.");

                // ── PASSO 5: Ajusta layout ──
                DeepSeekEngine.SendToAutoCAD(
                    $"(command \"_.PAGESETUP\" \"{config.Format}\")");

                sb.AppendLine("\n───────────────────────────────────────");
                sb.AppendLine($"✅ SEÇÕES GERADAS COM SUCESSO!");
                sb.AppendLine($"   Alinhamento: {config.AlignmentName}");
                sb.AppendLine($"   Escala: H=1:{config.HorizontalScale} V=1:{config.VerticalScale}");
                sb.AppendLine($"   Formato: {config.Format}");
                sb.AppendLine("💡 As seções estão nos Layouts criados.");

            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Erro: {ex.Message}");
            }

            sb.AppendLine("═══════════════════════════════════════");
            return sb.ToString();
        }

        /// <summary>
        /// Cria Sample Lines automaticamente e depois gera seções
        /// </summary>
        public static string FullSectionWorkflow(SectionConfig config)
        {
            var sb = new StringBuilder();

            // Cria sample lines com intervalo padrão
            sb.AppendLine("📏 Criando Sample Lines...");
            DeepSeekEngine.SendToAutoCAD(
                $"_AeccCreateSampleLines \"{config.AlignmentName}\" " +
                $"{config.HorizontalScale}");

            // Pequeno delay visual
            System.Threading.Thread.Sleep(200);

            // Gera as seções
            sb.AppendLine(GenerateFromSampleLines(config));

            return sb.ToString();
        }
    }
}
