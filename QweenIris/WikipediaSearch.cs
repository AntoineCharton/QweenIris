using Discord;
using OllamaSharp;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;

namespace QweenIris
{

    public class SearchResult
    {
        public string Title { get; set; }
        public string Snippet { get; set; }
        public int PageId { get; set; }
    }

    internal class WikipediaSearch: IAnswer
    {
        private readonly OllamaApiClient quickModel;
        private readonly OllamaApiClient thinkingModel;
        private string instructionsToFollow;
        private CancellationToken cancellationToken;


        public WikipediaSearch(OllamaApiClient quickModel, OllamaApiClient thinkingModel, CancellationToken cancellationToken)
        {
            this.quickModel = quickModel;
            this.thinkingModel = thinkingModel;
            this.cancellationToken = cancellationToken;
        }

        public WikipediaSearch SetInstructions(string instructions)
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
            response = await GetSearchPrompt(promptContext, feedback, pingAlive);
            Console.WriteLine(response);
            return response;
        }

        private async Task<string> GetSearchPrompt(PromptContext promptContext, Action<string, bool> feedback, Action pingAlive)
        {
            var searchWikipediaPrompt = new MessageContainer();
            searchWikipediaPrompt.SetContext("");
            searchWikipediaPrompt.SetUserPrompt("Make a search that could be used on wikipedia search. Only give the important keywords, no formating just the words \n prompt:" + promptContext.Prompt);
            searchWikipediaPrompt.SetInstructions( "Try to extract the widder search possible.\n" + 
                "ex: \n prompt: what happened in mumbai on 2024. output: mumbai. \n prompt: what year was lufa created in montreal. output: lufa montreal \n prompt: when was emmanuel macron born. output: emmanuel macron \n prompt: who is the president of france: president of france.");
            var searchFormat = "";
            searchFormat = await thinkingModel.GenerateResponseWithPing(searchWikipediaPrompt, pingAlive, cancellationToken);
            string search = Regex.Replace(searchFormat, @"<think>[\s\S]*?</think>", "");
            var text = await SearchAndFetchPages(search);
            feedback.Invoke("Searching on wikipedia: " + search, true);

            if(text.Count == 0)
            {
                searchWikipediaPrompt.SetUserPrompt("Make a search that could be used on wikipedia search. Only give the important keywords. Ex: User ask what happened in mumbai on 2024 output: mumbai 2024. No explanation, no formating just the words \n prompt:" + promptContext.Prompt);
                searchFormat = await thinkingModel.GenerateResponseWithPing(searchWikipediaPrompt, pingAlive, cancellationToken);
                search = Regex.Replace(searchFormat, @"<think>[\s\S]*?</think>", "");
                text = await SearchAndFetchPages(search);
                if (text.Count == 0)
                {
                    Console.WriteLine(search);
                    return "Something went wrong :(";
                }
            }

            string parsedInformation = "";
            string longestPotentialAnswer = "";
            foreach (var selectedText in text)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sortedText = SplitEvery2000(selectedText);
                var searchInformationPrompt = new MessageContainer();
                searchInformationPrompt.SetContext("");
                searchInformationPrompt.SetInstructions("Quote this article accordingly. Focus only on answering the prompt question specifically do not add information not asked for. Do not give numbers not present in the article. Only give information inside the article. If article doesn't specify the information answer nothing found");
                var information = "";
                
                foreach (var textSplit in sortedText)
                {
                    searchInformationPrompt.SetUserPrompt("article: " + textSplit + "\n prompt" + promptContext.Prompt);
                    information = await thinkingModel.GenerateResponseWithPing(searchInformationPrompt, pingAlive, cancellationToken);
                    parsedInformation = Regex.Replace(information, @"<think>[\s\S]*?</think>", "");
                    Console.WriteLine(parsedInformation);
                    var isAnswering = await IsAnsweringQuestion(parsedInformation, promptContext.Prompt, pingAlive);
                    Console.WriteLine(isAnswering);
                    parsedInformation += $"\n [Wikipedia]({GetWikipediaSearchUrl(search)})";
                    if (parsedInformation.Length > longestPotentialAnswer.Length)
                        longestPotentialAnswer = parsedInformation;

                    if (isAnswering)
                        return parsedInformation;
                }
            }

            return longestPotentialAnswer;
        }

        public static List<string> SplitEvery2000(string text)
        {
            List<string> result = new List<string>();

            for (int i = 0; i < text.Length; i += 2000)
            {
                int length = Math.Min(2000, text.Length - i);
                result.Add(text.Substring(i, length));
            }

            return result;
        }

        async Task<bool> IsAnsweringQuestion(string asnwer, string prompt, Action pingAlive)
        {
            var searchInformationPrompt = new MessageContainer();
            searchInformationPrompt.SetContext("");
            searchInformationPrompt.SetUserPrompt($"Prompt: {asnwer} + \n Answer 1 if the prompt says 'nothing found'. Otherwise 0. No explanation. Just 1 or 0 .");
            searchInformationPrompt.SetInstructions("");
            var isAnswering = "";
            isAnswering = await quickModel.GenerateResponseWithPing(searchInformationPrompt, pingAlive, cancellationToken);
            bool isPositive = isAnswering.IndexOf("1", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isPositive)
            {
                return true;
            }

            return false;
        }

        private static readonly HttpClient httpClient = new HttpClient();


        static string FormatURL(string query, int limit = 5)
        {
            return $"https://en.wikipedia.org/w/api.php?action=query&list=search&srsearch={Uri.EscapeDataString(query)}&srlimit={limit}&format=json";
        }

        public static string GetWikipediaSearchUrl(string query)
        {
            return $"https://en.wikipedia.org/wiki/Special:Search?search={Uri.EscapeDataString(query)}";
        }

        public static async Task<List<string>> SearchAndFetchPages(string query, int limit = 2)
        {
            var url = FormatURL(query, limit);
            var contents = new List<string>();

            try
            {
                var response = await httpClient.GetStringAsync(url);
                using var searchDoc = JsonDocument.Parse(response);
                var searchResults = searchDoc.RootElement.GetProperty("query").GetProperty("search");

                foreach (var item in searchResults.EnumerateArray())
                {
                    var title = item.GetProperty("title").GetString();
                    var pageText = await GetPagePlainText(title);
                    contents.Add(pageText);
                }

                return contents;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return contents;
            }
        }

        private static async Task<string> GetPagePlainText(string title)
        {
            var extractUrl = $"https://en.wikipedia.org/w/api.php?action=query&prop=extracts&explaintext&titles={Uri.EscapeDataString(title)}&format=json";

            try
            {
                var response = await httpClient.GetStringAsync(extractUrl);
                using var doc = JsonDocument.Parse(response);
                var pages = doc.RootElement.GetProperty("query").GetProperty("pages");

                foreach (var page in pages.EnumerateObject())
                {
                    if (page.Value.TryGetProperty("extract", out var extract))
                        return extract.GetString();
                }

                return "[No content found]";
            }
            catch
            {
                return "[Error fetching page]";
            }
        }
    }
}
