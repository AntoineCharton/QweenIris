using System.Diagnostics;

namespace QweenIris
{
    internal static class Ollama
    {
        public static void RestartOllama()
        {
            // Kill all running ollama processes
            var ollamaProcesses = Process.GetProcessesByName("ollama");
            foreach (var process in ollamaProcesses)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();
                }
                catch
                {
                    // Handle errors (permissions, already exited, etc.)
                }
            }
        }
    }
}
