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

        public async Task<string> GetAnswer(PromptContext promptContext, Action<string, bool> feedback, Action pingAlive)
        {
            feedback.Invoke("Let me think one second", true);
            var promptFormat = new MessageContainer();
            promptFormat.SetContext("History:" + promptContext.History + " this is the user talking: " + promptContext.User);
            promptFormat.SetUserPrompt(promptContext.Prompt);
            promptFormat.SetInstructions(instructionsToFollow);
    
            var response = "";
            //feedback.Invoke("Give me a moment", true);
            pingAlive.Invoke();
            response = await ollama.GenerateResponseWithPing(promptFormat, pingAlive);
            Console.WriteLine(response);
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");
            return output;
        }
    }
   
}
