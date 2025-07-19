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

        public async Task<string> GetAnswer(string history, string message, string user, Action<string, bool> feedback, Action pingAlive)
        {
            var response = "";
            var formatedInstructions = "Instructions:" + "{\n" + instructionsToFollow + "\n}";
            user = "User:" + "{\n" +user + "}";
            message = "Message:" + "{\n" + message + "\n}";
            pingAlive.Invoke();
            await foreach (var stream in ollama.GenerateAsync(user + message + formatedInstructions))
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
        public Task<string> GetAnswer(string history, string message, string user, Action<string, bool> feedback, Action pingAlive);
    }
}
