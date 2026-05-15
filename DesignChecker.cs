using System;
using System.Collections.Generic;
using System.Text;

namespace C3DDeepSeek
{
    /// <summary>
    /// Verificador de conformidade com normas DNIT, AASHTO e DNER.
    /// Valida raios, rampas, superelevação, visibilidade e gabaritos.
    /// </summary>
    public static class DesignChecker
    {
        public class CheckItem
        {
            public string Parameter { get; set; } = "";
            public double ActualValue { get; set; }
            public double MinValue { get; set; }
            public double MaxValue { get; set; }
            public string Unit { get; set; } = "";
            public bool Passed { get; set; }
            public string Norm { get; set; } = "";
        }

        /// <summary>
        /// Verifica parâmetros de projeto conforme velocidade diretriz
        /// </summary>
        public static string CheckByDesignSpeed(double speedKmh)
        {
            var sb = new StringBuilder();
            var checks = new List<CheckItem>();

            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"  📏 VERIFICAÇÃO DE NORMAS — V={speedKmh} km/h");
            sb.AppendLine("═══════════════════════════════════════");

            // Raios mínimos (DNIT)
            double eMax = 8.0;  // superelevação máxima (%)
            double fMax = 0.14; // atrito máximo
            double rMin = speedKmh * speedKmh / (127 * (eMax / 100 + fMax));

            checks.Add(new CheckItem
            {
                Parameter = "Raio mínimo (curva horizontal)",
                MinValue = rMin,
                Unit = "m",
                Norm = "DNIT 001/2009"
            });

            // Rampa máxima
            double rampMax = speedKmh switch
            {
                >= 120 => 3.0,
                >= 100 => 4.0,
                >= 80 => 5.0,
                >= 60 => 6.0,
                _ => 7.0
            };

            checks.Add(new CheckItem
            {
                Parameter = "Rampa máxima (greide)",
                MaxValue = rampMax,
                Unit = "%",
                Norm = "DNIT"
            });

            // Distância de visibilidade
            double dpr = 0.7 * speedKmh;
            double df = speedKmh * speedKmh / (254 * 0.35);
            double dVis = dpr + df;

            checks.Add(new CheckItem
            {
                Parameter = "Distância de visibilidade de parada",
                MinValue = dVis,
                Unit = "m",
                Norm = "DNIT"
            });

            // Superelevação
            checks.Add(new CheckItem
            {
                Parameter = "Superelevação máxima",
                MaxValue = eMax,
                Unit = "%",
                Norm = "DNIT"
            });

            // Largura de faixa
            double laneWidth = speedKmh >= 80 ? 3.6 : 3.5;
            checks.Add(new CheckItem
            {
                Parameter = "Largura de faixa recomendada",
                MinValue = laneWidth,
                Unit = "m",
                Norm = "DNIT"
            });

            // K mínimo (parábola vertical)
            double kMin = speedKmh switch
            {
                >= 120 => 150,
                >= 100 => 100,
                >= 80 => 60,
                >= 60 => 35,
                _ => 20
            };
            checks.Add(new CheckItem
            {
                Parameter = "Parâmetro K mínimo (curva vertical)",
                MinValue = kMin,
                Unit = "m/%",
                Norm = "DNIT"
            });

            // Exibe resultados
            sb.AppendLine("\n📋 PARÂMETROS MÍNIMOS DE PROJETO:");
            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine($"{"Parâmetro",-42} {"Mín/Máx",-12} {"Unid.",-6}");
            sb.AppendLine("───────────────────────────────────────────");

            foreach (var c in checks)
            {
                string value = c.MinValue > 0
                    ? $"≥ {c.MinValue:0.0}"
                    : $"≤ {c.MaxValue:0.0}";
                sb.AppendLine($"{c.Parameter,-42} {value,-12} {c.Unit,-6}");
            }

            sb.AppendLine("───────────────────────────────────────────");
            sb.AppendLine($"📚 Norma de referência: {checks[0].Norm}");
            sb.AppendLine("\n💡 Use DSCLASH para verificar interferências no projeto atual.");
            sb.AppendLine("═══════════════════════════════════════");

            return sb.ToString();
        }

        /// <summary>
        /// Verifica classificação da via e retorna parâmetros
        /// </summary>
        public static string GetRoadClassification(double vdm)
        {
            string classe = vdm switch
            {
                >= 20000 => "Classe 0 (Via Expressa — Pista Dupla)",
                >= 10000 => "Classe I-A (Pista Dupla)",
                >= 5000 => "Classe I-B (Pista Simples)",
                >= 1200 => "Classe II (Pista Simples)",
                >= 400 => "Classe III (Pista Simples)",
                _ => "Classe IV (Pista Simples)"
            };

            double speed = vdm switch
            {
                >= 20000 => 120,
                >= 10000 => 100,
                >= 5000 => 100,
                >= 1200 => 80,
                >= 400 => 60,
                _ => 40
            };

            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine("  🛣️ CLASSIFICAÇÃO DA VIA (DNIT)");
            sb.AppendLine("═══════════════════════════════════════");
            sb.AppendLine($"VDM: {vdm:N0} veíc/dia");
            sb.AppendLine($"Classificação: {classe}");
            sb.AppendLine($"Velocidade diretriz: {speed} km/h");
            sb.AppendLine("═══════════════════════════════════════");

            return sb.ToString();
        }
    }
}
