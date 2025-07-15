using Discord.WebSocket;
using OllamaSharp;
using System;
using System.Collections.Generic;
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

        public AnswerFactory()
        {
            var uri = new Uri("http://localhost:11434");
            complexModel = new OllamaApiClient(uri, "mistral-nemo:latest");
            thinkingModel = new OllamaApiClient(uri, "qwen3");
            simpleModel = new OllamaApiClient(uri, "qwen2.5vl");
            
        }

        public async Task<IAnswer> GetAnswer(string prompt, string normalInstructions, string codeInstructions, string newsSearchInstructions)
        {
            var targetAnswer = await GetAppropriateAnswer(prompt);
            Console.WriteLine(targetAnswer);
            switch(targetAnswer)
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
                case 9:
                case 10:
                    return new NewsSearch(complexModel).SetInstructions(newsSearchInstructions);
                default:
                    return new ComplexAnswer(thinkingModel).SetInstructions(normalInstructions);

            }
                
        }

        public async Task<int> GetAppropriateAnswer(string prompt)
        {
            var instruction = "You are a model that evaluates a user's intent and returns a single number from 0 to 10, based on the following fixed mapping (no skipped numbers):\r\n\r\n0 = The user is clearly referring to something earlier in the same conversation — this includes follow-up questions, clarifications, or referencing a previously discussed topic (e.g., “what about the other one?”, “can you go back to that?”, “like I said before…”)\r\n\r\n1 = General conversation or open-ended chatting (e.g., “what’s on your mind?”, “let’s talk”, “how was your day?”)\r\n\r\n2 = Casual greeting or small talk (e.g., “hi”, “hello”, “hey”, “yo”, “good morning”)\r\n\r\n3 = Asking personal questions (e.g., “what’s your name?”, “where are you from?”, “how old are you?”)\r\n\r\n4 = Entertainment or fun (e.g., jokes, memes, games, trivia, “tell me something funny”)\r\n\r\n5 = Asking for help or non-technical advice (e.g., “how to stay motivated?”, “how do I deal with stress?”)\r\n\r\n6 = Technical help or anything related to coding, programming, or software (e.g., “what’s wrong with this Python code?”, “how to fix a null reference error?”)\r\n\r\n7 = Learning or trying to understand general (non-coding) concepts (e.g., “how does the stock market work?”, “what is entropy?”)\r\n\r\n8 = Researching factual information (e.g., “who was the first president of France?”, “how many countries use the euro?”)\r\n\r\n9 = Looking for current events or recent updates (e.g., “what’s going on in Canada today?”, “any news on the wildfires?”)\r\n\r\n10 = Explicitly looking for news (e.g., “latest headlines”, “breaking news on Ukraine”, “what’s in today’s paper?”)\r\n\r\nReturn only one number from 0 to 10, based solely on the user's message. No explanation, no extra text — just the number.";
            prompt += instruction;
            var response = "";
            await foreach (var stream in thinkingModel.GenerateAsync(prompt))
            {
                response += stream.Response;
            }
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");
            return int.Parse(output);
        }
    }
}
