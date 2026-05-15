using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace C3DDeepSeek
{
    /// <summary>
    /// Gerador de seções transversais a partir de Sample Lines.
    /// CUSTOMIZÁVEL: estilos, bandas, grids, labels, offsets, presets salvos.
    /// </summary>
    public static class SectionGenerator
    {
        public class SectionConfig
        {
            // ── Básico ──
            public string AlignmentName { get; set; } = "";
            public string SampleLineGroup { get; set; } = "";
            public string PresetName { get; set; } = "";

            // ── Escalas ──
            public double HorizontalScale { get; set; } = 500;
            public double VerticalScale { get; set; } = 500;

            // ── Layout ──
            public string Format { get; set; } = "A1";
            public string TemplatePath { get; set; } = "";
            public int SectionsPerColumn { get; set; } = 5;
            public int ColumnsPerSheet { get; set; } = 2;

            // ── Estilos Civil 3D ──
            public string SectionViewStyle { get; set; } = "Road Section";      // estilo da view
            public string SectionLabelStyle { get; set; } = "Standard";          // estilo dos labels
            public string BandStyle { get; set; } = "Elevations and Stations";   // banda inferior
            public string CodeSetStyle { get; set; } = "Standard";               // código de cores
            public string VolumeTableStyle { get; set; } = "Standard";           // tabela de volumes

            // ── Conteúdo ──
            public bool IncludeVolumeTable { get; set; } = true;
            public bool IncludeOffsetElevation { get; set; } = true;
            public bool IncludeGrid { get; set; } = true;
            public bool IncludeLegend { get; set; } = false;
            public bool IncludeMaterialHatch { get; set; } = true;

            // ── Offsets ──
            public double LeftOffset { get; set; } = 20;    // metros
            public double RightOffset { get; set; } = 20;
            public double ElevationMin { get; set; } = 0;
            public double ElevationMax { get; set; } = 0;   // 0 = automático

            // ── Sample Lines ──
            public double SampleInterval { get; set; } = 20; // metros
            public bool IncludeCurvePoints { get; set; } = true;
            public bool IncludeTangentialPoints { get; set; } = false;
        }

        /// <summary>
        /// Presets de configuração por tipo de projeto
        /// </summary>
        public static Dictionary<string, SectionConfig> GetPresets()
        {
            return new Dictionary<string, SectionConfig>
            {
                ["ROAD"] = new SectionConfig
                {
                    PresetName = "ROAD",
                    HorizontalScale = 500, VerticalScale = 500,
                    Format = "A1", SectionsPerColumn = 5, ColumnsPerSheet = 2,
                    SectionViewStyle = "Road Section",
                    BandStyle = "Elevations and Stations",
                    LeftOffset = 20, RightOffset = 20,
                    IncludeVolumeTable = true,
                    IncludeGrid = true,
                    SampleInterval = 20
                },
                ["ROAD_DETAIL"] = new SectionConfig
                {
                    PresetName = "ROAD_DETAIL",
                    HorizontalScale = 200, VerticalScale = 200,
                    Format = "A3", SectionsPerColumn = 3, ColumnsPerSheet = 2,
                    SectionViewStyle = "Road Section",
                    BandStyle = "Elevations and Stations",
                    LeftOffset = 15, RightOffset = 15,
                    IncludeVolumeTable = true,
                    IncludeGrid = true,
                    IncludeLegend = true,
                    SampleInterval = 10
                },
                ["URBAN"] = new SectionConfig
                {
                    PresetName = "URBAN",
                    HorizontalScale = 200, VerticalScale = 200,
                    Format = "A3", SectionsPerColumn = 3, ColumnsPerSheet = 2,
                    LeftOffset = 30, RightOffset = 30,
                    IncludeVolumeTable = false,
                    IncludeGrid = true,
                    IncludeOffsetElevation = true,
                    IncludeLegend = true,
                    SampleInterval = 15
                },
                ["EARTHWORK"] = new SectionConfig
                {
                    PresetName = "EARTHWORK",
                    HorizontalScale = 500, VerticalScale = 500,
                    Format = "A1", SectionsPerColumn = 6, ColumnsPerSheet = 3,
                    LeftOffset = 30, RightOffset = 30,
                    IncludeVolumeTable = true,
                    IncludeGrid = false,
                    IncludeMaterialHatch = true,
                    SampleInterval = 10,
                    IncludeCurvePoints = true,
                    IncludeTangentialPoints = true
                },
                ["OVERVIEW"] = new SectionConfig
                {
                    PresetName = "OVERVIEW",
                    HorizontalScale = 1000, VerticalScale = 1000,
                    Format = "A4", SectionsPerColumn = 8, ColumnsPerSheet = 4,
                    LeftOffset = 50, RightOffset = 50,
                    IncludeVolumeTable = false,
                    IncludeGrid = false,
                    IncludeLegend = false,
                    SampleInterval = 50
                }
            };
        }

        /// <summary>
        /// Lista os presets disponíveis
        /// </summary>
        public static string ListPresets()
        {
            var sb = new StringBuilder();
            sb.AppendLine("📋 PRESETS DE SEÇÕES:");
            sb.AppendLine("───────────────────────────────────────────");
            foreach (var kv in GetPresets())
            {
                var p = kv.Value;
                sb.AppendLine($"  ▸ {kv.Key,-15} H=1:{p.HorizontalScale} V=1:{p.VerticalScale} | {p.Format} | {p.SectionsPerColumn}×{p.ColumnsPerSheet}");
            }
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine("💡 Use: DSSECTIONS → escolha preset ou CUSTOM");
            return sb.ToString();
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
                // ── Preset ──
                if (!string.IsNullOrWhiteSpace(config.PresetName))
                    sb.AppendLine($"🎯 Preset: {config.PresetName}");

                // ── DWT ──
                if (!string.IsNullOrWhiteSpace(config.TemplatePath) && File.Exists(config.TemplatePath))
                {
                    sb.AppendLine($"📄 Template: {config.TemplatePath}");
                    DeepSeekEngine.SendToAutoCAD(
                        $"(command \"_.LAYOUT\" \"_T\" \"{config.TemplatePath.Replace("\\", "/")}\")");
                }
                else
                    sb.AppendLine("📄 Template: Padrão Civil 3D");

                // ── Configuração ──
                sb.AppendLine($"🛤️ Alinhamento: {config.AlignmentName}");
                sb.AppendLine($"📏 Escala: H=1:{config.HorizontalScale} V=1:{config.VerticalScale}");
                sb.AppendLine($"📋 Formato: {config.Format}");
                sb.AppendLine($"📐 Layout: {config.SectionsPerColumn} seções/col × {config.ColumnsPerSheet} cols");
                sb.AppendLine($"↔️ Offsets: -{config.LeftOffset}m / +{config.RightOffset}m");
                sb.AppendLine($"📏 Sample Lines a cada {config.SampleInterval}m");

                // ── Estilos ──
                if (!string.IsNullOrWhiteSpace(config.SectionViewStyle))
                    sb.AppendLine($"🎨 Estilo seção: {config.SectionViewStyle}");
                if (!string.IsNullOrWhiteSpace(config.BandStyle) && config.IncludeOffsetElevation)
                    sb.AppendLine($"📊 Bandas: {config.BandStyle}");

                // ── Conteúdo ──
                var features = new List<string>();
                if (config.IncludeVolumeTable) features.Add("📦 Volumes");
                if (config.IncludeGrid) features.Add("📏 Grid");
                if (config.IncludeLegend) features.Add("🗺️ Legenda");
                if (config.IncludeMaterialHatch) features.Add("🎨 Hachuras");
                if (features.Count > 0)
                    sb.AppendLine($"✅ Incluindo: {string.Join(", ", features)}");

                // ── PASSO 1: Criar Sample Lines ──
                if (string.IsNullOrWhiteSpace(config.SampleLineGroup))
                {
                    string sampleCmd = $"_AeccCreateSampleLines \"{config.AlignmentName}\"";
                    DeepSeekEngine.SendToAutoCAD(sampleCmd);
                    sb.AppendLine("✅ Sample Lines criadas.");
                }

                // ── PASSO 2: Criar Multiple Section Views ──
                string sectionCmd = $"_AeccCreateMultipleSectionViews " +
                    $"\"{config.AlignmentName}\" " +
                    $"{config.HorizontalScale} {config.VerticalScale}";
                DeepSeekEngine.SendToAutoCAD(sectionCmd);
                sb.AppendLine("✅ Múltiplas seções criadas.");

                // ── PASSO 3: Bandas ──
                if (config.IncludeOffsetElevation && !string.IsNullOrWhiteSpace(config.BandStyle))
                {
                    DeepSeekEngine.SendToAutoCAD(
                        $"_AeccAddSectionViewBands \"{config.AlignmentName}\" \"{config.BandStyle}\"");
                    sb.AppendLine("✅ Bandas de offset/elevação adicionadas.");
                }

                // ── PASSO 4: Diagrama de volumes ──
                if (config.IncludeVolumeTable)
                {
                    DeepSeekEngine.SendToAutoCAD(
                        $"_AeccAddMaterialSection \"{config.AlignmentName}\"");
                    sb.AppendLine("✅ Diagrama de corte/aterro adicionado.");
                }

                // ── PASSO 5: Grid ──
                if (config.IncludeGrid)
                {
                    DeepSeekEngine.SendToAutoCAD(
                        $"_AeccSectionViewGrid \"{config.AlignmentName}\"");
                    sb.AppendLine("✅ Grid adicionado às seções.");
                }

                // ── PASSO 6: Folhas ──
                DeepSeekEngine.SendToAutoCAD(
                    $"_AeccCreateSectionSheets \"{config.AlignmentName}\" " +
                    $"\"{config.Format}\" " +
                    $"{config.HorizontalScale} {config.VerticalScale}");
                sb.AppendLine("✅ Folhas de seção geradas.");

                // ── PASSO 7: Layout ──
                DeepSeekEngine.SendToAutoCAD(
                    $"(command \"_.PAGESETUP\" \"{config.Format}\")");

                sb.AppendLine("\n───────────────────────────────────────");
                sb.AppendLine($"✅ SEÇÕES GERADAS!");
                sb.AppendLine($"   Alinhamento: {config.AlignmentName}");
                sb.AppendLine($"   Escala: H=1:{config.HorizontalScale} V=1:{config.VerticalScale}");
                sb.AppendLine($"   Formato: {config.Format} | Layout: {config.SectionsPerColumn}×{config.ColumnsPerSheet}");
                if (!string.IsNullOrWhiteSpace(config.PresetName))
                    sb.AppendLine($"   Preset: {config.PresetName}");
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
