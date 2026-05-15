using System;
using System.IO;

namespace C3DDeepSeek
{
    public static class DeepSeekConfig
    {
        private static string _apiKey;

        /// <summary>
        /// Carrega a chave API do arquivo .env ao lado da DLL
        /// </summary>
        public static string LoadApiKey()
        {
            if (!string.IsNullOrWhiteSpace(_apiKey))
                return _apiKey;

            try
            {
                var dllPath = typeof(DeepSeekConfig).Assembly.Location;
                var dllDir = Path.GetDirectoryName(dllPath);
                var envPath = Path.Combine(dllDir, ".env");

                if (File.Exists(envPath))
                {
                    var lines = File.ReadAllLines(envPath);
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("DEEPSEEK_API_KEY=", StringComparison.OrdinalIgnoreCase))
                        {
                            _apiKey = trimmed.Substring("DEEPSEEK_API_KEY=".Length).Trim();
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    // Fallback: chave hardcoded (não recomendado para produção)
                    _apiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY", EnvironmentVariableTarget.User) ?? "";
                }

                return _apiKey;
            }
            catch
            {
                return "";
            }
        }
    }
}
