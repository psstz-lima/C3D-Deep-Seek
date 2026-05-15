using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace C3DDeepSeek
{
    /// <summary>
    /// Histórico de conversa — mantém contexto entre perguntas.
    /// O DeepSeek lembra do que foi dito antes, como um chat real.
    /// </summary>
    public static class ChatHistory
    {
        private class Message
        {
            public string Role { get; set; } = "";
            public string Content { get; set; } = "";
        }

        private static readonly List<Message> _history = new List<Message>();
        private const int MaxHistory = 20; // mantém últimas 20 mensagens

        /// <summary>
        /// Adiciona mensagem do usuário ao histórico
        /// </summary>
        public static void AddUserMessage(string message)
        {
            _history.Add(new Message { Role = "user", Content = message });
            TrimHistory();
        }

        /// <summary>
        /// Adiciona resposta do assistente ao histórico
        /// </summary>
        public static void AddAssistantMessage(string message)
        {
            _history.Add(new Message { Role = "assistant", Content = message });
            TrimHistory();
        }

        /// <summary>
        /// Adiciona mensagem do sistema (contexto do desenho, etc.)
        /// </summary>
        public static void AddSystemMessage(string message)
        {
            _history.Add(new Message { Role = "system", Content = message });
            TrimHistory();
        }

        /// <summary>
        /// Retorna o histórico formatado para enviar à API
        /// </summary>
        public static object[] GetHistoryForApi(string systemPrompt)
        {
            var messages = new List<object>();

            // System prompt sempre primeiro
            messages.Add(new { role = "system", content = systemPrompt });

            // Histórico da conversa
            foreach (var msg in _history)
            {
                messages.Add(new { role = msg.Role, content = msg.Content });
            }

            return messages.ToArray();
        }

        /// <summary>
        /// Resume o histórico para economizar tokens — útil em conversas longas
        /// </summary>
        public static string GetSummary()
        {
            if (_history.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("[RESUMO DA CONVERSA ANTERIOR]");
            foreach (var msg in _history)
            {
                var prefix = msg.Role == "user" ? "👷 Usuário" :
                             msg.Role == "assistant" ? "🤖 DeepSeek" : "📋 Sistema";
                var shortContent = msg.Content.Length > 200
                    ? msg.Content.Substring(0, 200) + "..."
                    : msg.Content;
                sb.AppendLine($"{prefix}: {shortContent}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Limpa o histórico
        /// </summary>
        public static void Clear()
        {
            _history.Clear();
        }

        /// <summary>
        /// Número de mensagens no histórico
        /// </summary>
        public static int Count => _history.Count;

        private static void TrimHistory()
        {
            while (_history.Count > MaxHistory)
                _history.RemoveAt(0);
        }

        /// <summary>
        /// Retorna o contexto da conversa para embutir na mensagem atual
        /// </summary>
        public static string GetContextForPrompt()
        {
            if (_history.Count == 0) return "";

            var sb = new StringBuilder();
            sb.AppendLine("[HISTÓRICO DA CONVERSA — use este contexto para responder]");
            int i = 1;
            foreach (var msg in _history)
            {
                var prefix = msg.Role == "user" ? $"👷 Pergunta {i}" :
                             msg.Role == "assistant" ? $"🤖 Resposta {i}" : $"📋 Info {i}";
                sb.AppendLine($"{prefix}: {msg.Content}");
                i++;
            }
            return sb.ToString();
        }
    }
}
