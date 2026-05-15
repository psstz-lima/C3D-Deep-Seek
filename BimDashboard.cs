using System;
using System.Text;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Dashboard BIM — visão consolidada do projeto com indicadores,
    /// progresso e checklist de conformidade.
    /// </summary>
    public static class BimDashboard
    {
        public static string Generate()
        {
            var sb = new StringBuilder();

            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  📊 DASHBOARD BIM — C3D DeepSeek");
            sb.AppendLine("═══════════════════════════════════════");

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic doc = acadApp.ActiveDocument;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                sb.AppendLine($"📄 Projeto: {doc.Name}");
                sb.AppendLine($"📅 Data: {DateTime.Now:dd/MM/yyyy HH:mm}");
                sb.AppendLine();

                // ── Indicadores ──
                int surfaces = 0, aligns = 0, corridors = 0, pipes = 0, profiles = 0;
                int sites = 0, parcels = 0, assemblies = 0, flines = 0;

                try { surfaces = civilDoc.Surfaces.Count; } catch { }
                try { aligns = civilDoc.Alignments.Count; } catch { }
                try { corridors = civilDoc.Corridors.Count; } catch { }
                try { pipes = civilDoc.PipeNetworks.Count; } catch { }
                try { profiles = civilDoc.Profiles.Count; } catch { }
                try { sites = civilDoc.Sites.Count; } catch { }
                try { parcels = civilDoc.Parcels.Count; } catch { }
                try { assemblies = civilDoc.Assemblies.Count; } catch { }
                try { flines = civilDoc.FeatureLines.Count; } catch { }

                sb.AppendLine("┌───────────────────────────────────────┐");
                sb.AppendLine("│        INDICADORES DO PROJETO         │");
                sb.AppendLine("├──────────────┬────────────────────────┤");
                sb.AppendLine($"│ Superfícies  │ {surfaces,-22} │");
                sb.AppendLine($"│ Alinhamentos │ {aligns,-22} │");
                sb.AppendLine($"│ Perfis       │ {profiles,-22} │");
                sb.AppendLine($"│ Corredores   │ {corridors,-22} │");
                sb.AppendLine($"│ Montagens    │ {assemblies,-22} │");
                sb.AppendLine($"│ Redes Tub.   │ {pipes,-22} │");
                sb.AppendLine($"│ Sites        │ {sites,-22} │");
                sb.AppendLine($"│ Parcelas     │ {parcels,-22} │");
                sb.AppendLine($"│ Feat. Lines  │ {flines,-22} │");
                sb.AppendLine("└──────────────┴────────────────────────┘");

                // ── Progresso estimado ──
                int totalPossible = 9;
                int completed = 0;
                if (surfaces > 0) completed++;
                if (aligns > 0) completed++;
                if (corridors > 0) completed++;
                if (pipes > 0) completed++;
                if (profiles > 0) completed++;
                if (assemblies > 0) completed++;
                if (parcels > 0) completed++;
                if (flines > 0) completed++;

                double progress = (double)completed / totalPossible * 100;
                string bar = new string('█', (int)(progress / 5)) + new string('░', 20 - (int)(progress / 5));

                sb.AppendLine($"\n📈 PROGRESSO: {progress:0}%");
                sb.AppendLine($"   [{bar}]");
                sb.AppendLine($"   {completed}/{totalPossible} disciplinas iniciadas");

                // ── Scores de qualidade ──
                sb.AppendLine($"\n🏆 SCORES:");
                sb.AppendLine($"   Superfícies: {(surfaces > 0 ? "✅" : "⬜")} {(surfaces > 1 ? "Avançado" : surfaces > 0 ? "Iniciado" : "Não iniciado")}");
                sb.AppendLine($"   Alinhamentos: {(aligns > 0 ? "✅" : "⬜")} {(aligns > 1 ? "Avançado" : aligns > 0 ? "Iniciado" : "Não iniciado")}");
                sb.AppendLine($"   Corredores: {(corridors > 0 ? "✅" : "⬜")} {(corridors > 0 ? "Iniciado" : "Não iniciado")}");
                sb.AppendLine($"   Drenagem: {(pipes > 0 ? "✅" : "⬜")} {(pipes > 0 ? "Iniciado" : "Não iniciado")}");

                // ── Checklist BIM ──
                sb.AppendLine($"\n📋 CHECKLIST BIM:");
                bool hasCoordSystem = false;
                try { dynamic ucs = doc.GetVariable("UCSNAME"); hasCoordSystem = ucs != null && ucs.ToString() != ""; } catch { }
                sb.AppendLine($"   {(hasCoordSystem ? "✅" : "⬜")} Sistema de coordenadas definido");
                sb.AppendLine($"   {(surfaces > 0 ? "✅" : "⬜")} Superfície de terreno criada");
                sb.AppendLine($"   {(aligns > 0 && profiles > 0 ? "✅" : "⬜")} Alinhamento + Perfil");
                sb.AppendLine($"   {(corridors > 0 ? "✅" : "⬜")} Corredor modelado");
                sb.AppendLine($"   {(pipes > 0 ? "✅" : "⬜")} Redes de infraestrutura");

                // ── Recomendações ──
                sb.AppendLine($"\n💡 PRÓXIMOS PASSOS:");
                if (surfaces == 0) sb.AppendLine("   • Criar superfície do terreno (DSMODEL)");
                if (aligns == 0) sb.AppendLine("   • Criar alinhamento horizontal (DSMODEL)");
                if (aligns > 0 && profiles == 0) sb.AppendLine("   • Criar perfil do terreno (DSWORKFLOW RODOVIA)");
                if (aligns > 0 && profiles > 0 && corridors == 0) sb.AppendLine("   • Criar corredor (DSWORKFLOW CORREDOR)");
                if (corridors > 0) sb.AppendLine("   • Gerar seções e volumes (DSWORKFLOW SECOES)");
                if (!hasCoordSystem) sb.AppendLine("   • Definir sistema de coordenadas (DSTRANSFORM)");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"❌ Erro: {ex.Message}");
            }

            sb.AppendLine("═══════════════════════════════════════");
            return sb.ToString();
        }
    }
}
