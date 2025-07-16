using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
