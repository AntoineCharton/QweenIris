using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QweenIris
{

    internal class Program
    {
        private readonly DiscordBot discordBot;
        private readonly AnswerFactory answerFactory;
        private int readMessageCount;
        private CancellationTokenSource cancellationTokenSource;
        private int numberOfMessagesToRead;

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
            answerFactory = new AnswerFactory();
        }

        private async Task StartBotAsync()
        {
            
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            await discordBot.StartAsync();
            return;
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
            if(cancellationTokenSource != null)
            {
                try
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                } catch
                {
                }
            }
            if (numberOfMessagesToRead > 0)
            {
                sendMessage("You are typing to fast for me", false);
                return;
            }
            cancellationTokenSource = new CancellationTokenSource();
            Console.WriteLine(" " + promptContext.Prompt);
            TriggerTyping();
            numberOfMessagesToRead++;
            try
            {
                var answerProvider = await answerFactory.GetAnswer(promptContext, TriggerTyping, sendMessage, cancellationTokenSource.Token);
                var answer = "";
                if (answerProvider != null)
                {

                    answer = await answerProvider.GetAnswer(promptContext, sendMessage, TriggerTyping);
                    readMessageCount++;
                    Console.WriteLine(" " + answer);
                    await discordBot.ReplyAsync(answer, false);
                    if (readMessageCount > 30)
                    {
                        readMessageCount = 0;
                        Ollama.RestartOllama();
                        Console.WriteLine("Restarting Ollama");
                    }
                    if (cancellationTokenSource != null)
                    {
                        cancellationTokenSource.Dispose();
                        cancellationTokenSource = null;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                sendMessage("I didn't have time to finish :(", false);
            }
            numberOfMessagesToRead--;
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