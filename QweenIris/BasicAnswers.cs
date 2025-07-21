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

        public async Task<string> GetAnswer(string history, string shortHistory, string message, string user, Action<string, bool> feedback, Action pingAlive)
        {
            var promptFormat = new MessageContainer();
            promptFormat.SetInstructions(instructionsToFollow);
            user = "Name:" + user;
            promptFormat.SetContext(shortHistory);
            promptFormat.SetUserPrompt(message);
            pingAlive.Invoke();
            var response = "";
            await foreach (var stream in OllamaFormater.GenerateResponse(ollama, promptFormat))
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
        public Task<string> GetAnswer(string history, string shortHistory, string message, string user, Action<string, bool> feedback, Action pingAlive);
    }
}
