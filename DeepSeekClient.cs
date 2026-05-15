using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace C3DDeepSeek
{
    /// <summary>
    /// Cliente HTTP para API DeepSeek (compatível com formato OpenAI)
    /// </summary>
    public class DeepSeekClient
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private const string BaseUrl = "https://api.deepseek.com";
        private const string Model = "deepseek-chat";

        // Contexto do sistema: especialista em Civil 3D / AutoCAD
        private const string SystemPrompt = 
            "Você é um especialista em Autodesk Civil 3D 2026 e AutoCAD 2026. " +
            "Seu trabalho é ajudar engenheiros civis com comandos, fluxos de trabalho, " +
            "interpretação de problemas e rotinas de projeto.\n\n" +
            "Regras para responder:\n" +
            "1. Se o usuário pedir para executar um comando ou ação, responda no formato:\n" +
            "##EXEC## descrição do que será executado\n" +
            "COMANDO: o comando exato do AutoCAD/Civil 3D a ser executado\n" +
            "##ENDEXEC##\n\n" +
            "Exemplos de comandos:\n" +
            "- Criar superfície: COMANDO: CREATESURFACE\n" +
            "- Criar alinhamento: COMANDO: CREATEDALIGNMENT\n" +
            "- Desenhar polilinha: COMANDO: PLINE\n" +
            "- Zoom extensão: COMANDO: ZOOM E\n" +
            "- Listar layers: COMANDO: -LAYER ? *\n\n" +
            "Responda SEMPRE em português do Brasil.\n" +
            "Use ##EXEC## ... ##ENDEXEC## apenas quando houver comando claro para executar.";

        public DeepSeekClient(string apiKey)
        {
            _apiKey = apiKey;
            _http = new HttpClient
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = TimeSpan.FromSeconds(60)
            };
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _http.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// Envia pergunta para o DeepSeek e retorna a resposta interpretada.
        /// Inclui automaticamente o contexto do desenho atual do Civil 3D.
        /// </summary>
        public async Task<DeepSeekResponse> AskAsync(string userMessage)
        {
            try
            {
                // Coleta contexto do desenho aberto (layers, alinhamentos, superfícies, etc.)
                string contextInfo = "";
                try { contextInfo = DeepSeekContext.CollectContext(); } catch { }

                // Monta a mensagem do usuário com o contexto
                string fullUserMessage = userMessage;
                if (!string.IsNullOrWhiteSpace(contextInfo))
                {
                    fullUserMessage = $"[CONTEXTO DO DESENHO ATUAL]\n{contextInfo}\n\n[PERGUNTA DO USUÁRIO]\n{userMessage}";
                }

                var payload = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new { role = "system", content = SystemPrompt },
                        new { role = "user", content = fullUserMessage }
                    },
                    temperature = 0.3,
                    max_tokens = 800
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync("/v1/chat/completions", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new DeepSeekResponse
                    {
                        Success = false,
                        Text = $"Erro API ({response.StatusCode}): {responseBody}"
                    };
                }

                var parsed = JObject.Parse(responseBody);
                var text = parsed["choices"]?[0]?["message"]?["content"]?.ToString()?.Trim() ?? "(sem resposta)";

                return ParseResponse(text);
            }
            catch (Exception ex)
            {
                return new DeepSeekResponse
                {
                    Success = false,
                    Text = $"Erro de conexão: {ex.Message}"
                };
            }
        }

        private DeepSeekResponse ParseResponse(string text)
        {
            var result = new DeepSeekResponse { Success = true };

            // Buscar por bloco de comando ##EXEC## ... ##ENDEXEC##
            var execStart = text.IndexOf("##EXEC##", StringComparison.OrdinalIgnoreCase);
            var execEnd = text.IndexOf("##ENDEXEC##", StringComparison.OrdinalIgnoreCase);

            if (execStart >= 0 && execEnd > execStart)
            {
                var execBlock = text.Substring(execStart + 8, execEnd - execStart - 8).Trim();
                var cmdStart = execBlock.IndexOf("COMANDO:", StringComparison.OrdinalIgnoreCase);

                if (cmdStart >= 0)
                {
                    result.Command = execBlock.Substring(cmdStart + 8).Trim();
                }
                result.Explanation = execBlock.Replace("COMANDO:", "").Trim();

                // Texto de resposta é tudo fora do bloco EXEC
                var before = execStart > 0 ? text.Substring(0, execStart).Trim() : "";
                var after = execEnd + 11 < text.Length ? text.Substring(execEnd + 11).Trim() : "";
                result.Text = string.Join("\n", new[] { before, after }).Trim();
            }
            else
            {
                result.Text = text;
            }

            return result;
        }

        /// <summary>
        /// Valida se a chave API está funcionando
        /// </summary>
        public async Task<bool> ValidateKeyAsync()
        {
            try
            {
                var payload = new
                {
                    model = Model,
                    messages = new[]
                    {
                        new { role = "user", content = "OK" }
                    },
                    max_tokens = 2
                };

                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync("/v1/chat/completions", content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }

    public class DeepSeekResponse
    {
        public bool Success { get; set; }
        public string Text { get; set; } = "";
        public string Explanation { get; set; } = "";
        public string Command { get; set; } = "";
        public bool HasCommand => !string.IsNullOrWhiteSpace(Command);
    }
}
