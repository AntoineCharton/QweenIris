using Discord;
using Discord.WebSocket;
using OllamaSharp;
using System.IO;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

namespace QweenIris
{
    internal class NewsSearch
    {
        private readonly OllamaApiClient ollama;
        readonly string firstInstruction = "You are a model that evaluates a user's intent and returns a single number from 1 to 10, using the following fixed mapping (no skipped numbers):\r\n\r\n1 = Casual greeting or small talk (e.g., \"hi\", \"hello\", \"what’s up?\")\r\n\r\n2 = General conversation or chitchat\r\n\r\n3 = Asking personal questions (e.g., \"what's your name?\", \"how are you?\")\r\n\r\n4 = Entertainment-related (e.g., jokes, memes, fun videos)\r\n\r\n5 = Asking for help or advice (non-technical)\r\n\r\n6 = Technical help (e.g., coding, software, debugging)\r\n\r\n7 = Learning or trying to understand a concept (e.g., “how does a rocket work?”)\r\n\r\n8 = Researching factual information (e.g., “population of France”)\r\n\r\n9 = Looking for current events or updates\r\n\r\n10 = Explicitly seeking news (e.g., “latest headlines”, “news on Ukraine”)\r\n\r\nReturn only one number from 1 to 10 based on the user's message. No explanation. No extra text. Just the number.";
        public NewsSearch()
        {
            var uri = new Uri("http://localhost:11434");
            ollama = new OllamaApiClient(uri);
            ollama.SelectedModel = "qwen2.5vl";
        }

        public async Task<bool> OnlineSearchRequested(string message)
        {
            var type = new ResponseType();
            await ParsePromptCheckRequireOnlineSearch( message + " " + firstInstruction, type);
            return type.requireSearch;
        }

        private async Task<string> ParsePromptCheckRequireOnlineSearch(string prompt, ResponseType type)
        {
            Console.WriteLine(prompt);
            var response = "";
            await foreach (var stream in ollama.GenerateAsync(prompt))
            {
                response += stream.Response;
                
            }
            Console.WriteLine(response);
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");
            if (float.Parse(output) == 10 || float.Parse(output) == 9)
            {
                type.requireSearch = true;
                string[] parts = output.Split(':');

                if (parts.Length == 2)
                {
                    string firstPart = parts[0];
                    string secondPart = parts[1];
                    type.promptAnswer = secondPart;
                    return secondPart;
                }
            }

            return "Something went wrong";
        }

        public async Task<string> ParseFeed(string feed)
        {
            var response = "";
            await foreach (var stream in ollama.GenerateAsync("Synthetise this html document and make sure you keep urls: " + feed))
            {
                response += stream.Response;
                

            }
            Console.WriteLine(response);
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");

            if (!string.IsNullOrWhiteSpace(output))
            {
                return output;
            }

            return "";
        }
    }
}
