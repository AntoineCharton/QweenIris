using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QweenIris
{
    internal class CodeAnswer : IAnswer
    {
        private readonly OllamaApiClient ollama;

        public CodeAnswer(OllamaApiClient model)
        {
            // set up the client
            ollama = model;
        }

        public async Task<string> GetAnswer(string instructions, string codeInstructions, string history, string message, string user, Action<string> feedback, Action pingAlive)
        {
            var response = "";
            instructions = $"Your instructions are: '{codeInstructions}'";
            user = $"The user name is: '{user}'";
            message = $"This is the message: '{message}'";
            history = $"This is the history of the conversation do not account for it unless the user ask you: '{history}' This is the end of the history";
            feedback.Invoke("Let me think about it");
            pingAlive.Invoke();
            var count = 0;
            await foreach (var stream in ollama.GenerateAsync(instructions + history + user + message))
            {
                if(count % 500 == 0)
                {
                    pingAlive.Invoke();
                }
                count++;
                response += stream.Response;
            }
            Console.WriteLine(response);
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");
            return output;
        }
    }
}
