using OllamaSharp;
using System.Text.RegularExpressions;

namespace QweenIris
{
    internal class BasicAnswers: IAnswer
    {
        private readonly OllamaApiClient ollama;
        private string instructionsToFollow;
        private CancellationToken token;

        public BasicAnswers(OllamaApiClient model, CancellationToken token) {
            ollama = model;
            this.token = token;
        }

        public BasicAnswers SetInstructions(string instructions)
        {
            instructionsToFollow = instructions;
            return this;
        }

        public async Task<string> GetAnswer(PromptContext promptContext, Action<string, bool> feedback, Action pingAlive)
        {
            var promptFormat = new MessageContainer();
            promptFormat.SetInstructions(instructionsToFollow);
            var user = "Name:" + promptContext.User;
            promptFormat.SetContext(promptContext.ShortHistory);
            promptFormat.SetUserPrompt(promptContext.Prompt);
            pingAlive.Invoke();
            var response = "";
            try
            {
                response = await ollama.GenerateResponseWithPing(promptFormat, pingAlive, token);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return await GetAnswer(promptContext, feedback, pingAlive);
            }
            Console.WriteLine(response);
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");
            return output;
        }
    }

    interface IAnswer
    {
        public Task<string> GetAnswer(PromptContext promptContext, Action<string, bool> feedback, Action pingAlive);
    }
}
