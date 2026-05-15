using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace C3DDeepSeek
{
    /// <summary>
    /// Importador de pontos — Google Maps (KML/CSV), GPS, WebODM.
    /// Converte coordenadas geográficas ou UTM e cria COGO Points no Civil 3D.
    /// </summary>
    public static class GoogleMapsImporter
    {
        /// <summary>
        /// Importa pontos de arquivo KML do Google Maps/Google Earth
        /// </summary>
        public static string ImportKmlPoints(string filePath)
        {
            if (!File.Exists(filePath))
                return $"❌ Arquivo não encontrado: {filePath}";

            try
            {
                string kmlContent = File.ReadAllText(filePath, Encoding.UTF8);

                // Extrai coordenadas do KML (formato: lon,lat,z ou lon,lat)
                var coords = new List<(double lon, double lat, double z, string name)>();

                // Busca por <coordinates> tags
                int pos = 0;
                while (true)
                {
                    int startTag = kmlContent.IndexOf("<coordinates>", pos, StringComparison.OrdinalIgnoreCase);
                    if (startTag < 0) break;

                    int endTag = kmlContent.IndexOf("</coordinates>", startTag, StringComparison.OrdinalIgnoreCase);
                    if (endTag < 0) break;

                    string coordBlock = kmlContent.Substring(startTag + 13, endTag - startTag - 13).Trim();

                    // Busca nome do placemark
                    string name = "Ponto_KML";
                    int nameStart = kmlContent.LastIndexOf("<name>", startTag);
                    int nameEnd = kmlContent.IndexOf("</name>", nameStart);
                    if (nameStart > 0 && nameEnd > nameStart)
                        name = kmlContent.Substring(nameStart + 6, nameEnd - nameStart - 6).Trim();

                    // Parse coordenadas
                    foreach (var line in coordBlock.Split('\n', '\r'))
                    {
                        var parts = line.Trim().Split(',');
                        if (parts.Length >= 2)
                        {
                            if (double.TryParse(parts[0].Trim().Replace(".", ","),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.CurrentCulture, out double lon) &&
                                double.TryParse(parts[1].Trim().Replace(".", ","),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.CurrentCulture, out double lat))
                            {
                                double z = 0;
                                if (parts.Length >= 3)
                                    double.TryParse(parts[2].Trim().Replace(".", ","),
                                        System.Globalization.NumberStyles.Any,
                                        System.Globalization.CultureInfo.CurrentCulture, out z);

                                coords.Add((lon, lat, z, name));
                            }
                        }
                    }

                    pos = endTag + 14;
                }

                if (coords.Count == 0)
                    return "❌ Nenhuma coordenada encontrada no KML.";

                // Converte para UTM e cria COGO points
                return CreateCogoPoints(coords, filePath);
            }
            catch (Exception ex)
            {
                return $"❌ Erro ao processar KML: {ex.Message}";
            }
        }

        /// <summary>
        /// Importa pontos de CSV com coordenadas (UTM ou Lat/Lon)
        /// Formato: Lat,Lon,Elev,Desc ou E,N,Z,Desc
        /// </summary>
        public static string ImportCsvAsCogo(string filePath, bool isGeo = false)
        {
            if (!File.Exists(filePath))
                return $"❌ Arquivo não encontrado: {filePath}";

            try
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                if (lines.Length < 2)
                    return "❌ CSV vazio.";

                var coords = new List<(double lon, double lat, double z, string name)>();
                int count = 0;

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split(',', ';', '\t');
                    if (parts.Length >= 3)
                    {
                        if (double.TryParse(parts[0].Trim().Replace(".", ","),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.CurrentCulture, out double v1) &&
                            double.TryParse(parts[1].Trim().Replace(".", ","),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.CurrentCulture, out double v2))
                        {
                            double v3 = 0;
                            if (parts.Length >= 3)
                                double.TryParse(parts[2].Trim().Replace(".", ","),
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.CurrentCulture, out v3);

                            string desc = parts.Length >= 4 ? parts[3].Trim() : $"PONTO_{count + 1}";

                            if (isGeo)
                                coords.Add((v2, v1, v3, desc)); // lon, lat (Geo)
                            else
                                coords.Add((v1, v2, v3, desc)); // E, N (UTM)
                        }
                    }
                    count++;
                }

                if (coords.Count == 0)
                    return "❌ Nenhuma coordenada válida no CSV.";

                return CreateCogoPoints(coords, filePath, !isGeo);
            }
            catch (Exception ex)
            {
                return $"❌ Erro ao processar CSV: {ex.Message}";
            }
        }

        /// <summary>
        /// Cria pontos COGO no Civil 3D a partir de coordenadas
        /// </summary>
        private static string CreateCogoPoints(List<(double lon, double lat, double z, string name)> coords,
            string sourcePath, bool isUtm = false)
        {
            try
            {
                int created = 0;

                foreach (var coord in coords)
                {
                    double easting, northing;

                    if (isUtm)
                    {
                        easting = coord.lon;
                        northing = coord.lat;
                    }
                    else
                    {
                        // Converte Geo → UTM (simplificado)
                        var utm = GeoToUtmSimple(coord.lat, coord.lon);
                        easting = utm.easting;
                        northing = utm.northing;
                    }

                    string safeName = coord.name.Replace("\"", "").Replace("'", "");

                    // Cria COGO Point via comando
                    DeepSeekEngine.SendToAutoCAD(
                        $"(command \"_AeccCreatePoint\" " +
                        $"\"{easting:0.000},{northing:0.000}\" " +
                        $"\"{coord.z:0.000}\" " +
                        $"\"{safeName}\")");

                    created++;
                    if (created >= 5000) break; // segurança
                }

                return $"✅ {created} pontos COGO criados com sucesso!\n" +
                       $"📁 Origem: {Path.GetFileName(sourcePath)}\n" +
                       $"🌍 Coordenadas: {(isUtm ? "UTM" : "Geográficas (convertidas para UTM)")}";
            }
            catch (Exception ex)
            {
                return $"❌ Erro ao criar pontos: {ex.Message}";
            }
        }

        /// <summary>
        /// Conversão simplificada Geo → UTM
        /// </summary>
        private static (double easting, double northing) GeoToUtmSimple(double lat, double lon)
        {
            int fuso = (int)Math.Floor((lon + 180) / 6) + 1;

            double a = 6378137.0;
            double e2 = 0.00669438;
            double k0 = 0.9996;

            double latRad = lat * Math.PI / 180.0;
            double lonRad = lon * Math.PI / 180.0;
            double lon0 = (fuso * 6 - 183) * Math.PI / 180.0;

            double N = a / Math.Sqrt(1 - e2 * Math.Sin(latRad) * Math.Sin(latRad));
            double T = Math.Tan(latRad) * Math.Tan(latRad);
            double C = e2 / (1 - e2) * Math.Cos(latRad) * Math.Cos(latRad);
            double A = Math.Cos(latRad) * (lonRad - lon0);

            double M = a * ((1 - e2 / 4 - 3 * e2 * e2 / 64 - 5 * e2 * e2 * e2 / 256) * latRad
                - (3 * e2 / 8 + 3 * e2 * e2 / 32 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(2 * latRad)
                + (15 * e2 * e2 / 256 + 45 * e2 * e2 * e2 / 1024) * Math.Sin(4 * latRad)
                - (35 * e2 * e2 * e2 / 3072) * Math.Sin(6 * latRad));

            double easting = k0 * N * (A + (1 - T + C) * A * A * A / 6
                + (5 - 18 * T + T * T + 72 * C - 58 * e2) * A * A * A * A * A / 120) + 500000;

            double northing = k0 * (M + N * Math.Tan(latRad) * (A * A / 2
                + (5 - T + 9 * C + 4 * C * C) * A * A * A * A / 24
                + (61 - 58 * T + T * T + 600 * C - 330 * e2) * A * A * A * A * A * A / 720));

            if (lat < 0) northing += 10000000;

            return (easting, northing);
        }

        /// <summary>
        /// Abre diálogo para selecionar arquivo e importa automaticamente
        /// </summary>
        public static string SmartImport()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Importar pontos (Google Maps/Earth, GPS, CSV)",
                    Filter = "Todos suportados|*.kml;*.kmz;*.csv;*.txt;*.gpx|Google Earth (*.kml)|*.kml|Google Earth (*.kmz)|*.kmz|CSV (*.csv)|*.csv|GPX (*.gpx)|*.gpx",
                    FilterIndex = 1
                };

                if (dlg.ShowDialog() == true)
                {
                    string ext = Path.GetExtension(dlg.FileName).ToLower();

                    switch (ext)
                    {
                        case ".kml":
                        case ".kmz":
                            return ImportKmlPoints(dlg.FileName);
                        case ".csv":
                        case ".txt":
                            // Pergunta se é coordenada geográfica ou UTM
                            return ImportCsvAsCogo(dlg.FileName, false);
                        default:
                            return $"❌ Formato não suportado: {ext}";
                    }
                }

                return "Importação cancelada.";
            }
            catch (Exception ex)
            {
                return $"❌ Erro: {ex.Message}";
            }
        }
    }
}
