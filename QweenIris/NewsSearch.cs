using OllamaSharp;
using System.Text.RegularExpressions;

namespace QweenIris
{
    internal class NewsSearch : IAnswer
    {
        private readonly OllamaApiClient ollama;
        private string instructionsToFollow;

        public NewsSearch(OllamaApiClient model)
        {
            // set up the client
            ollama = model;
        }

        public NewsSearch SetInstructions(string instructions)
        {
            instructionsToFollow = instructions;
            return this;
        }

        public async Task<string> GetAnswer(string history, string message, string user, Action<string> feedback, Action pingAlive)
        {
            var response = "";
            var formatedInstruction = $"Your instructions are: '{instructionsToFollow}'";
            user = $"The user name is: '{user}'";
            message = $"This is the message: '{message}'";
            history = $"This is the history of the conversation do not account for it unless the user ask you: '{history}' This is the end of the history";
            feedback.Invoke("Let me think about it");
            pingAlive.Invoke();
            await foreach (var stream in ollama.GenerateAsync(formatedInstruction + history + user + message))
            {
                response += stream.Response;
            }
            Console.WriteLine(response);
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");
            return output;
        }
    }
}
