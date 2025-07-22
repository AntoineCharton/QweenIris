using Discord;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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

        public async Task<IAnswer> GetAnswer(string prompt, string history, string shortHistory, string characterID, string normalInstructions, string codeInstructions, string newsSearchInstructions, string user, Action pingAlive, Action<string, bool> feedback)
        {
            var codeElements = CountBraces(prompt);
            Console.WriteLine(codeElements);
            if (codeElements > 4)
            {
                return new CodeAnswer(complexModel).SetInstructions(codeInstructions);
            }    
            var targetModel = 0;
            var mostProbableInformation = 0;
            var searchModel = simpleModel;
            if (prompt.Length < 100)
            {
                var basicAnswer = new BasicAnswers(simpleModel).SetInstructions(normalInstructions);
                feedback.Invoke(await basicAnswer.GetAnswer("", "", prompt, user, feedback, pingAlive), true);
            }
            else
            {
                searchModel = simpleModel;
            }
            for(var i = 1; i < promptsList.Prompts.Count; i += 2)
            {
                var affirmationOne = promptsList.Prompts[mostProbableInformation].Prompt;
                var affirmationTwo = promptsList.Prompts[i].Prompt;
                var nextStep = i + 1;
                if (i + 1 >= promptsList.Prompts.Count)
                    nextStep = i;
                var affirmationThree = promptsList.Prompts[nextStep].Prompt;

                var formatedPrompt = promptsList.UserIntroduction + prompt +
                    promptsList.AffirmationOneIntroduction + affirmationOne +
                    promptsList.AffirmationTwoIntroduction + affirmationTwo +
                    promptsList.AffirmationThreeIntroduction + affirmationThree;

                var promptFormat = new MessageContainer();
                //promptFormat.SetContext("History:" + shortHistory);
                promptFormat.SetUserPrompt(formatedPrompt);
                promptFormat.SetInstructions(promptsList.ParsePromptInstructions);
                

                var targetAnswer = await GetAppropriateAnswer(promptFormat, history, searchModel, pingAlive);
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
            return GetCodedAnswer(targetModel, prompt, characterID, normalInstructions, codeInstructions, newsSearchInstructions);
        }

        IAnswer GetCodedAnswer(int targetAnswer,string prompt, string characterID, string normalInstructions, string codeInstructions, string newsSearchInstructions)
        {
            switch (targetAnswer)
            {
                case 0:
                    return null;
                case 1:
                    return new ComplexAnswer(thinkingModel).SetInstructions(characterID);
                case 2:
                    return new CodeAnswer(complexModel).SetInstructions(codeInstructions);
                case 3:
                    return new NewsSearch(pressModel, simpleModel).SetInstructions(newsSearchInstructions, normalInstructions);
                default:
                    return new ComplexAnswer(thinkingModel).SetInstructions(characterID);

            }
        }

        public async Task<int> GetAppropriateAnswer(MessageContainer prompt, string history, OllamaApiClient model, Action pingAlive)
        {
            var instruction = promptsList;
            var response = "";
            var Count = 0;
            try
            {

                await foreach (var stream in OllamaFormater.GenerateResponse(model, prompt))
                {
                    if (Count % 500 == 0)
                        pingAlive.Invoke();
                    Count++;
                    response += stream.Response;
                }
            } catch
            {
                pingAlive.Invoke();
                await GetAppropriateAnswer(prompt, history, model, pingAlive);
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
        public static async IAsyncEnumerable<GenerateResponseStream?> GenerateResponse(OllamaApiClient ollama, MessageContainer message)
        {
            var prompt = message.GetJsonString();
            await foreach (var item in ollama.GenerateAsync(prompt))
            {
                yield return item;
            }
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
