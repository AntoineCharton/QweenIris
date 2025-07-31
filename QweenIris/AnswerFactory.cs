using OllamaSharp;
using OllamaSharp.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace QweenIris
{
    internal class AnswerFactory
    {
        private readonly OllamaApiClient thinkingModel;
        private readonly OllamaApiClient complexModel;
        private readonly OllamaApiClient simpleModel;
        private readonly OllamaApiClient pressModel;
        private PromptsList promptsList;

        public AnswerFactory()
        {
            var uri = new Uri("http://localhost:11434");
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FactoryInstructions.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                promptsList = JsonSerializer.Deserialize<PromptsList>(json);
                complexModel = new OllamaApiClient(uri, promptsList.ComplexModel);
                thinkingModel = new OllamaApiClient(uri, promptsList.ThinkingModel);
                simpleModel = new OllamaApiClient(uri, promptsList.SimpleModel);
                pressModel = new OllamaApiClient(uri, promptsList.PressModel);
            }
        }

        private int CountBraces(string sentence)
        {
            var count = 0;
            foreach (char c in sentence)
            {
                if (c == '{') count++;
                else if (c == '}') count++;
                else if (c == '[') count++;
                else if (c == ']') count++;
            }
            return count;
        }

        public async Task<IAnswer> GetAnswer(PromptContext promptContext, Action pingAlive, Action<string, bool> feedback, CancellationToken cancellationToken)
        {
            var codeElements = CountBraces(promptContext.Prompt);
            Console.WriteLine(codeElements);
            if (codeElements > 4)
            {
                return new CodeAnswer(complexModel).SetInstructions(promptContext.CodeInstructions);
            }    
            var targetModel = 0;
            var mostProbableInformation = 0;
            var searchModel = simpleModel;
            for(var i = 1; i < promptsList.Prompts.Count; i += 2)
            {
                var affirmationOne = promptsList.Prompts[mostProbableInformation].Prompt;
                var affirmationTwo = promptsList.Prompts[i].Prompt;
                var nextStep = i + 1;
                if (i + 1 >= promptsList.Prompts.Count)
                    nextStep = i;
                var affirmationThree = promptsList.Prompts[nextStep].Prompt;

                var formatedPrompt = promptsList.UserIntroduction + promptContext.Prompt +
                    promptsList.AffirmationOneIntroduction + affirmationOne +
                    promptsList.AffirmationTwoIntroduction + affirmationTwo +
                    promptsList.AffirmationThreeIntroduction + affirmationThree;

                var promptFormat = new MessageContainer();
                //promptFormat.SetContext("History:" + shortHistory);
                promptFormat.SetUserPrompt(formatedPrompt);
                promptFormat.SetInstructions(promptsList.ParsePromptInstructions);
                

                var targetAnswer = await GetAppropriateAnswer(promptFormat, promptContext.History, searchModel, pingAlive, cancellationToken);
                if (targetAnswer == 2)
                {
                    mostProbableInformation = i;
                } else if(targetAnswer == 3)
                {
                    mostProbableInformation = i+1;
                }
            }

            if(promptsList.Prompts.Count > mostProbableInformation)
                targetModel = promptsList.Prompts[mostProbableInformation].Categories;

            try
            {
                Console.WriteLine("Selected new prompt: " + promptsList.Prompts[mostProbableInformation].Prompt);
            } catch
            {
                Console.WriteLine("Oops");
            }
            return GetCodedAnswer(targetModel, promptContext, cancellationToken);
        }

        IAnswer GetCodedAnswer(int targetAnswer, PromptContext promptContext, CancellationToken cancellationToken)

        {
            switch (targetAnswer)
            {
                case 0:
                    return new BasicAnswers(simpleModel, cancellationToken).SetInstructions(promptContext.NormalInstructions);
                case 1:
                    return new ComplexAnswer(complexModel, cancellationToken).SetInstructions(promptContext.CharacterId);
                case 2:
                    return new CodeAnswer(complexModel).SetInstructions(promptContext.CodeInstructions);
                case 3:
                    return new NewsSearch(pressModel, new WikipediaSearch(simpleModel, thinkingModel, cancellationToken).SetInstructions(promptContext.CharacterId), cancellationToken).SetInstructions(promptContext.NormalInstructions);
                case 4:
                    return new WikipediaSearch(simpleModel, thinkingModel, cancellationToken).SetInstructions(promptContext.CharacterId);
                default:
                    return new ComplexAnswer(complexModel, cancellationToken).SetInstructions(promptContext.CharacterId);

            }
        }

        public async Task<int> GetAppropriateAnswer(MessageContainer prompt, string history, OllamaApiClient model, Action pingAlive, CancellationToken cancellationToken)
        {
            var instruction = promptsList;
            var response = "";
            var Count = 0;
            try
            {
                response = await model.GenerateResponseWithPing(prompt, pingAlive, cancellationToken);
            } catch (OperationCanceledException)
            {
               
            } catch
            {
                pingAlive.Invoke();
                await GetAppropriateAnswer(prompt, history, model, pingAlive, cancellationToken);
            }
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");
            try
            {
                return int.Parse(output);
            }
            catch
            {
                return 0;
            }
        }
    }

    public class PromptData
    {
        public string Prompt { get; set; }
        public int Categories { get; set; }
    }

    public class PromptsList
    {
        public List<PromptData> Prompts { get; set; }
        public string ParsePromptInstructions { get; set; }
        public string UserIntroduction { get; set; }
        public string AffirmationOneIntroduction { get; set; }
        public string AffirmationTwoIntroduction { get; set; }
        public string AffirmationThreeIntroduction { get; set; }
        public string ThinkingModel { get; set; }
        public string SimpleModel { get; set; }
        public string ComplexModel { get; set; }
        public string PressModel { get; set; }
       
    }

    public class Message
    {
        public string Role { get; set; }
        public string Content { get; set; }

        private string context;
        private string prompt;

        public Message(string role) 
        { 
            Role = role;
        }

        public void SetInstructions(string instructions)
        {
            Content = instructions;
        }

        public void SetContext(string newContext)
        {
            context = newContext;
            FormatContext();
        }

        public void SetPrompt(string newPrompt)
        {
            prompt = newPrompt;
            FormatContext();
        }

        void FormatContext()
        {
            Content = "Context:\n" + context + "\n\n---\n\n" + "Question:\n" + prompt;
        }
    }

    public static class OllamaFormater
    {
        public static async IAsyncEnumerable<GenerateResponseStream?> GenerateResponse(this OllamaApiClient ollama, MessageContainer message)
        {
            var prompt = message.GetJsonString();
            await foreach (var item in ollama.GenerateAsync(prompt))
            {
                yield return item;
            }
        }

        public static async Task<string> GenerateResponseWithPing(this OllamaApiClient ollama, MessageContainer message, Action pingAlive, CancellationToken cancellationToken)
        {
            var response = "";
            await foreach (var stream in ollama.GenerateResponse(message))
            {
                cancellationToken.ThrowIfCancellationRequested();
                pingAlive.Invoke();
                response += stream.Response;
            }

            return response;
        }
    }

    public class MessageContainer
    {
        public List<Message> Messages { get; set; }

        public MessageContainer() {
            Messages = new List<Message>();
            Messages.Add(new Message("System"));
            Messages.Add(new Message("User"));
        }

        public void SetInstructions(string instruction)
        {
            Messages[0].SetInstructions(instruction);
        }

        public void SetContext(string context)
        {
            Messages[1].SetContext(context);
        }

        public void SetUserPrompt(string prompt)
        {
            Messages[1].SetPrompt(prompt);
        }

        public string GetJsonString()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonIndented = JsonSerializer.Serialize(this, options);
            return jsonIndented;
        }
    }
}
