using System;
using System.Text;

namespace C3DDeepSeek
{
    /// <summary>
    /// Transformação de coordenadas: UTM ↔ Topográficas, SIRGAS2000 ↔ WGS84,
    /// conversão de datum e georreferenciamento.
    /// </summary>
    public static class CoordinateTransformer
    {
        /// <summary>
        /// Converte UTM para coordenadas topográficas locais
        /// </summary>
        public static string UtmToTopographic(double easting, double northing, double azimuthOrigin = 0,
            double xOrigin = 0, double yOrigin = 0, double scaleFactor = 1.0)
        {
            // Translação + rotação
            double dx = easting - xOrigin;
            double dy = northing - yOrigin;

            double theta = azimuthOrigin * Math.PI / 180.0; // graus → radianos

            double xTopo = dx * Math.Cos(theta) + dy * Math.Sin(theta);
            double yTopo = -dx * Math.Sin(theta) + dy * Math.Cos(theta);

            xTopo *= scaleFactor;
            yTopo *= scaleFactor;

            var sb = new StringBuilder();
            sb.AppendLine("🗺️ UTM → TOPOGRÁFICO LOCAL");
            sb.AppendLine("───────────────────────────────────");
            sb.AppendLine($"Origem: E={xOrigin:0.000} N={yOrigin:0.000}");
            sb.AppendLine($"Azimute do eixo: {azimuthOrigin}°");
            sb.AppendLine($"Fator escala: {scaleFactor:0.0000}");
            sb.AppendLine("───────────────────────────────────");
            sb.AppendLine($"UTM: E={easting:0.000} N={northing:0.000}");
            sb.AppendLine($"Topo: X={xTopo:0.000} Y={yTopo:0.000}");
            sb.AppendLine("───────────────────────────────────");
            sb.AppendLine("💡 Use DSTRANSFORM para definir no projeto.");

            return sb.ToString();
        }

        /// <summary>
        /// Converte Topográfico Local → UTM
        /// </summary>
        public static string TopographicToUtm(double xTopo, double yTopo, double azimuthOrigin = 0,
            double xOrigin = 0, double yOrigin = 0, double scaleFactor = 1.0)
        {
            double theta = azimuthOrigin * Math.PI / 180.0;

            double xLocal = xTopo / scaleFactor;
            double yLocal = yTopo / scaleFactor;

            double dx = xLocal * Math.Cos(theta) - yLocal * Math.Sin(theta);
            double dy = xLocal * Math.Sin(theta) + yLocal * Math.Cos(theta);

            double easting = xOrigin + dx;
            double northing = yOrigin + dy;

            var sb = new StringBuilder();
            sb.AppendLine("🗺️ TOPOGRÁFICO → UTM");
            sb.AppendLine("───────────────────────────────────");
            sb.AppendLine($"Topo: X={xTopo:0.000} Y={yTopo:0.000}");
            sb.AppendLine($"UTM: E={easting:0.000} N={northing:0.000}");
            sb.AppendLine($"Fuso: {(int)(easting / 1000000)}");
            sb.AppendLine("───────────────────────────────────");
            return sb.ToString();
        }

        /// <summary>
        /// Converte coordenadas geográficas para UTM (simplificado para Brasil)
        /// </summary>
        public static string GeoToUtm(double lat, double lon)
        {
            // Simplificado — calcula fuso UTM e coordenadas aproximadas
            int fuso = (int)Math.Floor((lon + 180) / 6) + 1;

            // Constantes do elipsoide WGS84
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

            var sb = new StringBuilder();
            sb.AppendLine("🌍 GEOGRÁFICO → UTM (WGS84)");
            sb.AppendLine("───────────────────────────────────");
            sb.AppendLine($"Lat: {lat:0.000000}° | Lon: {lon:0.000000}°");
            sb.AppendLine($"Fuso UTM: {fuso}");
            sb.AppendLine($"Easting: {easting:0.000}m");
            sb.AppendLine($"Northing: {northing:0.000}m");
            sb.AppendLine("───────────────────────────────────");
            return sb.ToString();
        }

        /// <summary>
        /// Define sistema de coordenadas no desenho atual
        /// </summary>
        public static string SetCoordinateSystem(string epsgCode)
        {
            DeepSeekEngine.SendToAutoCAD(
                $"(command \"_AeccSetCoordinateSystem\" \"{epsgCode}\")");

            return $"✅ Sistema de coordenadas EPSG:{epsgCode} definido.";
        }

        /// <summary>
        /// Lista sistemas comuns no Brasil
        /// </summary>
        public static string ListBrazilSystems()
        {
            var sb = new StringBuilder();
            sb.AppendLine("🗺️ SISTEMAS DE COORDENADAS — BRASIL");
            sb.AppendLine("───────────────────────────────────────");
            sb.AppendLine("SIRGAS 2000:");
            sb.AppendLine("  EPSG:31965 — UTM 18S (Amazonas)");
            sb.AppendLine("  EPSG:31966 — UTM 19S (Pará/MT)");
            sb.AppendLine("  EPSG:31967 — UTM 20S (Nordeste/TO/GO)");
            sb.AppendLine("  EPSG:31968 — UTM 21S (BA/MG/ES)");
            sb.AppendLine("  EPSG:31969 — UTM 22S (SP/RJ/PR/SC)");
            sb.AppendLine("  EPSG:31970 — UTM 23S (RS)");
            sb.AppendLine("  EPSG:31971 — UTM 24S (Fernando de Noronha)");
            sb.AppendLine("  EPSG:31972 — UTM 25S (Ilhas)");
            sb.AppendLine("WGS 84:");
            sb.AppendLine("  EPSG:4326 — Geográfico WGS84");
            sb.AppendLine("───────────────────────────────────────");
            return sb.ToString();
        }
    }
}
