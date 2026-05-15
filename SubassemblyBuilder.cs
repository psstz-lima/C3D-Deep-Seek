using System;
using System.Collections.Generic;
using System.Text;

namespace C3DDeepSeek
{
    /// <summary>
    /// Construtor de Subassemblies personalizadas para montagem de corredores.
    /// Cria subassemblies paramétricas: pista, acostamento, sarjeta, calçada,
    /// talude (corte/aterro), muro de contenção e valeta de drenagem.
    /// </summary>
    public static class SubassemblyBuilder
    {
        public class SubassemblyTemplate
        {
            public string Name { get; set; } = "";
            public string Category { get; set; } = "";
            public string Description { get; set; } = "";
            public string MacroName { get; set; } = "";     // nome do subassembly no Civil 3D
            public Dictionary<string, double> DefaultParams { get; set; } = new Dictionary<string, double>();
        }

        /// <summary>
        /// Lista todos os templates de subassembly disponíveis
        /// </summary>
        public static List<SubassemblyTemplate> GetTemplates()
        {
            return new List<SubassemblyTemplate>
            {
                new SubassemblyTemplate
                {
                    Name = "Pista Simples",
                    Category = "Pavimento",
                    Description = "Faixa de rolamento com inclinação transversal",
                    MacroName = "LaneSuperelevationAOR",
                    DefaultParams = new Dictionary<string, double> { ["Largura"] = 3.5, ["Inclinacao"] = 2.0 }
                },
                new SubassemblyTemplate
                {
                    Name = "Acostamento",
                    Category = "Pavimento",
                    Description = "Acostamento em Concreto ou Solo",
                    MacroName = "ShoulderExtendAll",
                    DefaultParams = new Dictionary<string, double> { ["Largura"] = 2.5, ["Inclinacao"] = 4.0 }
                },
                new SubassemblyTemplate
                {
                    Name = "Sarjeta (Meio-Fio + Sarjeta)",
                    Category = "Drenagem",
                    Description = "Sarjeta com meio-fio tipo DNIT",
                    MacroName = "UrbanCurbGutterGeneral",
                    DefaultParams = new Dictionary<string, double> { ["AlturaMeioFio"] = 0.15, ["LarguraSarjeta"] = 0.60 }
                },
                new SubassemblyTemplate
                {
                    Name = "Calçada",
                    Category = "Urbanização",
                    Description = "Calçada/passeio com inclinação",
                    MacroName = "UrbanSidewalk",
                    DefaultParams = new Dictionary<string, double> { ["Largura"] = 2.0, ["Inclinacao"] = 2.0, ["Espessura"] = 0.10 }
                },
                new SubassemblyTemplate
                {
                    Name = "Talude de Corte",
                    Category = "Terraplenagem",
                    Description = "Talude de corte com valeta opcional",
                    MacroName = "DaylightCut",
                    DefaultParams = new Dictionary<string, double> { ["Inclinacao"] = 1.0 } // 1:1 (V:H)
                },
                new SubassemblyTemplate
                {
                    Name = "Talude de Aterro",
                    Category = "Terraplenagem",
                    Description = "Talude de aterro com berma opcional",
                    MacroName = "DaylightFill",
                    DefaultParams = new Dictionary<string, double> { ["Inclinacao"] = 1.5 } // 1.5:1
                },
                new SubassemblyTemplate
                {
                    Name = "Valeta de Drenagem",
                    Category = "Drenagem",
                    Description = "Valeta trapezoidal para drenagem",
                    MacroName = "Ditch",
                    DefaultParams = new Dictionary<string, double> { ["Profundidade"] = 0.50, ["LarguraFundo"] = 0.40, ["Inclinacao"] = 2.0 }
                },
                new SubassemblyTemplate
                {
                    Name = "Muro de Contenção",
                    Category = "Estruturas",
                    Description = "Muro de contenção em concreto armado",
                    MacroName = "RetainingWall",
                    DefaultParams = new Dictionary<string, double> { ["Altura"] = 3.0, ["EspessuraBase"] = 0.80, ["EspessuraTopo"] = 0.30 }
                },
                new SubassemblyTemplate
                {
                    Name = "Barreira New Jersey",
                    Category = "Segurança",
                    Description = "Barreira de concreto tipo New Jersey",
                    MacroName = "JerseyBarrier",
                    DefaultParams = new Dictionary<string, double> { ["Altura"] = 0.81 }
                },
                new SubassemblyTemplate
                {
                    Name = "Ciclovia",
                    Category = "Mobilidade",
                    Description = "Ciclovia bidirecional",
                    MacroName = "LaneSuperelevationAOR",
                    DefaultParams = new Dictionary<string, double> { ["Largura"] = 2.5, ["Inclinacao"] = 2.0 }
                }
            };
        }

        /// <summary>
        /// Gera o comando para criar um subassembly específico
        /// </summary>
        public static string GetCreateCommand(string templateName)
        {
            var templates = GetTemplates();
            var template = templates.Find(t => t.Name.Equals(templateName, StringComparison.OrdinalIgnoreCase));

            if (template == null)
                return null;

            var sb = new StringBuilder();

            // Comando para abrir o Tool Palette de subassemblies
            sb.AppendLine($"_.ToolPalettes");
            sb.AppendLine($"; Categoria: {template.Category}");

            // Comando para criar o subassembly via .NET API ou comando nativo
            sb.AppendLine($"_AeccCreateSubassembly \"{template.MacroName}\"");

            return sb.ToString();
        }

        /// <summary>
        /// Cria uma montagem completa de seção tipo (rodovia padrão DNIT)
        /// </summary>
        public static string BuildHighwayAssembly(string assemblyName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🏗️ MONTAGEM RODOVIÁRIA: {assemblyName}");
            sb.AppendLine("═══════════════════════════════════════");

            sb.AppendLine("\n📋 ESTRUTURA DA SEÇÃO TIPO:");
            sb.AppendLine("───────────────────────────────────────");
            sb.AppendLine("  ← Esquerda          EIXO          Direita →");
            sb.AppendLine("───────────────────────────────────────");
            sb.AppendLine("  Talude     Valeta  Acost.  Pista | Pista  Acost.  Valeta    Talude");
            sb.AppendLine("  1.5:1      0.5m    2.5m   3.5m  | 3.5m   2.5m    0.5m     1.5:1");
            sb.AppendLine("───────────────────────────────────────");

            // Sequência de comandos para criar a montagem
            sb.AppendLine("\n🔧 COMANDOS PARA EXECUÇÃO:");
            sb.AppendLine($"1. _AeccCreateAssembly \"{assemblyName}\"");
            sb.AppendLine("2. Adicione os subassemblies na ordem:");
            sb.AppendLine("   ← Lado Esquerdo:");
            sb.AppendLine("     • DaylightCut (Talude Corte 1:1)");
            sb.AppendLine("     • Ditch (Valeta Prof=0.5m)");
            sb.AppendLine("     • ShoulderExtendAll (Acostamento 2.5m)");
            sb.AppendLine("     • LaneSuperelevationAOR (Pista 3.5m @ 2%)");
            sb.AppendLine("   → Lado Direito:");
            sb.AppendLine("     • LaneSuperelevationAOR (Pista 3.5m @ 2%)");
            sb.AppendLine("     • ShoulderExtendAll (Acostamento 2.5m)");
            sb.AppendLine("     • Ditch (Valeta Prof=0.5m)");
            sb.AppendLine("     • DaylightFill (Talude Aterro 1.5:1)");

            sb.AppendLine("\n💡 Use o comando DSASSEMBLY para criar interativamente.");

            return sb.ToString();
        }

        /// <summary>
        /// Cria uma montagem urbana (avenida com passeio)
        /// </summary>
        public static string BuildUrbanAssembly(string assemblyName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🏙️ MONTAGEM URBANA: {assemblyName}");
            sb.AppendLine("═══════════════════════════════════════");

            sb.AppendLine("\n📋 ESTRUTURA:");
            sb.AppendLine("───────────────────────────────────────");
            sb.AppendLine("  Calçada  Canteiro  Pista | Pista  Canteiro  Calçada");
            sb.AppendLine("   2.0m     1.5m    3.5m  | 3.5m    1.5m     2.0m");
            sb.AppendLine("───────────────────────────────────────");

            sb.AppendLine("\n🔧 COMANDOS:");
            sb.AppendLine($"1. _AeccCreateAssembly \"{assemblyName}\"");
            sb.AppendLine("2. Subassemblies:");
            sb.AppendLine("   • UrbanSidewalk (Calçada 2.0m)");
            sb.AppendLine("   • UrbanCurbGutterGeneral (Meio-fio)");
            sb.AppendLine("   • LaneSuperelevationAOR (Pista 3.5m)");
            sb.AppendLine("   • LaneSuperelevationAOR (Pista 3.5m)");
            sb.AppendLine("   • UrbanCurbGutterGeneral (Meio-fio)");
            sb.AppendLine("   • UrbanSidewalk (Calçada 2.0m)");

            return sb.ToString();
        }

        /// <summary>
        /// Lista todos os subassemblies com suas propriedades
        /// </summary>
        public static string ListAllTemplates()
        {
            var sb = new StringBuilder();
            sb.AppendLine("🧩 CATÁLOGO DE SUBASSEMBLIES");
            sb.AppendLine("═══════════════════════════════════════");

            var templates = GetTemplates();
            string currentCategory = "";

            foreach (var t in templates)
            {
                if (t.Category != currentCategory)
                {
                    currentCategory = t.Category;
                    sb.AppendLine($"\n📂 {currentCategory}:");
                    sb.AppendLine("───────────────────────────────────────");
                }

                sb.AppendLine($"  ▸ {t.Name}");
                sb.AppendLine($"    {t.Description}");
                sb.Append($"    Parâmetros: ");
                foreach (var kv in t.DefaultParams)
                    sb.Append($"{kv.Key}={kv.Value} ");
                sb.AppendLine();
            }

            sb.AppendLine("\n═══════════════════════════════════════");
            sb.AppendLine("💡 Use DSASSEMBLY para criar uma montagem completa.");

            return sb.ToString();
        }
    }
}
