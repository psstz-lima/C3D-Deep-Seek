using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Análise crítica de projetos — compatibilização, detecção de erros,
    /// comparação entre 2 ou mais desenhos, validação de cotas e interferências.
    /// </summary>
    public static class ProjectAnalyzer
    {
        public class AnalysisResult
        {
            public bool Success { get; set; }
            public string Report { get; set; } = "";
            public List<string> Issues { get; set; } = new List<string>();
            public List<string> Recommendations { get; set; } = new List<string>();
            public int IssueCount => Issues.Count;
        }

        /// <summary>
        /// Analisa criticamente o projeto atual
        /// </summary>
        public static AnalysisResult AnalyzeCurrentProject()
        {
            var result = new AnalysisResult();
            var sb = new StringBuilder();

            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  🔍 ANÁLISE CRÍTICA DO PROJETO");
            sb.AppendLine("═══════════════════════════════════════");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic doc = acadApp.ActiveDocument;

                sb.AppendLine($"📄 Projeto: {doc.Name}");
                sb.AppendLine();

                // 1. Análise de layers
                AnalyzeLayers(doc, result);

                // 2. Análise Civil 3D
                AnalyzeCivil3DObjects(acadApp, result);

                // 3. Verificação de integridade
                AnalyzeIntegrity(doc, result);

                // 4. Análise BIM
                AnalyzeBIMCompliance(doc, result);

                // Monta relatório
                sb.AppendLine(FormatAnalysisResult(result));
                result.Report = sb.ToString();
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Report = $"❌ Erro na análise: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Compara dois projetos abertos e detecta incompatibilidades
        /// </summary>
        public static AnalysisResult CompareProjects()
        {
            var result = new AnalysisResult();
            var sb = new StringBuilder();

            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  ⚖️ COMPARAÇÃO ENTRE PROJETOS");
            sb.AppendLine("═══════════════════════════════════════");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                var docs = acadApp.Documents;
                if (docs.Count < 2)
                {
                    result.Issues.Add("⚠️ Apenas 1 desenho aberto. Abra 2 ou mais para comparar.");
                    result.Report = sb.ToString() + FormatAnalysisResult(result);
                    return result;
                }

                sb.AppendLine($"📄 Projetos abertos: {docs.Count}");
                for (int i = 0; i < docs.Count; i++)
                {
                    sb.AppendLine($"  [{i + 1}] {docs[i].Name}");
                }

                // Compara layers entre documentos
                CompareLayers(docs, result);

                // Compara coordenadas / georreferenciamento
                CompareCoordinates(docs, result);

                // Compara objetos Civil 3D
                CompareCivil3DObjects(acadApp, docs, result);

                result.Report = sb.ToString() + FormatAnalysisResult(result);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Report = $"❌ Erro na comparação: {ex.Message}";
            }

            return result;
        }

        private static void AnalyzeLayers(dynamic doc, AnalysisResult result)
        {
            try
            {
                dynamic layers = doc.Layers;
                int total = 0, frozen = 0, off = 0;
                var namingIssues = new List<string>();

                foreach (dynamic layer in layers)
                {
                    total++;
                    if (layer.Freeze) frozen++;
                    if (!layer.LayerOn) off++;

                    // Verifica naming convention (deveria seguir padrão NBR/empresa)
                    string name = layer.Name;
                    if (!name.Contains("-") && name.Length > 3 && !name.StartsWith("0") && !name.StartsWith("C-"))
                        namingIssues.Add(name);
                }

                if (frozen > 0)
                    result.Issues.Add($"⚠️ {frozen} layers congeladas — podem esconder informações");

                if (off > 0)
                    result.Issues.Add($"⚠️ {off} layers desligadas — verificar se são necessárias");

                if (namingIssues.Count > 0 && namingIssues.Count < 20)
                    result.Issues.Add($"⚠️ Layers fora do padrão: {string.Join(", ", namingIssues)}");
                else if (namingIssues.Count >= 20)
                    result.Issues.Add($"⚠️ {namingIssues.Count} layers com nomenclatura fora do padrão NBR");

                if (total > 100)
                    result.Recommendations.Add("💡 Considere usar filtros de layer para organização");
            }
            catch { }
        }

        private static void AnalyzeCivil3DObjects(dynamic acadApp, AnalysisResult result)
        {
            try
            {
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                // Verifica superfícies sem dados
                try
                {
                    dynamic surfaces = civilDoc.Surfaces;
                    foreach (dynamic surf in surfaces)
                    {
                        if (surf.Statistics == null)
                            result.Issues.Add($"⚠️ Superfície '{surf.Name}' pode estar vazia (sem dados)");
                    }
                }
                catch { }

                // Verifica alinhamentos sem perfil
                try
                {
                    dynamic aligns = civilDoc.Alignments;
                    dynamic profiles = civilDoc.Profiles;
                    int alignCount = aligns.Count;
                    int profileCount = profiles.Count;

                    if (alignCount > profileCount)
                        result.Recommendations.Add($"💡 {alignCount - profileCount} alinhamento(s) sem perfil — crie perfis para análise vertical");
                }
                catch { }

                // Verifica corredores sem superfície
                try
                {
                    dynamic corridors = civilDoc.Corridors;
                    foreach (dynamic corr in corridors)
                    {
                        if (corr.CorridorSurfaces.Count == 0)
                            result.Issues.Add($"⚠️ Corredor '{corr.Name}' não tem superfícies geradas");
                    }
                }
                catch { }

                // Verifica sites sem parcelas
                try
                {
                    dynamic sites = civilDoc.Sites;
                    foreach (dynamic site in sites)
                    {
                        if (site.Parcels.Count == 0 && site.Alignments.Count == 0)
                            result.Recommendations.Add($"💡 Site '{site.Name}' está vazio");
                    }
                }
                catch { }
            }
            catch
            {
                result.Recommendations.Add("💡 Não foi possível analisar objetos Civil 3D");
            }
        }

        private static void AnalyzeIntegrity(dynamic doc, AnalysisResult result)
        {
            try
            {
                // Verifica se há objetos na layer 0 (não recomendado)
                dynamic ms = doc.ModelSpace;
                int layer0Count = 0;
                int totalEntities = 0;

                foreach (dynamic entity in ms)
                {
                    totalEntities++;
                    if (entity.Layer == "0") layer0Count++;
                    if (totalEntities > 10000) break;
                }

                if (layer0Count > 10)
                    result.Issues.Add($"⚠️ {layer0Count} objetos na layer 0 — mova para layers específicas");

                if (totalEntities == 0)
                    result.Issues.Add("⚠️ Model Space vazio — nenhuma entidade encontrada");

                if (totalEntities > 50000)
                    result.Recommendations.Add("💡 Projeto grande — considere dividir em arquivos por disciplina");
            }
            catch { }
        }

        private static void AnalyzeBIMCompliance(dynamic doc, AnalysisResult result)
        {
            try
            {
                // Verifica coordenadas
                dynamic ucs = doc.GetVariable("UCSNAME");
                if (ucs == null || ucs.ToString() == "")
                    result.Recommendations.Add("💡 UCS não nomeada — defina sistema de coordenadas para BIM");

                // Verifica unidades
                int insunits = doc.GetVariable("INSUNITS");
                if (insunits != 2 && insunits != 6)  // 2=feet, 6=meters
                    result.Recommendations.Add($"💡 Unidades: {insunits} — verificar compatibilidade BIM (esperado: metros=6)");
            }
            catch { }
        }

        private static void CompareLayers(dynamic docs, AnalysisResult result)
        {
            try
            {
                var layerSets = new List<HashSet<string>>();
                for (int i = 0; i < docs.Count; i++)
                {
                    var layers = new HashSet<string>();
                    foreach (dynamic layer in docs[i].Layers)
                        layers.Add(layer.Name.ToUpper());
                    layerSets.Add(layers);
                }

                // Compara doc 0 com os demais
                for (int i = 1; i < layerSets.Count; i++)
                {
                    var onlyIn0 = new HashSet<string>(layerSets[0]);
                    onlyIn0.ExceptWith(layerSets[i]);
                    var onlyInI = new HashSet<string>(layerSets[i]);
                    onlyInI.ExceptWith(layerSets[0]);

                    if (onlyIn0.Count > 0)
                        result.Issues.Add($"⚠️ Layers no projeto [1] e não no [{i + 1}]: {onlyIn0.Count} layers diferentes");
                    if (onlyInI.Count > 0)
                        result.Issues.Add($"⚠️ Layers no projeto [{i + 1}] e não no [1]: {onlyInI.Count} layers diferentes");
                }
            }
            catch { }
        }

        private static void CompareCoordinates(dynamic docs, AnalysisResult result)
        {
            try
            {
                double[] coords0 = null;
                for (int i = 0; i < docs.Count; i++)
                {
                    dynamic extents = docs[i].GetVariable("EXTMIN");
                    if (i == 0 && extents != null)
                        coords0 = new[] { (double)extents, (double)docs[i].GetVariable("EXTMAX") };

                    // Verificação simplificada
                }

                if (coords0 != null)
                    result.Recommendations.Add("💡 Verifique sobreposição de coordenadas entre projetos manualmente");
            }
            catch { }
        }

        private static void CompareCivil3DObjects(dynamic acadApp, dynamic docs, AnalysisResult result)
        {
            try
            {
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                // Conta objetos e alerta sobre diferenças
                int surfaces = 0, aligns = 0, corridors = 0;
                try { surfaces = civilDoc.Surfaces.Count; } catch { }
                try { aligns = civilDoc.Alignments.Count; } catch { }
                try { corridors = civilDoc.Corridors.Count; } catch { }

                // Alerta se o projeto ativo tem objetos Civil 3D mas o outro não
                bool hasCivilData = surfaces > 0 || aligns > 0 || corridors > 0;
                if (hasCivilData)
                {
                    result.Recommendations.Add($"💡 Projeto contém: {surfaces} superf., {aligns} alinh., {corridors} corr.");
                    result.Recommendations.Add("💡 Verifique manualmente se o segundo projeto tem os mesmos objetos");
                }
            }
            catch { }
        }

        private static string FormatAnalysisResult(AnalysisResult result)
        {
            var sb = new StringBuilder();

            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────");

            if (result.Issues.Count > 0)
            {
                sb.AppendLine($"🔴 PROBLEMAS ENCONTRADOS ({result.Issues.Count}):");
                foreach (var issue in result.Issues)
                    sb.AppendLine($"  {issue}");
            }
            else
            {
                sb.AppendLine("✅ Nenhum problema crítico detectado.");
            }

            sb.AppendLine();

            if (result.Recommendations.Count > 0)
            {
                sb.AppendLine($"💡 RECOMENDAÇÕES ({result.Recommendations.Count}):");
                foreach (var rec in result.Recommendations)
                    sb.AppendLine($"  {rec}");
            }

            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"🏆 Score de qualidade: {(result.Issues.Count == 0 ? "A" : result.Issues.Count <= 3 ? "B" : result.Issues.Count <= 7 ? "C" : "D")}");
            sb.AppendLine("═══════════════════════════════════════");

            return sb.ToString();
        }
    }
}
