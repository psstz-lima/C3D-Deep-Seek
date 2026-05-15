using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;

namespace C3DDeepSeek
{
    /// <summary>
    /// Lê o contexto do desenho atual do Civil 3D / AutoCAD para enviar ao DeepSeek.
    /// Assim o assistente sabe exatamente o que existe no projeto aberto.
    /// </summary>
    public static class DeepSeekContext
    {
        /// <summary>
        /// Coleta todas as informações relevantes do desenho atual
        /// </summary>
        public static string CollectContext()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return "[Nenhum documento aberto]";

            var db = doc.Database;
            var sb = new StringBuilder();

            // ── Informações básicas ──
            sb.AppendLine($"📄 Desenho: {doc.Name}");
            sb.AppendLine();

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // ── Layers ──
                    var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    var layerNames = new List<string>();
                    foreach (ObjectId id in layerTable)
                    {
                        var layer = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (!layer.IsErased && !layer.IsDependent)
                            layerNames.Add(layer.Name);
                    }
                    sb.AppendLine($"📐 Layers ({layerNames.Count}): {string.Join(", ", layerNames)}");

                    // ── Blocos ──
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    int blockRefCount = 0;
                    var modelSpace = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId id in modelSpace)
                    {
                        if (id.ObjectClass.DxfName == "INSERT")
                            blockRefCount++;
                    }
                    sb.AppendLine($"🧱 Blocos inseridos no Model: {blockRefCount}");

                    // ── Entidades básicas ──
                    int lineCount = 0, polylineCount = 0, textCount = 0, mtextCount = 0, circleCount = 0;
                    foreach (ObjectId id in modelSpace)
                    {
                        var dxf = id.ObjectClass.DxfName;
                        switch (dxf)
                        {
                            case "LINE": lineCount++; break;
                            case "LWPOLYLINE": polylineCount++; break;
                            case "TEXT": textCount++; break;
                            case "MTEXT": mtextCount++; break;
                            case "CIRCLE": circleCount++; break;
                        }
                    }
                    if (lineCount + polylineCount + textCount + mtextCount + circleCount > 0)
                        sb.AppendLine($"📏 Entidades: {lineCount} linhas, {polylineCount} polilinhas, {circleCount} círculos, {textCount} textos, {mtextCount} MTexts");

                    tr.Commit();
                }
                catch
                {
                    // Se algo falhar, continua sem as informações do AutoCAD
                }
            }

            // ── Civil 3D Objects (via COM, mais confiável que a API managed em alguns casos) ──
            try
            {
                CollectCivil3DContext(sb);
            }
            catch
            {
                sb.AppendLine("⚠️ Não foi possível ler objetos do Civil 3D (API COM indisponível).");
            }

            return sb.ToString();
        }

        private static void CollectCivil3DContext(StringBuilder sb)
        {
            // Tenta via API managed primeiro
            try
            {
                CollectCivilManaged(sb);
                return;
            }
            catch
            {
                // Fallback: tenta via COM
            }

            try
            {
                CollectCivilCom(sb);
            }
            catch
            {
                sb.AppendLine("⚠️ Não foi possível ler objetos do Civil 3D.");
            }
        }

        private static void CollectCivilManaged(StringBuilder sb)
        {
            // Usa a API managed do Civil 3D (AeccDbMgd)
            var civilDoc = GetCivilDocument();
            if (civilDoc == null)
            {
                sb.AppendLine("⚠️ Documento Civil 3D não detectado (AeccDbMgd).");
                return;
            }

            using (var tr = civilDoc.Database.TransactionManager.StartTransaction())
            {
                // ── Alignments ──
                try
                {
                    var alignIds = civilDoc.GetAlignmentIds();
                    if (alignIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (ObjectId id in alignIds)
                        {
                            var align = (dynamic)tr.GetObject(id, OpenMode.ForRead);
                            names.Add(align.Name);
                        }
                        sb.AppendLine($"🛤️ Alinhamentos ({alignIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { /* sem alinhamentos ou sem acesso */ }

                // ── Surfaces ──
                try
                {
                    var surfIds = civilDoc.GetSurfaceIds();
                    if (surfIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (ObjectId id in surfIds)
                        {
                            var surf = (dynamic)tr.GetObject(id, OpenMode.ForRead);
                            names.Add(surf.Name);
                        }
                        sb.AppendLine($"⛰️ Superfícies ({surfIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Corridors ──
                try
                {
                    var corrIds = civilDoc.GetCorridorIds();
                    if (corrIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (ObjectId id in corrIds)
                        {
                            var corr = (dynamic)tr.GetObject(id, OpenMode.ForRead);
                            names.Add(corr.Name);
                        }
                        sb.AppendLine($"🛣️ Corredores ({corrIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Pipe Networks ──
                try
                {
                    var pipeIds = civilDoc.GetPipeNetworkIds();
                    if (pipeIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (ObjectId id in pipeIds)
                        {
                            var net = (dynamic)tr.GetObject(id, OpenMode.ForRead);
                            names.Add(net.Name);
                        }
                        sb.AppendLine($"🔧 Redes de tubulação ({pipeIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Profiles ──
                try
                {
                    var profIds = civilDoc.GetProfileIds();
                    if (profIds.Count > 0)
                        sb.AppendLine($"📊 Perfis: {profIds.Count}");
                }
                catch { }

                // ── Assemblies ──
                try
                {
                    var asmIds = civilDoc.GetAssemblyIds();
                    if (asmIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (ObjectId id in asmIds)
                        {
                            var asm = (dynamic)tr.GetObject(id, OpenMode.ForRead);
                            names.Add(asm.Name);
                        }
                        sb.AppendLine($"🏗️ Montagens ({asmIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Sites ──
                try
                {
                    var siteIds = civilDoc.GetSiteIds();
                    if (siteIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (ObjectId id in siteIds)
                        {
                            var site = (dynamic)tr.GetObject(id, OpenMode.ForRead);
                            names.Add(site.Name);
                        }
                        sb.AppendLine($"📍 Sites ({siteIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Parcels ──
                try
                {
                    var parcelIds = civilDoc.GetParcelIds();
                    if (parcelIds.Count > 0)
                        sb.AppendLine($"🏘️ Lotes: {parcelIds.Count}");
                }
                catch { }

                // ── Feature Lines ──
                try
                {
                    var flIds = civilDoc.GetFeatureLineIds();
                    if (flIds.Count > 0)
                        sb.AppendLine($"📐 Feature Lines: {flIds.Count}");
                }
                catch { }

                // ── Grading ──
                try
                {
                    var gradIds = civilDoc.GetGradingIds();
                    if (gradIds.Count > 0)
                        sb.AppendLine($"⛏️ Gradings: {gradIds.Count}");
                }
                catch { }

                tr.Commit();
            }
        }

        private static void CollectCivilCom(StringBuilder sb)
        {
            // Fallback via COM Interop
            try
            {
                dynamic acadApp = Application.AcadApplication;
                dynamic civilApp = null;

                // Tenta obter a aplicação Civil 3D via COM
                try { civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0"); }
                catch { try { civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.13.0"); }
                catch { /* versões anteriores */ } }

                if (civilApp == null)
                {
                    sb.AppendLine("⚠️ Aplicação Civil 3D não encontrada via COM.");
                    return;
                }

                dynamic civilDoc = civilApp.ActiveDocument;

                // Alignments
                try
                {
                    dynamic aligns = civilDoc.Alignments;
                    if (aligns.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (dynamic a in aligns) names.Add(a.Name);
                        sb.AppendLine($"🛤️ Alinhamentos ({aligns.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // Surfaces
                try
                {
                    dynamic surfaces = civilDoc.Surfaces;
                    if (surfaces.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (dynamic s in surfaces) names.Add(s.Name);
                        sb.AppendLine($"⛰️ Superfícies ({surfaces.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // Corridors
                try
                {
                    dynamic corridors = civilDoc.Corridors;
                    if (corridors.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (dynamic c in corridors) names.Add(c.Name);
                        sb.AppendLine($"🛣️ Corredores ({corridors.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // Pipe Networks
                try
                {
                    dynamic pipeNets = civilDoc.PipeNetworks;
                    if (pipeNets.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (dynamic p in pipeNets) names.Add(p.Name);
                        sb.AppendLine($"🔧 Redes de tubulação ({pipeNets.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // Sites
                try
                {
                    dynamic sites = civilDoc.Sites;
                    if (sites.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (dynamic s in sites) names.Add(s.Name);
                        sb.AppendLine($"📍 Sites ({sites.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // Profiles
                try { dynamic profiles = civilDoc.Profiles; if (profiles.Count > 0) sb.AppendLine($"📊 Perfis: {profiles.Count}"); }
                catch { }

                // Assemblies
                try { dynamic asms = civilDoc.Assemblies; if (asms.Count > 0) sb.AppendLine($"🏗️ Montagens: {asms.Count}"); }
                catch { }

                // Parcels
                try { dynamic parcels = civilDoc.Parcels; if (parcels.Count > 0) sb.AppendLine($"🏘️ Lotes: {parcels.Count}"); }
                catch { }

                // Feature Lines
                try { dynamic fls = civilDoc.FeatureLines; if (fls.Count > 0) sb.AppendLine($"📐 Feature Lines: {fls.Count}"); }
                catch { }

                // Gradings
                try { dynamic grads = civilDoc.Gradings; if (grads.Count > 0) sb.AppendLine($"⛏️ Gradings: {grads.Count}"); }
                catch { }
            }
            catch
            {
                sb.AppendLine("⚠️ Não foi possível acessar objetos Civil 3D via COM.");
            }
        }

        /// <summary>
        /// Tenta obter o CivilDocument usando reflexão (evita dependência forte de AeccDbMgd)
        /// </summary>
        private static dynamic GetCivilDocument()
        {
            try
            {
                // Tenta carregar CivilApplication dinamicamente
                var civilAppType = Type.GetType("Autodesk.Civil.ApplicationServices.CivilApplication, AeccDbMgd");
                if (civilAppType == null) return null;

                var activeDocProp = civilAppType.GetProperty("ActiveDocument");
                if (activeDocProp == null) return null;

                return activeDocProp.GetValue(null);
            }
            catch
            {
                return null;
            }
        }
    }
}
