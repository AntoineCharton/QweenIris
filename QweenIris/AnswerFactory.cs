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
          
            complexModel = new OllamaApiClient(uri, "mistral-nemo");
            thinkingModel = new OllamaApiClient(uri, "qwen3");
            simpleModel = new OllamaApiClient(uri, "qwen3:0.6b");
            pressModel = new OllamaApiClient(uri, "qwen3");
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FactoryInstructions.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                promptsList = JsonSerializer.Deserialize<PromptsList>(json);
            }

        }

        public async Task<IAnswer> GetAnswer(string prompt, string history, string characterID, string normalInstructions, string codeInstructions, string newsSearchInstructions, string user, Action pingAlive, Action<string> feedback)
        {
            var targetModel = 0;
            var mostProbableInformation = 0;
            var basicAnswer = new BasicAnswers(simpleModel).SetInstructions(normalInstructions);
            feedback.Invoke(await basicAnswer.GetAnswer("", prompt, user, feedback, pingAlive));
            for(var i = 1; i < promptsList.Prompts.Count; i += 2)
            {
                var affirmationOne = promptsList.Prompts[mostProbableInformation].Prompt;
                var affirmationTwo = promptsList.Prompts[i].Prompt;
                var affirmationThree = promptsList.Prompts[i+1].Prompt;
                var formatedPrompt = promptsList.UserIntroduction + prompt +
                    promptsList.AffirmationOneIntroduction + affirmationOne +
                    promptsList.AffirmationTwoIntroduction + affirmationTwo +
                    promptsList.AffirmationThreeIntroduction + affirmationThree +
                    promptsList.ParsePromptInstructions;

                var targetAnswer = await GetAppropriateAnswer(formatedPrompt, history, simpleModel, pingAlive);
                Console.WriteLine(formatedPrompt);
                if (targetAnswer == 2)
                {
                    mostProbableInformation = i;
                } else if(targetAnswer == 3)
                {
                    mostProbableInformation = i+1;
                }

                //Console.WriteLine(formatedPrompt);
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
                    return new ComplexAnswer(thinkingModel).SetInstructions(characterID + normalInstructions);
                case 2:
                    return new CodeAnswer(complexModel).SetInstructions(codeInstructions);
                case 3:
                    return new NewsSearch(pressModel, simpleModel).SetInstructions(newsSearchInstructions, normalInstructions);
                default:
                    return new ComplexAnswer(thinkingModel).SetInstructions(characterID + normalInstructions);

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
                    /*if (Count % 1000 == 0)
                        pingAlive.Invoke();*/
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
    }
}
