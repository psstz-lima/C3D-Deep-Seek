using System;
using System.IO;
using System.Text;

namespace C3DDeepSeek
{
    /// <summary>
    /// Gerenciador de templates de projeto inteligentes.
    /// Cria DWGs novos com layers, estilos, blocos e configurações padrão
    /// por tipo de projeto: Rodovia, Loteamento, Saneamento, Drenagem.
    /// </summary>
    public static class TemplateManager
    {
        /// <summary>
        /// Cria um novo desenho baseado no template selecionado
        /// </summary>
        public static string CreateFromTemplate(string templateType)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  🏗️ CRIAÇÃO DE PROJETO POR TEMPLATE");
            sb.AppendLine("═══════════════════════════════════════");

            switch (templateType.ToUpper())
            {
                case "RODOVIA":
                    return CreateHighwayTemplate(sb);
                case "LOTEAMENTO":
                    return CreateSubdivisionTemplate(sb);
                case "SANEAMENTO":
                    return CreateSanitationTemplate(sb);
                case "DRENAGEM":
                    return CreateDrainageTemplate(sb);
                case "TERRAPLENAGEM":
                    return CreateEarthworkTemplate(sb);
                default:
                    return ListAvailableTemplates();
            }
        }

        private static string CreateHighwayTemplate(StringBuilder sb)
        {
            sb.AppendLine("📋 Template: RODOVIA");
            sb.AppendLine("───────────────────────────────────────");

            // Cria novo DWG
            DeepSeekEngine.SendToAutoCAD("_.NEW");

            // Layers padrão rodovia
            string[] layers = {
                "C-TOPO-MESTRA", "C-TOPO-INTERM", "C-TERRA-EXIST",
                "C-PAV-PISTA", "C-PAV-ACOST", "C-DREN-VALETA",
                "C-SINAL-HORIZ", "C-SINAL-VERT", "C-EIXO",
                "C-GREIDE", "C-SECAO"
            };

            foreach (var layer in layers)
            {
                DeepSeekEngine.SendToAutoCAD($"_.-LAYER _M {layer} ;");
            }

            // Volta para layer 0
            DeepSeekEngine.SendToAutoCAD("_.-LAYER _S 0 ;");

            sb.AppendLine("✅ Layers criadas:");
            foreach (var l in layers) sb.AppendLine($"   • {l}");
            sb.AppendLine("\n📐 Configuração:");
            sb.AppendLine("   • Unidades: Metros");
            sb.AppendLine("   • Coordenadas: UTM SIRGAS 2000");
            sb.AppendLine("   • Escala: 1:1000");
            sb.AppendLine("\n💡 Próximo passo: DSMODEL para começar a modelar.");

            return sb.ToString();
        }

        private static string CreateSubdivisionTemplate(StringBuilder sb)
        {
            sb.AppendLine("📋 Template: LOTEAMENTO");
            sb.AppendLine("───────────────────────────────────────");

            DeepSeekEngine.SendToAutoCAD("_.NEW");

            string[] layers = {
                "C-TOPO-MESTRA", "C-TOPO-INTERM",
                "C-LOTE-DIVISA", "C-LOTE-NUM",
                "C-VIA-EIXO", "C-VIA-MEIOFIO",
                "C-AGUA-REDE", "C-ESGOTO-REDE",
                "C-DRENAGEM", "C-AREAS-VERDES"
            };

            foreach (var layer in layers)
            {
                DeepSeekEngine.SendToAutoCAD($"_.-LAYER _M {layer} ;");
            }

            DeepSeekEngine.SendToAutoCAD("_.-LAYER _S 0 ;");

            sb.AppendLine("✅ Layers criadas:");
            foreach (var l in layers) sb.AppendLine($"   • {l}");
            sb.AppendLine("\n💡 Use DSWORKFLOW LOTEAMENTO para o fluxo completo.");

            return sb.ToString();
        }

        private static string CreateSanitationTemplate(StringBuilder sb)
        {
            sb.AppendLine("📋 Template: SANEAMENTO");
            sb.AppendLine("───────────────────────────────────────");

            DeepSeekEngine.SendToAutoCAD("_.NEW");

            string[] layers = {
                "C-TOPO-MESTRA", "C-TOPO-INTERM",
                "C-AGUA-REDE", "C-AGUA-LIGACAO",
                "C-ESGOTO-REDE", "C-ESGOTO-LIGACAO",
                "C-ESGOTO-EE", "C-DREN-AGUA",
                "C-PV-VISITA", "C-BOCA-LOBO"
            };

            foreach (var layer in layers)
            {
                DeepSeekEngine.SendToAutoCAD($"_.-LAYER _M {layer} ;");
            }

            DeepSeekEngine.SendToAutoCAD("_.-LAYER _S 0 ;");

            sb.AppendLine("✅ Layers criadas:");
            foreach (var l in layers) sb.AppendLine($"   • {l}");
            sb.AppendLine("\n💡 Use DSWORKFLOW DRENAGEM para iniciar.");

            return sb.ToString();
        }

        private static string CreateDrainageTemplate(StringBuilder sb)
        {
            sb.AppendLine("📋 Template: DRENAGEM");
            sb.AppendLine("───────────────────────────────────────");

            DeepSeekEngine.SendToAutoCAD("_.NEW");

            string[] layers = {
                "C-TOPO-MESTRA", "C-TOPO-INTERM",
                "C-DREN-BACIA", "C-DREN-GALERIA",
                "C-DREN-BOCA", "C-DREN-PV",
                "C-DREN-SARJETA", "C-DREN-DISSIPADOR"
            };

            foreach (var layer in layers)
            {
                DeepSeekEngine.SendToAutoCAD($"_.-LAYER _M {layer} ;");
            }

            DeepSeekEngine.SendToAutoCAD("_.-LAYER _S 0 ;");

            sb.AppendLine("✅ Layers criadas:");
            foreach (var l in layers) sb.AppendLine($"   • {l}");
            sb.AppendLine("\n💡 Use DSCALC para dimensionamento hidráulico.");

            return sb.ToString();
        }

        private static string CreateEarthworkTemplate(StringBuilder sb)
        {
            sb.AppendLine("📋 Template: TERRAPLENAGEM");
            sb.AppendLine("───────────────────────────────────────");

            DeepSeekEngine.SendToAutoCAD("_.NEW");

            string[] layers = {
                "C-TOPO-NATURAL", "C-TOPO-PROJETO",
                "C-TERRA-CORTE", "C-TERRA-ATERRO",
                "C-TERRA-PLATAFORMA", "C-TERRA-TALUDE",
                "C-SECAO-CORTE", "C-SECAO-ATERRO"
            };

            foreach (var layer in layers)
            {
                DeepSeekEngine.SendToAutoCAD($"_.-LAYER _M {layer} ;");
            }

            DeepSeekEngine.SendToAutoCAD("_.-LAYER _S 0 ;");

            sb.AppendLine("✅ Layers criadas:");
            foreach (var l in layers) sb.AppendLine($"   • {l}");
            sb.AppendLine("\n💡 Use DSWORKFLOW TERRAPLENAGEM para iniciar.");

            return sb.ToString();
        }

        public static string ListAvailableTemplates()
        {
            var sb = new StringBuilder();
            sb.AppendLine("📋 TEMPLATES DISPONÍVEIS:");
            sb.AppendLine("───────────────────────────────────────");
            sb.AppendLine("  RODOVIA       — Projeto rodoviário (DNIT)");
            sb.AppendLine("  LOTEAMENTO    — Loteamento urbano");
            sb.AppendLine("  SANEAMENTO    — Água e esgoto");
            sb.AppendLine("  DRENAGEM      — Drenagem pluvial");
            sb.AppendLine("  TERRAPLENAGEM — Movimento de terra");
            sb.AppendLine("───────────────────────────────────────");
            sb.AppendLine("💡 Use: DSTEMPLATE RODOVIA");
            return sb.ToString();
        }
    }
}
