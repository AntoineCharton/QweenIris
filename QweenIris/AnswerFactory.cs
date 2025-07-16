using Discord.WebSocket;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private readonly string factoryInstructions;

        public AnswerFactory()
        {
            var uri = new Uri("http://localhost:11434");
          
            complexModel = new OllamaApiClient(uri, "mistral-nemo");
            thinkingModel = new OllamaApiClient(uri, "qwen3:latest");
            simpleModel = new OllamaApiClient(uri, "qwen2.5vl:latest");
            pressModel = new OllamaApiClient(uri, "qwen2.5vl:latest");
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FactoryInstructions.txt");
            if (File.Exists(path))
            {
                string content = File.ReadAllText(path);
                factoryInstructions = content;
            }
            else
            {
            }

        }

        public async Task<IAnswer> GetAnswer(string prompt, string history, string normalInstructions, string codeInstructions, string newsSearchInstructions, Action pingAlive)
        {
            var targetAnswer = await GetAppropriateAnswer(prompt, "", simpleModel, pingAlive);
            if (targetAnswer != 0 && targetAnswer != 7 && targetAnswer != 1)
            {
                Console.WriteLine(targetAnswer);
                return GetCodedAnswer(targetAnswer, normalInstructions, codeInstructions, newsSearchInstructions);
            }

            targetAnswer = await GetAppropriateAnswer(prompt, history, thinkingModel, pingAlive);
            Console.WriteLine(targetAnswer);
            return GetCodedAnswer(targetAnswer, normalInstructions, codeInstructions, newsSearchInstructions);
        }

        IAnswer GetCodedAnswer(int targetAnswer, string normalInstructions, string codeInstructions, string newsSearchInstructions)
        {
            switch (targetAnswer)
            {
                case 0:
                    return new ComplexAnswer(thinkingModel).SetInstructions(normalInstructions);
                case 1:
                    return new BasicAnswers(simpleModel).SetInstructions(normalInstructions);
                case 2:
                    return new BasicAnswers(simpleModel).SetInstructions(normalInstructions);
                case 3:
                case 4:
                    return new BasicAnswers(thinkingModel).SetInstructions(normalInstructions);
                case 6:
                    return new CodeAnswer(complexModel).SetInstructions(codeInstructions);
                case 8:
                case 9:
                case 10:
                    return new NewsSearch(pressModel).SetInstructions(newsSearchInstructions);
                default:
                    return new ComplexAnswer(thinkingModel).SetInstructions(normalInstructions);

            }
        }

        public async Task<int> GetAppropriateAnswer(string prompt, string history, OllamaApiClient model, Action pingAlive)
        {
            var instruction = factoryInstructions;
            prompt += instruction;
            var response = "";
            var Count = 0;
            await foreach (var stream in model.GenerateAsync(history + " " + prompt))
            {
                if (Count % 1000 == 0)
                    pingAlive.Invoke();
                Count++;
                response += stream.Response;
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
}
