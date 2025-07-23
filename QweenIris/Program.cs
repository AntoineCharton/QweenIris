using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QweenIris
{

    internal class Program
    {
        private readonly WebFetcher webFetcher;
        private readonly DiscordBot discordBot;
        private readonly AnswerFactory answerFactory;
        private int readMessageCount;

        static void Main(string[] args) =>
            new Program().StartBotAsync().GetAwaiter().GetResult();


        [DllImport("kernel32.dll")]
        public static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        private const uint ENABLE_EXTENDED_FLAGS = 0x0080;

        private static void DisableQuickEditMode()
        {
            IntPtr handle = Process.GetCurrentProcess().MainWindowHandle;
            SetConsoleMode(handle, ENABLE_EXTENDED_FLAGS);
        }

        public Program()
        {
            DisableQuickEditMode();
            Ollama.RestartOllama();
            discordBot = new DiscordBot(ReadMessage);
            webFetcher = new WebFetcher();
            answerFactory = new AnswerFactory();
        }

        private Task StartBotAsync()
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            return discordBot.StartAsync();
        }

        private async void TriggerTyping()
        {
            await discordBot.TriggerTyping();
        }

        private async void sendMessage(string message, bool deletePrevious)
        {
            await discordBot.ReplyAsync(message, deletePrevious);
        }

        private async void ReadMessage(PromptContext promptContext)
        {
            Console.WriteLine(" " + promptContext.Prompt);
            TriggerTyping();
            var answerProvider = await answerFactory.GetAnswer(promptContext, TriggerTyping, sendMessage);
            if (answerProvider != null)
            {
                var answer = await answerProvider.GetAnswer(promptContext, sendMessage, TriggerTyping);
                readMessageCount++;
                Console.WriteLine(" " + answer);
                await discordBot.ReplyAsync(answer, false);
                if (readMessageCount > 30)
                {
                    readMessageCount = 0;
                    Ollama.RestartOllama();
                    Console.WriteLine("Restarting Ollama");
                }
            }
        }
    }

public static class Config
    {
        public static IConfigurationRoot Load()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
        }
    }

}