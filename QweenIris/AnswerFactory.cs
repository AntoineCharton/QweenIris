using Discord.WebSocket;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

        public async Task<IAnswer> GetAnswer(string prompt, string history, string characterID, string normalInstructions, string codeInstructions, string newsSearchInstructions, string user, Action pingAlive, Action<string, bool> feedback)
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
                var basicAnswer = new BasicAnswers(simpleModel).SetInstructions(normalInstructions + " Do not share any links");
                feedback.Invoke(await basicAnswer.GetAnswer("", prompt, user, feedback, pingAlive), true);
            }
            else
            {
                searchModel = thinkingModel;
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
                    promptsList.AffirmationThreeIntroduction + affirmationThree +
                    promptsList.ParsePromptInstructions;

                var targetAnswer = await GetAppropriateAnswer(formatedPrompt, history, searchModel, pingAlive);
                if (targetAnswer == 2)
                {
                    mostProbableInformation = i;
                } else if(targetAnswer == 3)
                {
                    mostProbableInformation = i+1;
                }
            }

            targetModel = promptsList.Prompts[mostProbableInformation].Categories;

            Console.WriteLine("Selected new prompt: " + promptsList.Prompts[mostProbableInformation].Prompt);
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

        public async Task<int> GetAppropriateAnswer(string prompt, string history, OllamaApiClient model, Action pingAlive)
        {
            var instruction = promptsList;
            var response = "";
            var Count = 0;
            try
            {
                await foreach (var stream in model.GenerateAsync(prompt))
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
}
