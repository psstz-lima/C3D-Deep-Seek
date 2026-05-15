using System;
using System.IO;
using System.Text;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Importação de dados para o Civil 3D:
    /// CSV (pontos), LandXML, Shapefile, KML.
    /// </summary>
    public static class DataImporter
    {
        /// <summary>
        /// Importa arquivo de pontos CSV (formato: X,Y,Z,Descrição)
        /// </summary>
        public static string ImportCsvPoints(string filePath)
        {
            if (!File.Exists(filePath))
                return $"❌ Arquivo não encontrado: {filePath}";

            try
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length < 2)
                    return "❌ CSV vazio ou sem dados.";

                int count = 0;
                foreach (var line in lines)
                {
                    var parts = line.Split(',', ';', '\t');
                    if (parts.Length >= 3)
                    {
                        // Formato: X,Y,Z[,Descrição]
                        string x = parts[0].Trim();
                        string y = parts[1].Trim();
                        string z = parts[2].Trim();
                        string desc = parts.Length > 3 ? parts[3].Trim() : "PONTO_CSV";

                        // Cria ponto COGO via comando
                        DeepSeekEngine.SendToAutoCAD(
                            $"(command \"_.AeccCreatePoint\" \"{x},{y}\" \"{z}\" \"{desc}\")"
                        );
                        count++;
                    }
                }

                return $"✅ Importados {count} pontos do CSV.\nArquivo: {filePath}";
            }
            catch (Exception ex)
            {
                return $"❌ Erro ao importar CSV: {ex.Message}";
            }
        }

        /// <summary>
        /// Importa arquivo LandXML via comando nativo do Civil 3D
        /// </summary>
        public static string ImportLandXml(string filePath)
        {
            if (!File.Exists(filePath))
                return $"❌ Arquivo não encontrado: {filePath}";

            try
            {
                DeepSeekEngine.SendToAutoCAD($"_.-LANDXMLIN \"{filePath}\"");
                return $"✅ Importação LandXML iniciada.\nArquivo: {filePath}";
            }
            catch (Exception ex)
            {
                return $"❌ Erro ao importar LandXML: {ex.Message}";
            }
        }

        /// <summary>
        /// Importa arquivo SHP (Shapefile) via comando MAPIMPORT do AutoCAD Map 3D
        /// </summary>
        public static string ImportShapefile(string filePath)
        {
            if (!File.Exists(filePath))
                return $"❌ Arquivo não encontrado: {filePath}";

            try
            {
                DeepSeekEngine.SendToAutoCAD($"_.MAPIMPORT \"{filePath}\"");
                return $"✅ Importação Shapefile iniciada.\nArquivo: {filePath}";
            }
            catch (Exception ex)
            {
                return $"❌ Erro ao importar SHP: {ex.Message}";
            }
        }

        /// <summary>
        /// Importa KML/KMZ via comando MAPIMPORT
        /// </summary>
        public static string ImportKml(string filePath)
        {
            if (!File.Exists(filePath))
                return $"❌ Arquivo não encontrado: {filePath}";

            try
            {
                DeepSeekEngine.SendToAutoCAD($"_.MAPIMPORT \"{filePath}\"");
                return $"✅ Importação KML/KMZ iniciada.\nArquivo: {filePath}";
            }
            catch (Exception ex)
            {
                return $"❌ Erro ao importar KML: {ex.Message}";
            }
        }

        /// <summary>
        /// Importa arquivo IFC
        /// </summary>
        public static string ImportIfc(string filePath)
        {
            if (!File.Exists(filePath))
                return $"❌ Arquivo não encontrado: {filePath}";

            try
            {
                DeepSeekEngine.SendToAutoCAD($"_.-IMPORTIFC \"{filePath}\"");
                return $"✅ Importação IFC iniciada.\nArquivo: {filePath}";
            }
            catch (Exception ex)
            {
                return $"❌ Erro ao importar IFC: {ex.Message}";
            }
        }

        /// <summary>
        /// Detecta o tipo de arquivo e importa adequadamente
        /// </summary>
        public static string SmartImport(string filePath)
        {
            if (!File.Exists(filePath))
                return $"❌ Arquivo não encontrado: {filePath}";

            var ext = Path.GetExtension(filePath).ToUpper();

            switch (ext)
            {
                case ".CSV":
                case ".TXT":
                    return ImportCsvPoints(filePath);
                case ".XML":
                    return ImportLandXml(filePath);
                case ".SHP":
                    return ImportShapefile(filePath);
                case ".KML":
                case ".KMZ":
                    return ImportKml(filePath);
                case ".IFC":
                    return ImportIfc(filePath);
                case ".DWG":
                case ".DXF":
                    DeepSeekEngine.SendToAutoCAD($"_.OPEN \"{filePath}\"");
                    return $"✅ Abrindo arquivo DWG/DXF: {filePath}";
                default:
                    return $"❌ Formato não suportado: {ext}\n" +
                           "Suportados: CSV, XML (LandXML), SHP, KML/KMZ, IFC, DWG, DXF";
            }
        }
    }
}
