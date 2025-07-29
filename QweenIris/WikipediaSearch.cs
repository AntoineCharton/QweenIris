using OllamaSharp;
using System.Collections.Generic;
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

        public WikipediaSearch(OllamaApiClient quickModel, OllamaApiClient thinkingModel)
        {
            this.quickModel = quickModel;
            this.thinkingModel = thinkingModel;
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
            searchWikipediaPrompt.SetContext("History:");
            searchWikipediaPrompt.SetUserPrompt("Make a search that could be used on wikipedia search. Only give the important keywords. Ex: User ask what happened in mumbai on 2024 output: mumbai 2024. No explanation, no formating just the words \n prompt:" + promptContext.Prompt);
            searchWikipediaPrompt.SetInstructions("");
            var searchFormat = "";
            searchFormat = await thinkingModel.GenerateResponseWithPing(searchWikipediaPrompt, pingAlive);
            string search = Regex.Replace(searchFormat, @"<think>[\s\S]*?</think>", "");
            var text = await SearchAndFetchPages(search);
            feedback.Invoke("Searching on wikipedia: " + search, true);

            if(text.Count == 0)
            {
                searchWikipediaPrompt.SetUserPrompt("Make a search that could be used on wikipedia search. Only give the important keywords. Ex: User ask what happened in mumbai on 2024 output: mumbai 2024. No explanation, no formating just the words \n prompt:" + promptContext.Prompt);
                searchFormat = await thinkingModel.GenerateResponseWithPing(searchWikipediaPrompt, pingAlive);
                search = Regex.Replace(searchFormat, @"<think>[\s\S]*?</think>", "");
                text = await SearchAndFetchPages(search);
                if (text.Count == 0)
                {
                    Console.WriteLine(search);
                    return "Something went wrong :(";
                }
            }
            //Console.WriteLine("Wikipedia text: " + text[0] + " ");
            Console.WriteLine(text[0].Length);
            var sortedText = text[0];
            
            var searchInformationPrompt = new MessageContainer();
            searchInformationPrompt.SetContext("History:");
            searchInformationPrompt.SetUserPrompt("This is the article" + sortedText + "Quote this article accordingly. Focus only on answering the prompt question specifically do not add information not asked for: " + promptContext.Prompt);
            searchInformationPrompt.SetInstructions("");
            var information = "";
            information = await thinkingModel.GenerateResponseWithPing(searchInformationPrompt, pingAlive);
            string parsedInformation = Regex.Replace(information, @"<think>[\s\S]*?</think>", "");
            parsedInformation += $"\n [Wikipedia]({GetWikipediaSearchUrl(search)})";
            return parsedInformation;
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

        public static async Task<List<string>> SearchAndFetchPages(string query, int limit = 5)
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
