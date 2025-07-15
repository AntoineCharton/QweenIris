using Microsoft.Playwright;
using OllamaSharp;
using System.Text.RegularExpressions;

namespace QweenIris
{
    internal class BasicAnswers: IAnswer
    {
        private readonly OllamaApiClient ollama;

        public BasicAnswers(OllamaApiClient model) {
            ollama = model;
        }

        public async Task<string> GetAnswer(string instructions, string codeInstructions, string history, string message, string user, Action<string> feedback, Action pingAlive)
        {
            var response = "";
            instructions = $"Your instructions are: '{instructions}'";
            user = $"The user name is: '{user}'";
            message = $"This is the message: '{message}'";
            //history = $"This is the history of the conversation do not account for it unless the user ask you: '{history}' This is the end of the history";
            pingAlive.Invoke();
            await foreach (var stream in ollama.GenerateAsync(instructions + user + message))
            {
                response += stream.Response;
            }
            Console.WriteLine(response);
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");
            return output;
        }
    }

    interface IAnswer
    {
        public Task<string> GetAnswer(string instructions, string codeInstructions, string history, string message, string user, Action<string> feedback, Action pingAlive);
    }
}
