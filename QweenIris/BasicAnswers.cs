using Microsoft.Playwright;
using OllamaSharp;
using System.Text.RegularExpressions;

namespace QweenIris
{
    internal class BasicAnswers: IAnswer
    {
        private readonly OllamaApiClient ollama;
        private string instructionsToFollow;

        public BasicAnswers(OllamaApiClient model) {
            ollama = model;
        }

        public BasicAnswers SetInstructions(string instructions)
        {
            instructionsToFollow = instructions;
            return this;
        }

        public async Task<string> GetAnswer(string history, string message, string user, Action<string> feedback, Action pingAlive)
        {
            var response = "";
            var formatedInstructions = $"Your instructions are: '{instructionsToFollow}'";
            user = $"The user name is: '{user}'";
            message = $"This is the message: '{message}'";
            pingAlive.Invoke();
            await foreach (var stream in ollama.GenerateAsync(formatedInstructions + user + message))
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
        public Task<string> GetAnswer(string history, string message, string user, Action<string> feedback, Action pingAlive);
    }
}
