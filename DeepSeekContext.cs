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
            // Tenta via API managed primeiro (in-process, NETLOAD)
            if (CollectCivilManaged(sb))
                return;

            // Fallback: tenta via COM interop
            if (CollectCivilCom(sb))
                return;

            sb.AppendLine("⚠️ Civil 3D não detectado. O desenho pode ser AutoCAD puro (sem objetos Civil).");
        }

        /// <summary>
        /// Tenta carregar AeccDbMgd e usar CivilApplication diretamente
        /// </summary>
        private static bool CollectCivilManaged(StringBuilder sb)
        {
            try
            {
                // Tenta carregar o assembly do Civil 3D explicitamente
                var asm = System.Reflection.Assembly.LoadFrom(
                    @"C:\Program Files\Autodesk\AutoCAD 2026\C3D\AeccDbMgd.dll");

                if (asm == null) return false;

                var civilAppType = asm.GetType("Autodesk.Civil.ApplicationServices.CivilApplication");
                if (civilAppType == null) return false;

                var activeDocProp = civilAppType.GetProperty("ActiveDocument");
                if (activeDocProp == null) return false;

                dynamic civilDoc = activeDocProp.GetValue(null);
                if (civilDoc == null) return false;

                // ── Alignments ──
                try
                {
                    dynamic alignIds = civilDoc.GetAlignmentIds();
                    if (alignIds != null && alignIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (var id in alignIds)
                        {
                            try
                            {
                                dynamic tr = civilDoc.Database.TransactionManager.StartTransaction();
                                dynamic al = tr.GetObject(id, (Autodesk.AutoCAD.DatabaseServices.OpenMode)0); // ForRead
                                names.Add(al.Name);
                                tr.Dispose();
                            }
                            catch { }
                        }
                        if (names.Count > 0)
                            sb.AppendLine($"🛤️ Alinhamentos ({alignIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Surfaces ──
                try
                {
                    dynamic surfIds = civilDoc.GetSurfaceIds();
                    if (surfIds != null && surfIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (var id in surfIds)
                        {
                            try
                            {
                                dynamic tr = civilDoc.Database.TransactionManager.StartTransaction();
                                dynamic s = tr.GetObject(id, (Autodesk.AutoCAD.DatabaseServices.OpenMode)0);
                                names.Add(s.Name);
                                tr.Dispose();
                            }
                            catch { }
                        }
                        if (names.Count > 0)
                            sb.AppendLine($"⛰️ Superfícies ({surfIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Corridors ──
                try
                {
                    dynamic corrIds = civilDoc.GetCorridorIds();
                    if (corrIds != null && corrIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (var id in corrIds)
                        {
                            try
                            {
                                dynamic tr = civilDoc.Database.TransactionManager.StartTransaction();
                                dynamic c = tr.GetObject(id, (Autodesk.AutoCAD.DatabaseServices.OpenMode)0);
                                names.Add(c.Name);
                                tr.Dispose();
                            }
                            catch { }
                        }
                        if (names.Count > 0)
                            sb.AppendLine($"🛣️ Corredores ({corrIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Pipe Networks ──
                try
                {
                    dynamic pipeIds = civilDoc.GetPipeNetworkIds();
                    if (pipeIds != null && pipeIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (var id in pipeIds)
                        {
                            try
                            {
                                dynamic tr = civilDoc.Database.TransactionManager.StartTransaction();
                                dynamic p = tr.GetObject(id, (Autodesk.AutoCAD.DatabaseServices.OpenMode)0);
                                names.Add(p.Name);
                                tr.Dispose();
                            }
                            catch { }
                        }
                        if (names.Count > 0)
                            sb.AppendLine($"🔧 Redes de tubulação ({pipeIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Profiles ──
                try
                {
                    dynamic profIds = civilDoc.GetProfileIds();
                    if (profIds != null && profIds.Count > 0)
                        sb.AppendLine($"📊 Perfis: {profIds.Count}");
                }
                catch { }

                // ── Assemblies ──
                try
                {
                    dynamic asmIds = civilDoc.GetAssemblyIds();
                    if (asmIds != null && asmIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (var id in asmIds)
                        {
                            try
                            {
                                dynamic tr = civilDoc.Database.TransactionManager.StartTransaction();
                                dynamic a = tr.GetObject(id, (Autodesk.AutoCAD.DatabaseServices.OpenMode)0);
                                names.Add(a.Name);
                                tr.Dispose();
                            }
                            catch { }
                        }
                        if (names.Count > 0)
                            sb.AppendLine($"🏗️ Montagens ({asmIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Sites ──
                try
                {
                    dynamic siteIds = civilDoc.GetSiteIds();
                    if (siteIds != null && siteIds.Count > 0)
                    {
                        var names = new List<string>();
                        foreach (var id in siteIds)
                        {
                            try
                            {
                                dynamic tr = civilDoc.Database.TransactionManager.StartTransaction();
                                dynamic si = tr.GetObject(id, (Autodesk.AutoCAD.DatabaseServices.OpenMode)0);
                                names.Add(si.Name);
                                tr.Dispose();
                            }
                            catch { }
                        }
                        if (names.Count > 0)
                            sb.AppendLine($"📍 Sites ({siteIds.Count}): {string.Join(", ", names)}");
                    }
                }
                catch { }

                // ── Parcels ──
                try
                {
                    dynamic parcelIds = civilDoc.GetParcelIds();
                    if (parcelIds != null && parcelIds.Count > 0)
                        sb.AppendLine($"🏘️ Lotes: {parcelIds.Count}");
                }
                catch { }

                // ── Feature Lines ──
                try
                {
                    dynamic flIds = civilDoc.GetFeatureLineIds();
                    if (flIds != null && flIds.Count > 0)
                        sb.AppendLine($"📐 Feature Lines: {flIds.Count}");
                }
                catch { }

                // ── Gradings ──
                try
                {
                    dynamic gradIds = civilDoc.GetGradingIds();
                    if (gradIds != null && gradIds.Count > 0)
                        sb.AppendLine($"⛏️ Gradings: {gradIds.Count}");
                }
                catch { }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Fallback COM — usa o AutoCAD.Application COM para acessar objetos Civil 3D
        /// </summary>
        private static bool CollectCivilCom(StringBuilder sb)
        {
            try
            {
                // Usa o objeto Application do AutoCAD (in-process)
                dynamic acadApp = Application.AcadApplication;
                if (acadApp == null) return false;

                dynamic civilApp = null;

                // Tenta várias versões do ProgID do Civil 3D
                string[] progIds = {
                    "AeccXUiLand.AeccApplication.14.0",  // Civil 3D 2026
                    "AeccXUiLand.AeccApplication.13.5",  // Civil 3D 2025
                    "AeccXUiLand.AeccApplication.13.0",  // Civil 3D 2024
                    "AeccXUiLand.AeccApplication.12.0",  // Civil 3D 2023
                };

                foreach (var progId in progIds)
                {
                    try
                    {
                        civilApp = acadApp.GetInterfaceObject(progId);
                        if (civilApp != null) break;
                    }
                    catch { }
                }

                if (civilApp == null) return false;

                dynamic civilDoc = civilApp.ActiveDocument;
                if (civilDoc == null) return false;

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

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
