using OllamaSharp;
using System.Text.RegularExpressions;

namespace QweenIris
{
    internal class ComplexAnswer: IAnswer
    {
        private readonly OllamaApiClient ollama;
        private string instructionsToFollow;

        public ComplexAnswer(OllamaApiClient model)
        {
            // set up the client
            ollama = model;
        }

        public ComplexAnswer SetInstructions(string instructions)
        {
            instructionsToFollow = instructions;
            return this;
        }

        public async Task<string> GetAnswer(string history, string shortHistory, string message, string user, Action<string, bool> feedback, Action pingAlive)
        {
            var promptFormat = new MessageContainer();
            promptFormat.SetContext("History:" + history + " this is the user talking: " + user );
            promptFormat.SetUserPrompt(message);
            promptFormat.SetInstructions(instructionsToFollow);
    
            var response = "";
            //feedback.Invoke("Give me a moment", true);
            pingAlive.Invoke();
            var count = 0;
            await foreach (var stream in OllamaFormater.GenerateResponse(ollama, promptFormat))
            {
                if (count % 100 == 0)
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
