using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Exportação de relatórios para Excel .xlsx via COM Interop.
    /// Gera planilhas profissionais com abas para cada categoria.
    /// </summary>
    public static class ExcelExporter
    {
        /// <summary>
        /// Exporta relatório completo para Excel
        /// </summary>
        public static string ExportFullReport(string outputPath = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
                    var drawingName = doc?.Name ?? "Projeto";
                    drawingName = Path.GetFileNameWithoutExtension(drawingName);
                    outputPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                        $"Relatorio_{drawingName}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                }

                dynamic excel = null;
                dynamic workbook = null;

                try
                {
                    excel = Activator.CreateInstance(Type.GetTypeFromProgID("Excel.Application"));
                    excel.Visible = false;
                    excel.DisplayAlerts = false;
                    workbook = excel.Workbooks.Add();

                    // Aba 1: Resumo
                    CreateSummarySheet(workbook);

                    // Aba 2: Layers
                    CreateLayersSheet(workbook);

                    // Aba 3: Entidades
                    CreateEntitiesSheet(workbook);

                    // Aba 4: Civil 3D
                    CreateCivil3DSheet(workbook);

                    // Aba 5: Superfícies
                    CreateSurfacesSheet(workbook);

                    // Aba 6: Alinhamentos
                    CreateAlignmentsSheet(workbook);

                    // Aba 7: Corredores
                    CreateCorridorsSheet(workbook);

                    // Aba 8: Redes
                    CreatePipeNetworksSheet(workbook);

                    workbook.SaveAs(outputPath);
                    workbook.Close();
                    excel.Quit();

                    return $"✅ Relatório Excel salvo em:\n{outputPath}";
                }
                finally
                {
                    if (workbook != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(workbook);
                    if (excel != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(excel);
                }
            }
            catch (Exception ex)
            {
                // Fallback: gera CSV
                return ExportCsvReport(ex.Message);
            }
        }

        private static string ExportCsvReport(string errorDetail = "")
        {
            try
            {
                var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
                var drawingName = doc?.Name ?? "Projeto";
                drawingName = Path.GetFileNameWithoutExtension(drawingName);
                var outputPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"Relatorio_{drawingName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                var sb = new StringBuilder();
                sb.AppendLine("Tipo;Categoria;Nome;Detalhes");

                // Coleta dados via COM
                try
                {
                    dynamic acadApp = AcAp.Application.AcadApplication;
                    dynamic adoc = acadApp.ActiveDocument;
                    dynamic layers = adoc.Layers;
                    foreach (dynamic layer in layers)
                        sb.AppendLine($"Layer;{layer.Name};{(layer.LayerOn ? "ON" : "OFF")};Cor={layer.Color}");

                    dynamic ms = adoc.ModelSpace;
                    foreach (dynamic entity in ms)
                        sb.AppendLine($"Entidade;{entity.EntityName};{entity.Layer};;");
                }
                catch { }

                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
                return $"⚠️ Excel não disponível. CSV gerado em:\n{outputPath}";
            }
            catch (Exception ex2)
            {
                return $"❌ Erro ao gerar CSV: {ex2.Message}";
            }
        }

        private static void CreateSummarySheet(dynamic workbook)
        {
            dynamic sheet = workbook.Sheets[1];
            sheet.Name = "Resumo";

            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            sheet.Cells[1, 1] = "RELATÓRIO C3D DEEPSEEK — PROJETO";
            sheet.Cells[1, 1].Font.Bold = true;
            sheet.Cells[1, 1].Font.Size = 14;

            sheet.Cells[3, 1] = "Desenho:";
            sheet.Cells[3, 2] = doc?.Name ?? "N/A";
            sheet.Cells[4, 1] = "Data:";
            sheet.Cells[4, 2] = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            sheet.Cells[5, 1] = "Aplicação:";
            sheet.Cells[5, 2] = "Autodesk Civil 3D 2026";

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic adoc = acadApp.ActiveDocument;
                dynamic layers = adoc.Layers;
                int totalLayers = 0;
                foreach (dynamic l in layers) totalLayers++;
                sheet.Cells[7, 1] = "Total Layers:";
                sheet.Cells[7, 2] = totalLayers;
            }
            catch { }

            sheet.Columns[1].ColumnWidth = 25;
            sheet.Columns[2].ColumnWidth = 60;
        }

        private static void CreateLayersSheet(dynamic workbook)
        {
            dynamic sheet = workbook.Sheets.Add();
            sheet.Name = "Layers";

            sheet.Cells[1, 1] = "Nome";
            sheet.Cells[1, 2] = "Status";
            sheet.Cells[1, 3] = "Cor";
            sheet.Cells[1, 4] = "Tipo Linha";
            sheet.Cells[1, 5] = "Congelada";
            sheet.Cells[1, 6] = "Bloqueada";
            sheet.Range["A1:F1"].Font.Bold = true;

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic adoc = acadApp.ActiveDocument;
                dynamic layers = adoc.Layers;
                int row = 2;
                foreach (dynamic layer in layers)
                {
                    sheet.Cells[row, 1] = layer.Name;
                    sheet.Cells[row, 2] = layer.LayerOn ? "ON" : "OFF";
                    sheet.Cells[row, 3] = layer.Color.ToString();
                    sheet.Cells[row, 4] = layer.Linetype;
                    sheet.Cells[row, 5] = layer.Freeze ? "Sim" : "Não";
                    sheet.Cells[row, 6] = layer.Lock ? "Sim" : "Não";
                    row++;
                }
            }
            catch { }

            sheet.Columns.AutoFit();
        }

        private static void CreateEntitiesSheet(dynamic workbook)
        {
            dynamic sheet = workbook.Sheets.Add();
            sheet.Name = "Entidades";

            sheet.Cells[1, 1] = "Tipo";
            sheet.Cells[1, 2] = "Layer";
            sheet.Cells[1, 3] = "Handle";
            sheet.Range["A1:C1"].Font.Bold = true;

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic adoc = acadApp.ActiveDocument;
                dynamic ms = adoc.ModelSpace;
                int row = 2;
                foreach (dynamic entity in ms)
                {
                    sheet.Cells[row, 1] = entity.EntityName;
                    sheet.Cells[row, 2] = entity.Layer;
                    sheet.Cells[row, 3] = entity.Handle;
                    row++;
                    if (row > 5000) break; // limite para performance
                }
            }
            catch { }

            sheet.Columns.AutoFit();
        }

        private static void CreateCivil3DSheet(dynamic workbook)
        {
            dynamic sheet = workbook.Sheets.Add();
            sheet.Name = "Civil3D_Objetos";

            sheet.Cells[1, 1] = "Tipo";
            sheet.Cells[1, 2] = "Nome";
            sheet.Cells[1, 3] = "Detalhes";
            sheet.Range["A1:C1"].Font.Bold = true;

            int row = 2;
            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;

                // Alignments
                try
                {
                    foreach (dynamic a in civilDoc.Alignments)
                    {
                        sheet.Cells[row, 1] = "Alinhamento";
                        sheet.Cells[row, 2] = a.Name;
                        try { sheet.Cells[row, 3] = $"{a.Length:0.00}m"; } catch { }
                        row++;
                    }
                }
                catch { }

                // Surfaces
                try
                {
                    foreach (dynamic s in civilDoc.Surfaces)
                    {
                        sheet.Cells[row, 1] = "Superfície";
                        sheet.Cells[row, 2] = s.Name;
                        try { sheet.Cells[row, 3] = s.Type; } catch { }
                        row++;
                    }
                }
                catch { }

                // Corridors
                try
                {
                    foreach (dynamic c in civilDoc.Corridors)
                    {
                        sheet.Cells[row, 1] = "Corredor";
                        sheet.Cells[row, 2] = c.Name;
                        row++;
                    }
                }
                catch { }

                // Pipe Networks
                try
                {
                    foreach (dynamic p in civilDoc.PipeNetworks)
                    {
                        sheet.Cells[row, 1] = "Rede Tubulação";
                        sheet.Cells[row, 2] = p.Name;
                        row++;
                    }
                }
                catch { }

                // Sites
                try
                {
                    foreach (dynamic si in civilDoc.Sites)
                    {
                        sheet.Cells[row, 1] = "Site";
                        sheet.Cells[row, 2] = si.Name;
                        row++;
                    }
                }
                catch { }
            }
            catch { }

            sheet.Columns.AutoFit();
        }

        private static void CreateSurfacesSheet(dynamic workbook)
        {
            dynamic sheet = workbook.Sheets.Add();
            sheet.Name = "Superficies";

            sheet.Cells[1, 1] = "Nome";
            sheet.Cells[1, 2] = "Tipo";
            sheet.Cells[1, 3] = "Estilo";
            sheet.Cells[1, 4] = "Layer";
            sheet.Range["A1:D1"].Font.Bold = true;

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                int row = 2;
                foreach (dynamic s in civilDoc.Surfaces)
                {
                    sheet.Cells[row, 1] = s.Name;
                    sheet.Cells[row, 2] = s.Type;
                    try { sheet.Cells[row, 3] = s.Style.Name; } catch { }
                    try { sheet.Cells[row, 4] = s.Layer; } catch { }
                    row++;
                }
            }
            catch { }

            sheet.Columns.AutoFit();
        }

        private static void CreateAlignmentsSheet(dynamic workbook)
        {
            dynamic sheet = workbook.Sheets.Add();
            sheet.Name = "Alinhamentos";

            sheet.Cells[1, 1] = "Nome";
            sheet.Cells[1, 2] = "Comprimento";
            sheet.Cells[1, 3] = "Estação Inicial";
            sheet.Cells[1, 4] = "Estação Final";
            sheet.Cells[1, 5] = "Estilo";
            sheet.Range["A1:E1"].Font.Bold = true;

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                int row = 2;
                foreach (dynamic a in civilDoc.Alignments)
                {
                    sheet.Cells[row, 1] = a.Name;
                    try { sheet.Cells[row, 2] = $"{a.Length:0.00}"; } catch { }
                    try { sheet.Cells[row, 3] = $"{a.StartingStation:0.00}"; } catch { }
                    try { sheet.Cells[row, 4] = $"{a.EndingStation:0.00}"; } catch { }
                    try { sheet.Cells[row, 5] = a.Style.Name; } catch { }
                    row++;
                }
            }
            catch { }

            sheet.Columns.AutoFit();
        }

        private static void CreateCorridorsSheet(dynamic workbook)
        {
            dynamic sheet = workbook.Sheets.Add();
            sheet.Name = "Corredores";

            sheet.Cells[1, 1] = "Nome";
            sheet.Cells[1, 2] = "Baselines";
            sheet.Cells[1, 3] = "Superfícies";
            sheet.Range["A1:C1"].Font.Bold = true;

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                int row = 2;
                foreach (dynamic c in civilDoc.Corridors)
                {
                    sheet.Cells[row, 1] = c.Name;
                    try { sheet.Cells[row, 2] = c.Baselines.Count; } catch { }
                    try { sheet.Cells[row, 3] = c.CorridorSurfaces.Count; } catch { }
                    row++;
                }
            }
            catch { }

            sheet.Columns.AutoFit();
        }

        private static void CreatePipeNetworksSheet(dynamic workbook)
        {
            dynamic sheet = workbook.Sheets.Add();
            sheet.Name = "Redes_Tubulacao";

            sheet.Cells[1, 1] = "Nome";
            sheet.Cells[1, 2] = "Tubos";
            sheet.Cells[1, 3] = "Estruturas";
            sheet.Range["A1:C1"].Font.Bold = true;

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                dynamic civilApp = acadApp.GetInterfaceObject("AeccXUiLand.AeccApplication.14.0");
                dynamic civilDoc = civilApp.ActiveDocument;
                int row = 2;
                foreach (dynamic p in civilDoc.PipeNetworks)
                {
                    sheet.Cells[row, 1] = p.Name;
                    try { sheet.Cells[row, 2] = p.Pipes.Count; } catch { }
                    try { sheet.Cells[row, 3] = p.Structures.Count; } catch { }
                    row++;
                }
            }
            catch { }

            sheet.Columns.AutoFit();
        }
    }
}
