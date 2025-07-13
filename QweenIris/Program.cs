using Discord;
using Discord.WebSocket;
using OllamaSharp;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace QweenIris
{

    internal class Program
    {
        private readonly DiscordSocketClient client;
        private readonly string token;
        private readonly OllamaApiClient ollama;
        private readonly WebFetcher webFetcher;
        public Program()
        {
            this.client = new DiscordSocketClient();
            webFetcher = new WebFetcher();
            var config = Config.Load();
            token = config["Discord:Token"];
            this.client.MessageReceived += async message =>
            {
                // Avoid blocking the gateway task
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await MessageHandler(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in MessageHandler: {ex.Message}");
                    }
                });
            };
            // set up the client
            var uri = new Uri("http://localhost:11434");
            ollama = new OllamaApiClient(uri);
            ollama.SelectedModel = "qwen3";

        }

        private async Task ParsePrompt(SocketMessage message, string prompt)
        {
            Console.WriteLine(prompt);
            var response = "";
            await foreach (var stream in ollama.GenerateAsync(prompt))
            {
                response += stream.Response;
                

            }
            Console.WriteLine(response);
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");

            if (!string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    await ReplyAsync(message, line);
                }
            }
            Console.WriteLine(); // Newline at the end
        }

        public async Task StartBotAsync()
        {

            this.client.Log += LogFuncAsync;

            await this.client.LoginAsync(TokenType.Bot, token);
            await this.client.StartAsync();
            await Task.Delay(-1);

            async Task LogFuncAsync(LogMessage message) =>
                Console.Write(message.ToString());
        }

        private async Task MessageHandler(SocketMessage message)
        {
            if (message.Author.IsBot) return;
            Console.WriteLine(message.Channel.Id);

            ulong channelID = 1392593573551013958;
            ulong instructionsChannelID = 1392616212621557871;
            Console.WriteLine(channelID == message.Channel.Id);
            if (message.Channel.Id == channelID)
            {
                await ReadHistory(message, channelID, instructionsChannelID);
                Console.WriteLine("Content: " + message.Content);
                //await ReplyAsync(message, "C# response works!");
            }
        }

        private async Task ReplyAsync(SocketMessage message, string response) =>
            await message.Channel.SendMessageAsync(response);

        static void Main(string[] args) =>
            new Program().StartBotAsync().GetAwaiter().GetResult();

        public async Task ReadHistory(SocketMessage socketMessage, ulong id, ulong instructionsID)
        {

            var instructions = client.GetChannel(instructionsID) as SocketTextChannel;
            var instructionMessage = await instructions.GetMessagesAsync(limit: 1).FlattenAsync();
            var contentWithInstructions = "";
            foreach (var message in instructionMessage)
            {
                contentWithInstructions += " " + message.Content;
            }


            var channel = client.GetChannel(id) as SocketTextChannel;
            // Get the last 100 messages
            var messages = await channel.GetMessagesAsync(limit: 1).FlattenAsync();
            var requireSearch = false;
            var newsSearch = new NewsSearch();
            foreach (var message in messages)
            {
                requireSearch = await newsSearch.OnlineSearchRequested(message.Content);
            }

            if (!requireSearch)
            {
                foreach (var message in messages)
                {
                    await ParsePrompt(socketMessage, $"```Your instructions to reply: {contentWithInstructions}```:```You are speaking to {message.Author.Username}```:```This is the message```{message.Content}");
                }
            }
            else
            {

                var html = await webFetcher.GetHtmlAsync($"https://rss.dw.com/rdf/rss-en-top");
                var synthetised = await newsSearch.ParseFeed(html);
                //html = await webFetcher.GetHtmlAsync($"https://moxie.foxnews.com/google-publisher/latest.xml");
                //synthetised += await newsSearch.ParseFeed(html);
                //html += await webFetcher.GetHtmlAsync($"https://www.lemonde.fr/rss/une.xml");
                ///html += await webFetcher.GetHtmlAsync($"https://news.google.com/search?q=site:wikipedia.org+{responseType.promptAnswer}");
                //html = await webFetcher.GetHtmlAsync($"https://www.lemonde.fr/en/");
                foreach (var message in messages)
                {
                    await ParsePrompt(socketMessage, $"```Your instructions to reply: This is the infos the user is looking for. make sure you link urls to your answer that are related to the news. Do not include URLs that don't have text description:``` ```{synthetised}```:```You are speaking to {message.Author.Username}```:```This is the message```{message.Content}");
                }
                Console.WriteLine(synthetised);
                Console.WriteLine("Search required");
                //Console.WriteLine($"https://news.google.com/search?q=site:wikipedia.org+{responseType.promptAnswer}");
            }
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