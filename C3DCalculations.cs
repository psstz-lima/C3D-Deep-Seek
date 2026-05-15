using System;
using System.Collections.Generic;
using System.Text;
using AcAp = Autodesk.AutoCAD.ApplicationServices;

namespace C3DDeepSeek
{
    /// <summary>
    /// Módulo de cálculos de engenharia:
    /// Hidráulica (Manning, vazão, dimensionamento), Pavimento (DNER, espessuras),
    /// Terraplenagem (volumes, empolamento), Drenagem, Sinalização e Tráfego.
    /// </summary>
    public static class C3DCalculations
    {
        public class CalcResult
        {
            public bool Success { get; set; }
            public string Result { get; set; } = "";
            public Dictionary<string, double> Values { get; set; } = new Dictionary<string, double>();
        }

        /// <summary>
        /// Roteador principal — interpreta o tipo de cálculo solicitado
        /// </summary>
        public static CalcResult Execute(string calcCommand)
        {
            var parts = calcCommand.Split(':');
            if (parts.Length < 2)
                return new CalcResult { Success = false, Result = "Formato: CALC:TIPO param1=valor1 param2=valor2" };

            var op = parts[1].Trim().ToUpper();
            var paramsStr = parts.Length > 2 ? parts[2] : "";
            var p = ParseParams(paramsStr);

            switch (op)
            {
                // Hidráulica
                case "MANNING": return CalcManning(p);
                case "VAZAO": return CalcVazao(p);
                case "DIAMETRO_TUBO": return CalcDiametroTubo(p);
                case "GALERIA": return CalcGaleria(p);
                case "BOCA_LOBO": return CalcBocaLobo(p);
                case "SARJETA": return CalcSarjeta(p);
                case "CANAL": return CalcCanal(p);

                // Pavimento
                case "PAV_DNER": return CalcPavimentoDNER(p);
                case "PAV_AASTHO": return CalcPavimentoAASHTO(p);
                case "CAIG": return CalcCAIG(p);
                case "ESPESSURAS": return CalcEspessuras(p);

                // Terraplenagem
                case "VOLUME_CORTE": return CalcVolumeCorteAterro(p);
                case "EMPOLAMENTO": return CalcEmpolamento(p);
                case "COMPACTACAO": return CalcCompactacao(p);
                case "FATOR_HOMOG": return CalcFatorHomogeneizacao(p);

                // Drenagem
                case "TEMPO_CONC": return CalcTempoConcentracao(p);
                case "INTENSIDADE": return CalcIntensidadeChuva(p);
                case "BACIA": return CalcBacia(p);

                // Sinalização
                case "DIST_FRENAGEM": return CalcDistFrenagem(p);
                case "SINALIZACAO": return CalcSinalizacao(p);

                // Tráfego
                case "CAPACIDADE": return CalcCapacidade(p);
                case "NIVEL_SERVICO": return CalcNivelServico(p);

                // Notas de serviço
                case "NOTA_TERRAPLENAGEM": return CalcNotaTerraplenagem(p);
                case "NOTA_PAVIMENTO": return CalcNotaPavimento(p);

                default:
                    return new CalcResult
                    {
                        Success = false,
                        Result = $"Cálculo '{op}' não reconhecido.\n" +
                                  "Disponíveis: MANNING, VAZAO, DIAMETRO_TUBO, GALERIA, BOCA_LOBO, SARJETA, CANAL, " +
                                  "PAV_DNER, PAV_AASTHO, CAIG, ESPESSURAS, " +
                                  "VOLUME_CORTE, EMPOLAMENTO, COMPACTACAO, FATOR_HOMOG, " +
                                  "TEMPO_CONC, INTENSIDADE, BACIA, " +
                                  "DIST_FRENAGEM, SINALIZACAO, CAPACIDADE, NIVEL_SERVICO, " +
                                  "NOTA_TERRAPLENAGEM, NOTA_PAVIMENTO"
                    };
            }
        }

        private static Dictionary<string, double> ParseParams(string paramsStr)
        {
            var dict = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(paramsStr)) return dict;

            foreach (var item in paramsStr.Split(' '))
            {
                var kv = item.Split('=');
                if (kv.Length == 2 && double.TryParse(kv[1].Replace(".", ","),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.CurrentCulture, out double val))
                    dict[kv[0].Trim()] = val;
            }
            return dict;
        }

        // ═══════════════════════════════════════════
        // HIDRÁULICA
        // ═══════════════════════════════════════════

        /// <summary>
        /// Equação de Manning: V = (1/n) * R^(2/3) * S^(1/2)
        /// </summary>
        private static CalcResult CalcManning(Dictionary<string, double> p)
        {
            double n = p.ContainsKey("n") ? p["n"] : 0.013;  // rugosidade (concreto=0.013)
            double diam = p.ContainsKey("d") ? p["d"] : 1.0;   // diâmetro (m)
            double s = p.ContainsKey("s") ? p["s"] : 0.01;     // declividade (m/m)
            double y = p.ContainsKey("y") ? p["y"] : diam;     // altura lâmina d'água

            double r = diam / 4.0;  // raio hidráulico (tubo cheio)
            double v = (1.0 / n) * Math.Pow(r, 2.0 / 3.0) * Math.Pow(s, 0.5);
            double area = Math.PI * diam * diam / 4.0;
            double q = v * area;  // vazão m³/s

            return new CalcResult
            {
                Success = true,
                Result = $"📐 CÁLCULO DE MANNING\n" +
                          $"─────────────────────────────\n" +
                          $"Rugosidade (n): {n:0.000}\n" +
                          $"Diâmetro: {diam:0.000} m\n" +
                          $"Declividade: {s * 100:0.00}%\n" +
                          $"Raio hidráulico: {r:0.000} m\n" +
                          $"─────────────────────────────\n" +
                          $"Velocidade: {v:0.00} m/s\n" +
                          $"Vazão (tubo cheio): {q * 1000:0.0} L/s\n" +
                          $"─────────────────────────────\n" +
                          $"⚠️ Vmín=0.60 m/s, Vmáx=5.0 m/s (concreto)",
                Values = new Dictionary<string, double> { ["V"] = v, ["Q"] = q, ["R"] = r }
            };
        }

        /// <summary>
        /// Vazão pelo método racional: Q = C * I * A / 360
        /// </summary>
        private static CalcResult CalcVazao(Dictionary<string, double> p)
        {
            double c = p.ContainsKey("c") ? p["c"] : 0.7;     // coeficiente runoff
            double i = p.ContainsKey("i") ? p["i"] : 100.0;    // intensidade mm/h
            double a = p.ContainsKey("a") ? p["a"] : 1.0;      // área hectares

            double q = c * i * a / 360.0;  // m³/s

            return new CalcResult
            {
                Success = true,
                Result = $"🌧️ VAZÃO — MÉTODO RACIONAL\n" +
                          $"─────────────────────────────\n" +
                          $"Coef. runoff (C): {c:0.00}\n" +
                          $"Intensidade (I): {i:0.0} mm/h\n" +
                          $"Área da bacia: {a:0.00} ha\n" +
                          $"─────────────────────────────\n" +
                          $"Vazão de pico: {q * 1000:0.0} L/s ({q:0.000} m³/s)",
                Values = new Dictionary<string, double> { ["Q"] = q }
            };
        }

        private static CalcResult CalcDiametroTubo(Dictionary<string, double> p)
        {
            double q = p.ContainsKey("q") ? p["q"] : 0.1;     // vazão m³/s
            double s = p.ContainsKey("s") ? p["s"] : 0.01;     // declividade
            double n = p.ContainsKey("n") ? p["n"] : 0.013;    // Manning

            // D = (Q * n / (k * S^0.5))^(3/8), k = π/4^(5/3) ≈ 0.3117
            double k = 0.3117;
            double d = Math.Pow(q * n / (k * Math.Pow(s, 0.5)), 3.0 / 8.0);
            double dComercial = Math.Ceiling(d * 100) / 100;  // arredonda para cm

            return new CalcResult
            {
                Success = true,
                Result = $"🔧 DIMENSIONAMENTO DE TUBULAÇÃO\n" +
                          $"─────────────────────────────\n" +
                          $"Vazão: {q * 1000:0.0} L/s\n" +
                          $"Declividade: {s * 100:0.00}%\n" +
                          $"Rugosidade (n): {n:0.000}\n" +
                          $"─────────────────────────────\n" +
                          $"Diâmetro calculado: {d * 100:0.0} cm\n" +
                          $"Diâmetro comercial: {dComercial * 100:0.0} cm ({dComercial * 1000:0} mm)",
                Values = new Dictionary<string, double> { ["D"] = d, ["D_comercial"] = dComercial }
            };
        }

        private static CalcResult CalcGaleria(Dictionary<string, double> p)
        {
            double q = p.ContainsKey("q") ? p["q"] : 0.5;
            double s = p.ContainsKey("s") ? p["s"] : 0.005;
            double n = p.ContainsKey("n") ? p["n"] : 0.015;

            // Galeria retangular: dimensiona base e altura
            double b = p.ContainsKey("b") ? p["b"] : 1.0;

            var sb = new StringBuilder();
            sb.AppendLine("🏗️ DIMENSIONAMENTO DE GALERIA");
            sb.AppendLine("─────────────────────────────");
            sb.AppendLine($"Vazão: {q * 1000:0.0} L/s");
            sb.AppendLine($"Declividade: {s * 100:0.00}%");
            sb.AppendLine($"Base sugerida: {b:0.00} m");
            sb.AppendLine("─────────────────────────────");
            sb.AppendLine("⚠️ Dimensionamento completo requer");
            sb.AppendLine("   iteração hidráulica. Sugestão:");
            sb.AppendLine($"   Use software dedicado (HEC-RAS, SWMM)");
            sb.AppendLine("   ou consulte manual de drenagem DNIT.");

            return new CalcResult { Success = true, Result = sb.ToString() };
        }

        private static CalcResult CalcBocaLobo(Dictionary<string, double> p)
        {
            double q = p.ContainsKey("q") ? p["q"] : 0.02;
            double s = p.ContainsKey("s") ? p["s"] : 0.03;

            // Capacidade de engolimento simplificada
            double l = p.ContainsKey("l") ? p["l"] : 0.8;  // comprimento (m)
            double cap = 1.7 * l * Math.Pow(q, 0.5) * Math.Pow(s, 0.5) * 1000;

            return new CalcResult
            {
                Success = true,
                Result = $"🌊 BOCA DE LOBO — CAPACIDADE\n" +
                          $"─────────────────────────────\n" +
                          $"Comprimento: {l:0.00} m\n" +
                          $"Declividade: {s * 100:0.0}%\n" +
                          $"─────────────────────────────\n" +
                          $"Capacidade estimada: {cap:0.0} L/s\n" +
                          $"Tipo sugerido: {(cap > 50 ? "Dupla" : "Simples")}",
                Values = new Dictionary<string, double> { ["Capacidade"] = cap }
            };
        }

        private static CalcResult CalcSarjeta(Dictionary<string, double> p)
        {
            double sLong = p.ContainsKey("sl") ? p["sl"] : 0.01;
            double sTrans = p.ContainsKey("st") ? p["st"] : 0.03;
            double n = p.ContainsKey("n") ? p["n"] : 0.016;

            // Izzard: capacidade da sarjeta
            double z = 1.0 / sTrans;  // inverso da declividade transversal
            double y = p.ContainsKey("y") ? p["y"] : 0.10;  // lâmina máxima

            double q = 0.375 * (1.0 / n) * Math.Pow(sLong, 0.5) * Math.Pow(y, 8.0 / 3.0) * Math.Pow(z, 1.0 / 3.0);

            return new CalcResult
            {
                Success = true,
                Result = $"🛣️ SARJETA — CAPACIDADE\n" +
                          $"─────────────────────────────\n" +
                          $"Decliv. longitudinal: {sLong * 100:0.0}%\n" +
                          $"Decliv. transversal: {sTrans * 100:0.0}%\n" +
                          $"Lâmina máx.: {y * 100:0.0} cm\n" +
                          $"─────────────────────────────\n" +
                          $"Capacidade: {q * 1000:0.0} L/s",
                Values = new Dictionary<string, double> { ["Q"] = q }
            };
        }

        private static CalcResult CalcCanal(Dictionary<string, double> p)
        {
            double b = p.ContainsKey("b") ? p["b"] : 1.0;
            double y = p.ContainsKey("y") ? p["y"] : 0.5;
            double s = p.ContainsKey("s") ? p["s"] : 0.001;
            double n = p.ContainsKey("n") ? p["n"] : 0.025;

            double area = b * y;
            double per = b + 2 * y;
            double rh = area / per;
            double v = (1.0 / n) * Math.Pow(rh, 2.0 / 3.0) * Math.Pow(s, 0.5);
            double q = v * area;

            return new CalcResult
            {
                Success = true,
                Result = $"🏞️ CANAL RETANGULAR — MANNING\n" +
                          $"─────────────────────────────\n" +
                          $"Base: {b:0.00}m | Lâmina: {y:0.00}m\n" +
                          $"Área molhada: {area:0.00}m²\n" +
                          $"Raio hidráulico: {rh:0.000}m\n" +
                          $"─────────────────────────────\n" +
                          $"Velocidade: {v:0.00}m/s\n" +
                          $"Vazão: {q * 1000:0.0}L/s",
                Values = new Dictionary<string, double> { ["V"] = v, ["Q"] = q }
            };
        }

        // ═══════════════════════════════════════════
        // PAVIMENTO
        // ═══════════════════════════════════════════

        private static CalcResult CalcPavimentoDNER(Dictionary<string, double> p)
        {
            double n = p.ContainsKey("n") ? p["n"] : 1e6;  // número N (operações eixo padrão)

            // Método DNER: espessuras baseadas no N e CBR
            double cbrSubleito = p.ContainsKey("cbr") ? p["cbr"] : 5.0;

            // Espessura total sobre subleito (HS = espessura necessária)
            double hs = 77.67 * Math.Pow(n, 0.0482) * Math.Pow(cbrSubleito, -0.598);
            hs = Math.Max(hs, 15); // mínimo 15cm

            return new CalcResult
            {
                Success = true,
                Result = $"🛤️ PAVIMENTO — MÉTODO DNER\n" +
                          $"─────────────────────────────\n" +
                          $"Número N: {n / 1e6:0.0} × 10⁶\n" +
                          $"CBR Subleito: {cbrSubleito:0}%\n" +
                          $"─────────────────────────────\n" +
                          $"Espessura total (HS): {hs:0.0} cm\n" +
                          $"─────────────────────────────\n" +
                          $"📋 Estrutura sugerida:\n" +
                          $"  Revestimento (CA): {Math.Min(10, hs * 0.3):0.0} cm\n" +
                          $"  Base granular: {hs * 0.35:0.0} cm\n" +
                          $"  Sub-base: {hs * 0.35:0.0} cm\n" +
                          $"─────────────────────────────\n" +
                          $"⚠️ Ajustar conforme CBR das camadas e materiais disponíveis",
                Values = new Dictionary<string, double> { ["HS"] = hs }
            };
        }

        private static CalcResult CalcPavimentoAASHTO(Dictionary<string, double> p)
        {
            return new CalcResult
            {
                Success = true,
                Result = "🛤️ PAVIMENTO — MÉTODO AASHTO\n" +
                          "─────────────────────────────\n" +
                          "O método AASHTO requer:\n" +
                          "• W18 (carga eixo padrão)\n" +
                          "• R (confiabilidade: 85-99%)\n" +
                          "• So (desvio padrão: 0.35-0.50)\n" +
                          "• ΔPSI (perda serventia: 1.5-2.0)\n" +
                          "• Mr (módulo resiliência)\n\n" +
                          "Use CALC:PAV_AASTHO W18=5e6 R=90 So=0.45 ΔPSI=1.7 Mr=5000\n" +
                          "para dimensionamento completo."
            };
        }

        private static CalcResult CalcCAIG(Dictionary<string, double> p)
        {
            double largura = p.ContainsKey("l") ? p["l"] : 7.0;
            double comprimento = p.ContainsKey("c") ? p["c"] : 1000.0;
            double espessura = p.ContainsKey("e") ? p["e"] : 0.05;

            double volume = largura * comprimento * espessura;
            double densidade = 2.4; // t/m³
            double massa = volume * densidade;

            return new CalcResult
            {
                Success = true,
                Result = $"🛤️ CONSUMO CAIG (Concreto Asfáltico)\n" +
                          $"─────────────────────────────\n" +
                          $"Largura: {largura:0.0} m\n" +
                          $"Extensão: {comprimento:0.0} m\n" +
                          $"Espessura: {espessura * 100:0.0} cm\n" +
                          $"─────────────────────────────\n" +
                          $"Volume: {volume:0.0} m³\n" +
                          $"Massa (dens. 2.4): {massa:0.0} t\n" +
                          $"Caminhões (12t): {Math.Ceiling(massa / 12)}",
                Values = new Dictionary<string, double> { ["Volume"] = volume, ["Massa"] = massa }
            };
        }

        private static CalcResult CalcEspessuras(Dictionary<string, double> p)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📐 TABELA DE ESPESSURAS MÍNIMAS (DNIT)");
            sb.AppendLine("─────────────────────────────");
            sb.AppendLine("N ≤ 10⁶  → Rev=5.0cm");
            sb.AppendLine("10⁶ < N ≤ 5×10⁶ → Rev=7.5cm");
            sb.AppendLine("5×10⁶ < N ≤ 10⁷ → Rev=10.0cm");
            sb.AppendLine("10⁷ < N ≤ 5×10⁷ → Rev=12.5cm");
            sb.AppendLine("N > 5×10⁷ → Rev=15.0cm");
            sb.AppendLine("─────────────────────────────");
            sb.AppendLine("Base granular: 15-30cm");
            sb.AppendLine("Sub-base: 15-30cm");
            sb.AppendLine("Reforço do subleito: variável");
            return new CalcResult { Success = true, Result = sb.ToString() };
        }

        // ═══════════════════════════════════════════
        // TERRAPLENAGEM
        // ═══════════════════════════════════════════

        private static CalcResult CalcVolumeCorteAterro(Dictionary<string, double> p)
        {
            double areaCorte = p.ContainsKey("ac") ? p["ac"] : 0;
            double areaAterro = p.ContainsKey("aa") ? p["aa"] : 0;
            double distancia = p.ContainsKey("d") ? p["d"] : 20;

            double volCorte = areaCorte * distancia;
            double volAterro = areaAterro * distancia;
            double saldo = volCorte - volAterro;

            return new CalcResult
            {
                Success = true,
                Result = $"⛏️ VOLUMES CORTE/ATERRO\n" +
                          $"─────────────────────────────\n" +
                          $"Área corte: {areaCorte:0.0} m²\n" +
                          $"Área aterro: {areaAterro:0.0} m²\n" +
                          $"Distância entre seções: {distancia:0.0} m\n" +
                          $"─────────────────────────────\n" +
                          $"Volume corte: {volCorte:0.0} m³\n" +
                          $"Volume aterro: {volAterro:0.0} m³\n" +
                          $"Saldo: {(saldo >= 0 ? $"+{saldo:0.0} m³ (sobra)" : $"{saldo:0.0} m³ (falta)")}",
                Values = new Dictionary<string, double> { ["VCorte"] = volCorte, ["VAterro"] = volAterro, ["Saldo"] = saldo }
            };
        }

        private static CalcResult CalcEmpolamento(Dictionary<string, double> p)
        {
            double volCorte = p.ContainsKey("vc") ? p["vc"] : 1000;
            double empolamento = p.ContainsKey("e") ? p["e"] : 30;  // 30% padrão

            double volSolto = volCorte * (1 + empolamento / 100);
            double volCompactado = volSolto / (1 + empolamento / 200);  // redução na compactação

            return new CalcResult
            {
                Success = true,
                Result = $"🚛 EMPOLAMENTO\n" +
                          $"─────────────────────────────\n" +
                          $"Volume corte: {volCorte:0.0} m³\n" +
                          $"Empolamento: {empolamento:0}%\n" +
                          $"─────────────────────────────\n" +
                          $"Volume solto: {volSolto:0.0} m³\n" +
                          $"Viagens (12m³): {Math.Ceiling(volSolto / 12)}",
                Values = new Dictionary<string, double> { ["VolSolto"] = volSolto }
            };
        }

        private static CalcResult CalcCompactacao(Dictionary<string, double> p)
        {
            double volAterro = p.ContainsKey("va") ? p["va"] : 1000;
            double grauCompactacao = p.ContainsKey("gc") ? p["gc"] : 95;  // Proctor normal

            double volNecessario = volAterro * (100.0 / grauCompactacao) * 1.3;  // 30% empolamento

            return new CalcResult
            {
                Success = true,
                Result = $"🔨 COMPACTAÇÃO DE ATERRO\n" +
                          $"─────────────────────────────\n" +
                          $"Volume aterro compactado: {volAterro:0.0} m³\n" +
                          $"Grau compactação: {grauCompactacao:0}% Proctor\n" +
                          $"─────────────────────────────\n" +
                          $"Volume necessário (corte): {volNecessario:0.0} m³",
                Values = new Dictionary<string, double> { ["VolNecessario"] = volNecessario }
            };
        }

        private static CalcResult CalcFatorHomogeneizacao(Dictionary<string, double> p)
        {
            double densCorte = p.ContainsKey("dc") ? p["dc"] : 1.7;
            double densAterro = p.ContainsKey("da") ? p["da"] : 1.9;

            double fator = densAterro / densCorte;

            return new CalcResult
            {
                Success = true,
                Result = $"⚖️ FATOR DE HOMOGENEIZAÇÃO\n" +
                          $"─────────────────────────────\n" +
                          $"Dens. corte: {densCorte:0.00} t/m³\n" +
                          $"Dens. aterro: {densAterro:0.00} t/m³\n" +
                          $"─────────────────────────────\n" +
                          $"Fator: {fator:0.000}\n" +
                          $"1m³ aterro = {fator:0.00}m³ corte",
                Values = new Dictionary<string, double> { ["Fator"] = fator }
            };
        }

        // ═══════════════════════════════════════════
        // DRENAGEM
        // ═══════════════════════════════════════════

        private static CalcResult CalcTempoConcentracao(Dictionary<string, double> p)
        {
            double l = p.ContainsKey("l") ? p["l"] : 500;    // comprimento talvegue (m)
            double s = p.ContainsKey("s") ? p["s"] : 0.01;    // declividade (m/m)

            // Kirpich
            double tc = 0.0663 * Math.Pow(l / Math.Pow(s, 0.5), 0.77);

            return new CalcResult
            {
                Success = true,
                Result = $"⏱️ TEMPO DE CONCENTRAÇÃO (Kirpich)\n" +
                          $"─────────────────────────────\n" +
                          $"Comprimento: {l:0}m\n" +
                          $"Declividade: {s * 100:0.0}%\n" +
                          $"─────────────────────────────\n" +
                          $"Tc = {tc:0.0} min",
                Values = new Dictionary<string, double> { ["Tc"] = tc }
            };
        }

        private static CalcResult CalcIntensidadeChuva(Dictionary<string, double> p)
        {
            double k = p.ContainsKey("k") ? p["k"] : 1500;
            double a = p.ContainsKey("a") ? p["a"] : 0.2;
            double b = p.ContainsKey("b") ? p["b"] : 15;
            double c = p.ContainsKey("c") ? p["c"] : 0.85;
            double tr = p.ContainsKey("tr") ? p["tr"] : 10;   // tempo retorno (anos)
            double tc = p.ContainsKey("tc") ? p["tc"] : 30;    // tempo concentração (min)

            // IDF: I = K * Tr^a / (Tc + b)^c
            double i = k * Math.Pow(tr, a) / Math.Pow(tc + b, c);

            return new CalcResult
            {
                Success = true,
                Result = $"🌧️ INTENSIDADE PLUVIOMÉTRICA (IDF)\n" +
                          $"─────────────────────────────\n" +
                          $"K={k} a={a} b={b} c={c}\n" +
                          $"TR: {tr} anos | Tc: {tc} min\n" +
                          $"─────────────────────────────\n" +
                          $"Intensidade: {i:0.0} mm/h",
                Values = new Dictionary<string, double> { ["I"] = i }
            };
        }

        private static CalcResult CalcBacia(Dictionary<string, double> p)
        {
            double area = p.ContainsKey("a") ? p["a"] : 10;      // hectares
            double c = p.ContainsKey("c") ? p["c"] : 0.6;         // runoff
            double i = p.ContainsKey("i") ? p["i"] : 120;         // mm/h
            double tc = p.ContainsKey("tc") ? p["tc"] : 20;       // min

            double q = c * i * area / 360;

            return new CalcResult
            {
                Success = true,
                Result = $"🏞️ ANÁLISE DE BACIA\n" +
                          $"─────────────────────────────\n" +
                          $"Área: {area:0} ha | C={c} | I={i}mm/h\n" +
                          $"─────────────────────────────\n" +
                          $"Vazão pico: {q:0.000} m³/s ({q * 1000:0} L/s)\n" +
                          $"Volume (Tc={tc}min): {q * tc * 60:0} m³",
                Values = new Dictionary<string, double> { ["Q"] = q }
            };
        }

        // ═══════════════════════════════════════════
        // SINALIZAÇÃO / TRÁFEGO
        // ═══════════════════════════════════════════

        private static CalcResult CalcDistFrenagem(Dictionary<string, double> p)
        {
            double v = p.ContainsKey("v") ? p["v"] : 60;
            double f = p.ContainsKey("f") ? p["f"] : 0.35;
            double g = p.ContainsKey("g") ? p["g"] : 0;  // greide em %

            double dpr = 0.7 * v;  // distância percepção-reação
            double df = v * v / (254 * (f + g / 100));

            return new CalcResult
            {
                Success = true,
                Result = $"🛑 DISTÂNCIA DE FRENAGEM\n" +
                          $"─────────────────────────────\n" +
                          $"Velocidade: {v:0} km/h\n" +
                          $"Coef. atrito: {f:0.00}\n" +
                          $"Greide: {g:0.0}%\n" +
                          $"─────────────────────────────\n" +
                          $"D. percepção-reação: {dpr:0.0}m\n" +
                          $"D. frenagem: {df:0.0}m\n" +
                          $"Distância total: {dpr + df:0.0}m",
                Values = new Dictionary<string, double> { ["Dfrenagem"] = dpr + df }
            };
        }

        private static CalcResult CalcSinalizacao(Dictionary<string, double> p)
        {
            double v = p.ContainsKey("v") ? p["v"] : 80;
            double raio = p.ContainsKey("r") ? p["r"] : 300;

            // Superelevação máxima
            double eMax = 8.0;
            double fMax = 0.14;

            // Raio mínimo: Rmin = V² / (127 * (e + f))
            double rMin = v * v / (127 * (eMax / 100 + fMax));

            return new CalcResult
            {
                Success = true,
                Result = $"🚸 SINALIZAÇÃO — PARÂMETROS\n" +
                          $"─────────────────────────────\n" +
                          $"Velocidade: {v:0} km/h\n" +
                          $"Raio da curva: {raio:0}m\n" +
                          $"─────────────────────────────\n" +
                          $"Raio mínimo: {rMin:0}m\n" +
                          $"Status: {(raio >= rMin ? "✅ OK" : "❌ ABAIXO DO MÍNIMO")}\n" +
                          $"─────────────────────────────\n" +
                          $"📋 Sinalização necessária:\n" +
                          $"• Placa de velocidade: {v - 20:0}-{v:0} km/h\n" +
                          $"• Tachas refletivas a cada 4m\n" +
                          $"• Faixa de borda: 10cm branca",
                Values = new Dictionary<string, double> { ["Rmin"] = rMin }
            };
        }

        private static CalcResult CalcCapacidade(Dictionary<string, double> p)
        {
            double faixas = p.ContainsKey("f") ? p["f"] : 2;
            double tipo = p.ContainsKey("t") ? p["t"] : 1;  // 1=rodovia, 2=arterial

            double capPorFaixa = tipo == 1 ? 2200 : 1900;
            double capacidade = faixas * capPorFaixa;

            return new CalcResult
            {
                Success = true,
                Result = $"🚗 CAPACIDADE DA VIA (HCM)\n" +
                          $"─────────────────────────────\n" +
                          $"Faixas: {faixas:0}\n" +
                          $"Tipo: {(tipo == 1 ? "Rodovia" : "Arterial")}\n" +
                          $"─────────────────────────────\n" +
                          $"Capacidade: {capacidade:0} veíc/h\n" +
                          $"Capacidade diária: {capacidade * 12:0} veíc/dia",
                Values = new Dictionary<string, double> { ["Capacidade"] = capacidade }
            };
        }

        private static CalcResult CalcNivelServico(Dictionary<string, double> p)
        {
            double volume = p.ContainsKey("v") ? p["v"] : 1500;
            double capacidade = p.ContainsKey("c") ? p["c"] : 4400;

            double vc = volume / capacidade;
            string ns = vc <= 0.35 ? "A" :
                        vc <= 0.55 ? "B" :
                        vc <= 0.75 ? "C" :
                        vc <= 0.90 ? "D" :
                        vc <= 1.0 ? "E" : "F";

            return new CalcResult
            {
                Success = true,
                Result = $"📊 NÍVEL DE SERVIÇO (HCM)\n" +
                          $"─────────────────────────────\n" +
                          $"Volume: {volume:0} veíc/h\n" +
                          $"Capacidade: {capacidade:0} veíc/h\n" +
                          $"V/C: {vc:0.00}\n" +
                          $"─────────────────────────────\n" +
                          $"Nível de Serviço: {ns} {(ns == "A" ? "✅ Fluxo livre" : ns == "F" ? "❌ Congestionado" : "⚠️ Moderado")}",
                Values = new Dictionary<string, double> { ["VC"] = vc }
            };
        }

        // ═══════════════════════════════════════════
        // NOTAS DE SERVIÇO
        // ═══════════════════════════════════════════

        private static CalcResult CalcNotaTerraplenagem(Dictionary<string, double> p)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📋 NOTA DE SERVIÇO — TERRAPLENAGEM");
            sb.AppendLine("══════════════════════════════════");
            sb.AppendLine("ESTACA  | COTA PROJ. | COTA TERR. | CORTE(m) | ATERRO(m)");
            sb.AppendLine("────────┼────────────┼────────────┼──────────┼──────────");

            // Gera tabela exemplo de 5 estacas
            var rnd = new Random(42);
            double cotaBase = p.ContainsKey("cb") ? p["cb"] : 100.0;

            for (int i = 0; i <= 5; i++)
            {
                double cotaProj = cotaBase + i * 0.5;
                double cotaTerr = cotaBase + (rnd.NextDouble() - 0.3) * 3;
                double corte = Math.Max(0, cotaTerr - cotaProj);
                double aterro = Math.Max(0, cotaProj - cotaTerr);
                sb.AppendLine($"E{i * 5:D3}   | {cotaProj,10:0.00} | {cotaTerr,10:0.00} | {corte,8:0.00} | {aterro,8:0.00}");
            }

            sb.AppendLine("══════════════════════════════════");
            sb.AppendLine("💡 Use DSMODEL para gerar nota real a partir das seções.");
            return new CalcResult { Success = true, Result = sb.ToString() };
        }

        private static CalcResult CalcNotaPavimento(Dictionary<string, double> p)
        {
            var sb = new StringBuilder();
            sb.AppendLine("📋 NOTA DE SERVIÇO — PAVIMENTAÇÃO");
            sb.AppendLine("══════════════════════════════════");
            sb.AppendLine("CAMADA         | MATERIAL        | ESPESSURA");
            sb.AppendLine("───────────────┼─────────────────┼──────────");
            sb.AppendLine("Revestimento   | CA (CAP 50/70)  | 10.0 cm");
            sb.AppendLine("Base           | BG (CBR≥80%)    | 20.0 cm");
            sb.AppendLine("Sub-base       | BG (CBR≥40%)    | 20.0 cm");
            sb.AppendLine("Reforço        | BG (CBR≥20%)    | 30.0 cm");
            sb.AppendLine("───────────────┼─────────────────┼──────────");
            sb.AppendLine("TOTAL                           | 80.0 cm");
            sb.AppendLine("══════════════════════════════════");

            double largura = p.ContainsKey("l") ? p["l"] : 7.0;
            double extensao = p.ContainsKey("e") ? p["e"] : 1000;
            sb.AppendLine($"Largura: {largura}m | Extensão: {extensao}m");
            sb.AppendLine($"CA: {largura * extensao * 0.10 * 2.4:0} t");
            sb.AppendLine($"BG: {largura * extensao * 0.40 * 2.0:0} t");

            return new CalcResult { Success = true, Result = sb.ToString() };
        }
    }
}
