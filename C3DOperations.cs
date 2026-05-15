using System;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Operações diretas na API do Civil 3D — cria, modifica e consulta objetos.
    /// Premium: operações que vão além de simples comandos de linha.
    /// </summary>
    public static class C3DOperations
    {
        public class OperationResult
        {
            public bool Success { get; set; }
            public string Message { get; set; } = "";
        }

        /// <summary>
        /// Interpreta e executa uma operação da API
        /// Formato: API:CREATE_SURFACE nome=XXX tipo=TIN layer=XXX
        /// </summary>
        public static OperationResult Execute(string apiCommand)
        {
            // Extrai a parte após "API:"
            var parts = apiCommand.Split(':');
            if (parts.Length < 2)
                return new OperationResult { Success = false, Message = "Formato inválido. Use API:OPERAÇÃO params" };

            var operationPart = parts[1].Trim();
            var opParts = operationPart.Split(' ');
            var operation = opParts[0].ToUpper();
            var parameters = ParseParameters(opParts);

            switch (operation)
            {
                case "CREATE_SURFACE":
                    return CreateSurface(parameters);
                case "LIST_SURFACES":
                    return ListSurfaces();
                case "LIST_ALIGNMENTS":
                    return ListAlignments();
                case "LIST_CORRIDORS":
                    return ListCorridors();
                case "LIST_PIPENETWORKS":
                    return ListPipeNetworks();
                case "SURFACE_INFO":
                    return SurfaceInfo(parameters);
                case "ALIGNMENT_INFO":
                    return AlignmentInfo(parameters);
                case "FREEZE_LAYER":
                    return FreezeLayer(parameters);
                case "THAW_LAYER":
                    return ThawLayer(parameters);
                case "SET_LAYER":
                    return SetCurrentLayer(parameters);
                case "ZOOM_EXTENTS":
                    return ZoomExtents();
                case "EXPORT_IFC":
                    return ExportIFC();
                default:
                    return new OperationResult
                    {
                        Success = false,
                        Message = $"Operação '{operation}' não reconhecida. " +
                                  "Disponíveis: CREATE_SURFACE, LIST_SURFACES, LIST_ALIGNMENTS, " +
                                  "LIST_CORRIDORS, LIST_PIPENETWORKS, SURFACE_INFO, ALIGNMENT_INFO, " +
                                  "FREEZE_LAYER, THAW_LAYER, SET_LAYER, ZOOM_EXTENTS, EXPORT_IFC"
                    };
            }
        }

        private static System.Collections.Generic.Dictionary<string, string> ParseParameters(string[] parts)
        {
            var dict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < parts.Length; i++)
            {
                var kv = parts[i].Split('=');
                if (kv.Length == 2)
                    dict[kv[0].Trim()] = kv[1].Trim();
            }
            return dict;
        }

        private static OperationResult CreateSurface(System.Collections.Generic.Dictionary<string, string> p)
        {
            var name = p.ContainsKey("nome") ? p["nome"] : (p.ContainsKey("name") ? p["name"] : "Superficie_AI");
            var type = p.ContainsKey("tipo") ? p["tipo"].ToUpper() : (p.ContainsKey("type") ? p["type"].ToUpper() : "TIN");
            var layer = p.ContainsKey("layer") ? p["layer"] : "C-TOPO";

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                // Cria a superfície
                dynamic surfaces = civilDoc.Surfaces;
                dynamic newSurf = surfaces.Add(name, type);

                // Define layer
                try
                {
                    dynamic acadDoc = acadApp.ActiveDocument;
                    dynamic layers = acadDoc.Layers;
                    // Tenta criar a layer se não existir
                    try { dynamic l = layers.Add(layer); } catch { /* já existe */ }
                    newSurf.Layer = layer;
                }
                catch { }

                return new OperationResult
                {
                    Success = true,
                    Message = $"✅ Superfície '{name}' criada com sucesso! Tipo: {type}, Layer: {layer}\n" +
                              $"   Use _AeccAddPointGroups para adicionar pontos."
                };
            }
            catch (Exception ex)
            {
                return new OperationResult
                {
                    Success = false,
                    Message = $"❌ Erro ao criar superfície: {ex.Message}"
                };
            }
        }

        private static OperationResult ListSurfaces()
        {
            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                dynamic surfaces = civilDoc.Surfaces;

                if (surfaces.Count == 0)
                    return new OperationResult { Success = true, Message = "Nenhuma superfície encontrada no projeto." };

                var msg = $"📋 {surfaces.Count} superfície(s) no projeto:\n";
                foreach (dynamic s in surfaces)
                {
                    msg += $"   ▸ {s.Name} (Tipo: {s.Type})";
                    try { msg += $" - Layer: {s.Layer}"; } catch { }
                    msg += "\n";
                }
                return new OperationResult { Success = true, Message = msg };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Erro: {ex.Message}" };
            }
        }

        private static OperationResult ListAlignments()
        {
            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                dynamic aligns = civilDoc.Alignments;

                if (aligns.Count == 0)
                    return new OperationResult { Success = true, Message = "Nenhum alinhamento encontrado." };

                var msg = $"📋 {aligns.Count} alinhamento(s):\n";
                foreach (dynamic a in aligns)
                {
                    msg += $"   ▸ {a.Name}";
                    try { msg += $" - {a.Length:0.00}m"; } catch { }
                    msg += "\n";
                }
                return new OperationResult { Success = true, Message = msg };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Erro: {ex.Message}" };
            }
        }

        private static OperationResult ListCorridors()
        {
            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                dynamic corridors = civilDoc.Corridors;

                if (corridors.Count == 0)
                    return new OperationResult { Success = true, Message = "Nenhum corredor encontrado." };

                var msg = $"📋 {corridors.Count} corredor(es):\n";
                foreach (dynamic c in corridors)
                {
                    msg += $"   ▸ {c.Name}";
                    try { msg += $" - {c.Baselines.Count} baseline(s)"; } catch { }
                    try { msg += $" - {c.CorridorSurfaces.Count} superfície(s)"; } catch { }
                    msg += "\n";
                }
                return new OperationResult { Success = true, Message = msg };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Erro: {ex.Message}" };
            }
        }

        private static OperationResult ListPipeNetworks()
        {
            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                dynamic nets = civilDoc.PipeNetworks;

                if (nets.Count == 0)
                    return new OperationResult { Success = true, Message = "Nenhuma rede de tubulação encontrada." };

                var msg = $"📋 {nets.Count} rede(s):\n";
                foreach (dynamic n in nets)
                {
                    msg += $"   ▸ {n.Name}";
                    try { msg += $" - {n.Pipes.Count} tubos, {n.Structures.Count} estruturas"; } catch { }
                    msg += "\n";
                }
                return new OperationResult { Success = true, Message = msg };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Erro: {ex.Message}" };
            }
        }

        private static OperationResult SurfaceInfo(System.Collections.Generic.Dictionary<string, string> p)
        {
            var name = p.ContainsKey("nome") ? p["nome"] : (p.ContainsKey("name") ? p["name"] : "");
            if (string.IsNullOrWhiteSpace(name))
                return new OperationResult { Success = false, Message = "Especifique o nome da superfície: nome=XXX" };

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                dynamic surfaces = civilDoc.Surfaces;

                foreach (dynamic s in surfaces)
                {
                    if (s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        var msg = $"📊 Superfície: {s.Name}\n";
                        msg += $"   Tipo: {s.Type}\n";
                        try { msg += $"   Estilo: {s.Style.Name}\n"; } catch { }
                        try { msg += $"   Render Material: {s.RenderMaterialName}\n"; } catch { }
                        return new OperationResult { Success = true, Message = msg };
                    }
                }
                return new OperationResult { Success = false, Message = $"Superfície '{name}' não encontrada." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Erro: {ex.Message}" };
            }
        }

        private static OperationResult AlignmentInfo(System.Collections.Generic.Dictionary<string, string> p)
        {
            var name = p.ContainsKey("nome") ? p["nome"] : (p.ContainsKey("name") ? p["name"] : "");
            if (string.IsNullOrWhiteSpace(name))
                return new OperationResult { Success = false, Message = "Especifique o nome do alinhamento: nome=XXX" };

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                dynamic aligns = civilDoc.Alignments;

                foreach (dynamic a in aligns)
                {
                    if (a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        var msg = $"📊 Alinhamento: {a.Name}\n";
                        try { msg += $"   Comprimento: {a.Length:0.00}m\n"; } catch { }
                        try { msg += $"   Estação inicial: {a.StartingStation:0.00}\n"; } catch { }
                        try { msg += $"   Estação final: {a.EndingStation:0.00}\n"; } catch { }
                        try { msg += $"   Estilo: {a.Style.Name}\n"; } catch { }
                        return new OperationResult { Success = true, Message = msg };
                    }
                }
                return new OperationResult { Success = false, Message = $"Alinhamento '{name}' não encontrado." };
            }
            catch (Exception ex)
            {
                return new OperationResult { Success = false, Message = $"Erro: {ex.Message}" };
            }
        }

        private static OperationResult FreezeLayer(System.Collections.Generic.Dictionary<string, string> p)
        {
            var name = p.ContainsKey("layer") ? p["layer"] : (p.ContainsKey("nome") ? p["nome"] : "");
            if (string.IsNullOrWhiteSpace(name))
                return new OperationResult { Success = false, Message = "Especifique o nome da layer: layer=XXX" };

            DeepSeekEngine.SendToAutoCAD($"_.-LAYER _F {name} ;");
            return new OperationResult { Success = true, Message = $"✅ Layer '{name}' congelada." };
        }

        private static OperationResult ThawLayer(System.Collections.Generic.Dictionary<string, string> p)
        {
            var name = p.ContainsKey("layer") ? p["layer"] : (p.ContainsKey("nome") ? p["nome"] : "");
            if (string.IsNullOrWhiteSpace(name))
                return new OperationResult { Success = false, Message = "Especifique o nome da layer: layer=XXX" };

            DeepSeekEngine.SendToAutoCAD($"_.-LAYER _T {name} ;");
            return new OperationResult { Success = true, Message = $"✅ Layer '{name}' descongelada." };
        }

        private static OperationResult SetCurrentLayer(System.Collections.Generic.Dictionary<string, string> p)
        {
            var name = p.ContainsKey("layer") ? p["layer"] : (p.ContainsKey("nome") ? p["nome"] : "");
            if (string.IsNullOrWhiteSpace(name))
                return new OperationResult { Success = false, Message = "Especifique o nome da layer: layer=XXX" };

            DeepSeekEngine.SendToAutoCAD($"_.-LAYER _S {name} ;");
            return new OperationResult { Success = true, Message = $"✅ Layer atual: '{name}'." };
        }

        private static OperationResult ZoomExtents()
        {
            DeepSeekEngine.SendToAutoCAD("_.ZOOM _E");
            return new OperationResult { Success = true, Message = "✅ Zoom Extents executado." };
        }

        private static OperationResult ExportIFC()
        {
            DeepSeekEngine.SendToAutoCAD("_.-EXPORTIFC");
            return new OperationResult
            {
                Success = true,
                Message = "✅ Comando de exportação IFC iniciado.\n" +
                          "   Selecione o caminho e nome do arquivo na janela do AutoCAD."
            };
        }
    }
}
