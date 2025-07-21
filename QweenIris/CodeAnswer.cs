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
        private string instructionsToFollow;

        public CodeAnswer(OllamaApiClient model)
        {
            // set up the client
            ollama = model;
        }

        public CodeAnswer SetInstructions(string instructions)
        {
            instructionsToFollow = instructions;
            return this;
        }

        public async Task<string> GetAnswer(string history, string shortHistory, string message, string user, Action<string, bool> feedback, Action pingAlive)
        {
            var response = "";
            feedback.Invoke("Let me think about it", true);
            pingAlive.Invoke();
            MessageContainer messageContainer = new MessageContainer();
            messageContainer.SetInstructions(instructionsToFollow);
            messageContainer.SetContext(history);
            messageContainer.SetUserPrompt(message);
            var count = 0;
            await foreach (var stream in OllamaFormater.GenerateResponse(ollama, messageContainer))
            {
                if(count % 100 == 0)
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
