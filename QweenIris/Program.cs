using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using OllamaSharp;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QweenIris
{

    internal class Program
    {
        private readonly WebFetcher webFetcher;
        private readonly DiscordBot discordBot;
        private readonly AnswerFactory answerFactory;

        static void Main(string[] args) =>
            new Program().StartBotAsync().GetAwaiter().GetResult();


        public Program()
        {
            discordBot = new DiscordBot(ReadMessage, 1392616212621557871, 1394400815191425045, 1394684362959884411, 1392593573551013958);
            webFetcher = new WebFetcher();
            answerFactory = new AnswerFactory();
        }

        private Task StartBotAsync()
        {
            return discordBot.StartAsync();
        }

        private async void TriggerTyping()
        {
            await discordBot.TriggerTyping();
        }

        private async void sendMessage(string message)
        {
            await discordBot.ReplyAsync(message);
        }

        private async void ReadMessage(string instructions, string codeInstructions, string newsSearchInstructions, string history, string message, string user)
        {
            Console.WriteLine(" " +message);
            TriggerTyping();
            var answerProvider = await answerFactory.GetAnswer(message, instructions, codeInstructions, newsSearchInstructions);
            var answer  = await answerProvider.GetAnswer(history, message,  user, sendMessage, TriggerTyping);
            Console.WriteLine(" " + answer);
            await discordBot.ReplyAsync(answer);
        }
    }

    class ResponseType
    {
        public bool requireSearch;
        public string promptAnswer;
    }

    public class WebFetcher
    {
        public async Task<string> GetHtmlAsync(string url)
        {
            using var httpClient = new HttpClient();
            string html = await httpClient.GetStringAsync(url);
            return html;
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