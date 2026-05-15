using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AcAp = Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;

namespace C3DDeepSeek
{
    /// <summary>
    /// Motor premium de execução — interpreta comandos complexos,
    /// sequências, AutoLISP e operações avançadas do Civil 3D.
    /// </summary>
    public static class DeepSeekEngine
    {
        /// <summary>
        /// Resultado da execução de um comando
        /// </summary>
        public class ExecutionResult
        {
            public bool Success { get; set; }
            public string Output { get; set; } = "";
            public List<string> Steps { get; set; } = new List<string>();
            public string Report { get; set; } = "";
        }

        /// <summary>
        /// Analisa a resposta do DeepSeek e executa os comandos encontrados.
        /// Suporta comandos simples, sequências (separadas por ;) e AutoLISP.
        /// </summary>
        public static ExecutionResult Execute(DeepSeekResponse response)
        {
            var result = new ExecutionResult();

            if (!response.HasCommand)
            {
                result.Success = true;
                result.Output = response.Text;
                return result;
            }

            var command = response.Command.Trim();

            // ── Detecta e executa operações especiais ──

            // Relatório / análise
            if (command.StartsWith("REPORT:", StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith("RELATORIO:", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteReport(command);
            }

            // Operação direta Civil 3D via API
            if (command.StartsWith("API:", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteApiOperation(command);
            }

            // BIM / IFC
            if (command.StartsWith("BIM:", StringComparison.OrdinalIgnoreCase) ||
                command.StartsWith("IFC:", StringComparison.OrdinalIgnoreCase))
            {
                return ExecuteBimOperation(command);
            }

            // ── Execução de comandos normais ──

            // Se contém ;, é uma sequência de comandos
            if (command.Contains(";"))
            {
                return ExecuteSequence(command, result);
            }

            // Se é LISP (começa com parênteses)
            if (command.StartsWith("("))
            {
                return ExecuteLisp(command, result);
            }

            // Comando único
            return ExecuteSingle(command, result);
        }

        /// <summary>
        /// Executa uma sequência de comandos separados por ;
        /// </summary>
        private static ExecutionResult ExecuteSequence(string commands, ExecutionResult result)
        {
            var steps = commands.Split(';');
            var sb = new StringBuilder();
            bool allOk = true;

            foreach (var step in steps)
            {
                var cmd = step.Trim();
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                result.Steps.Add(cmd);
                try
                {
                    SendToAutoCAD(cmd);
                    sb.AppendLine($"✅ {cmd}");
                }
                catch (Exception ex)
                {
                    allOk = false;
                    sb.AppendLine($"❌ {cmd}: {ex.Message}");
                }
            }

            result.Success = allOk;
            result.Output = sb.ToString();
            return result;
        }

        /// <summary>
        /// Executa um único comando AutoCAD/Civil 3D
        /// </summary>
        private static ExecutionResult ExecuteSingle(string command, ExecutionResult result)
        {
            result.Steps.Add(command);
            try
            {
                SendToAutoCAD(command);
                result.Success = true;
                result.Output = $"✅ Comando executado: {command}";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Output = $"❌ Erro: {ex.Message}";
            }
            return result;
        }

        /// <summary>
        /// Executa uma expressão AutoLISP — envia direto pro AutoCAD interpretar
        /// </summary>
        private static ExecutionResult ExecuteLisp(string lispExpression, ExecutionResult result)
        {
            result.Steps.Add(lispExpression);
            try
            {
                // LISP expressions são enviadas como texto puro — AutoCAD as interpreta
                SendToAutoCAD(lispExpression);
                result.Success = true;
                result.Output = $"✅ LISP executado: {lispExpression}";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Output = $"❌ Erro LISP: {ex.Message}";
            }
            return result;
        }

        /// <summary>
        /// Gera relatório usando C3DReports
        /// </summary>
        private static ExecutionResult ExecuteReport(string command)
        {
            var result = new ExecutionResult();
            try
            {
                // Extrai o tipo de relatório: RELATORIO: SURFACES, ALIGNMENTS, etc.
                var parts = command.Split(':');
                var reportType = parts.Length > 1 ? parts[1].Trim().ToUpper() : "FULL";

                result.Report = C3DReports.Generate(reportType);
                result.Success = true;
                result.Output = $"📊 Relatório '{reportType}' gerado.\n{result.Report}";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Output = $"❌ Erro ao gerar relatório: {ex.Message}";
            }
            return result;
        }

        /// <summary>
        /// Operação direta via API managed do Civil 3D
        /// </summary>
        private static ExecutionResult ExecuteApiOperation(string command)
        {
            var result = new ExecutionResult();
            try
            {
                var opResult = C3DOperations.Execute(command);
                result.Success = opResult.Success;
                result.Output = opResult.Message;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Output = $"❌ Erro na operação: {ex.Message}";
            }
            return result;
        }

        /// <summary>
        /// Operação BIM (IFC export, validação, etc.)
        /// </summary>
        private static ExecutionResult ExecuteBimOperation(string command)
        {
            var result = new ExecutionResult();
            // Placeholder — IFC export via comando nativo: -EXPORTIFC
            try
            {
                SendToAutoCAD("_.-EXPORTIFC");
                result.Success = true;
                result.Output = "✅ Comando IFC iniciado. Selecione o arquivo na janela de exportação.";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Output = $"❌ Erro BIM: {ex.Message}";
            }
            return result;
        }

        /// <summary>
        /// Envia comando para o AutoCAD usando COM SendCommand (confiável)
        /// </summary>
        public static void SendToAutoCAD(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;

            try
            {
                dynamic acadApp = AcAp.Application.AcadApplication;
                acadApp.ActiveDocument.SendCommand(command + "\n");
            }
            catch
            {
                var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
                doc?.SendStringToExecute(command + "\n", true, false, true);
            }
        }

        /// <summary>
        /// Exibe o resultado da execução no editor do AutoCAD
        /// </summary>
        public static void DisplayResult(ExecutionResult result)
        {
            var doc = AcAp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;

            ed.WriteMessage($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            ed.WriteMessage($"\n🤖 DeepSeek Engine v2.0 - Resultado");
            ed.WriteMessage($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            if (result.Steps.Count > 0)
            {
                ed.WriteMessage($"\n📋 {result.Steps.Count} passo(s) executado(s):");
                foreach (var step in result.Steps)
                    ed.WriteMessage($"\n   ▸ {step}");
            }

            if (!string.IsNullOrWhiteSpace(result.Report))
            {
                ed.WriteMessage($"\n{result.Report}");
            }

            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                ed.WriteMessage($"\n{result.Output}");
            }

            ed.WriteMessage($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");
        }
    }
}
