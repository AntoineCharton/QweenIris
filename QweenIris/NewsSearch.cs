using OllamaSharp;
using OllamaSharp.Models.Chat;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace QweenIris
{
    internal class NewsSearch : IAnswer
    {
        private readonly OllamaApiClient newsModel;
        private readonly OllamaApiClient normalModel;
        private readonly WebFetcher webFetcher;
        private string newsInstructionsToFollow;
        private string normalInstructionsToFollow;
        MediaFeedList mediaFeedList;

        public NewsSearch(OllamaApiClient newsModel, OllamaApiClient normalModel)
        {
            webFetcher = new WebFetcher();
            // set up the client
            this.newsModel = newsModel;
            this.normalModel = normalModel;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NewsSearchList.json");
            string json = File.ReadAllText(path, Encoding.UTF8);
            mediaFeedList = JsonSerializer.Deserialize<MediaFeedList>(json);
        }

        public NewsSearch SetInstructions(string newsInstructions, string normalInstructions)
        {
            newsInstructionsToFollow = newsInstructions;
            normalInstructionsToFollow = normalInstructions;
            return this;
        }

        public static void ShuffleList(List<MediaFeeds> list)
        {
            Random rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                MediaFeeds temp = list[n];
                list[n] = list[k];
                list[k] = temp;
            }
        }

        private void KeepMatchedFeeds(List<MediaFeeds> feeds, params string[] categories)
        {
            HashSet<string> categorySet = new HashSet<string>(categories);
            foreach (var feed in feeds.ToList())
            {
                bool shouldRemove = true;

                foreach (string category in feed.Categories)
                {
                    if (categorySet.Contains(category))
                    {
                        shouldRemove = false;
                        break;
                    }
                }

                if (shouldRemove)
                {
                    feeds.Remove(feed);
                }
            }
        }

        public async Task<string> GetRelevantArticle(string feed, string message, Action pingAlive, Action<string> feedback)
        {
            var article = new RSS2Parser();
            try
            {
                await article.ParseRss(webFetcher, feed);
                var newsArticles = "";
                for (var i = 0; i < article.Items.Count; i++)
                {
                    newsArticles += $"--{i}--";
                    newsArticles += article.Items[i].ToString() + "\n";
                }

                var pickCount = 0;
                var instruction = "Pick one article here that would match the user request. If the request is vague, pick articles that are good news or cover light subjects. Only output the number associated with that article. If nothing is found just output -1. No explanation, no extra text — just the number.";
                var pickedArticle = "";
                await foreach (var stream in newsModel.GenerateAsync(newsArticles + instruction + message))
                {
                    if (pickCount % 500 == 0)
                    {
                        pingAlive.Invoke();
                    }
                    pickCount++;
                    pickedArticle += stream.Response;
                }
                pickedArticle = Regex.Replace(pickedArticle, @"<think>[\s\S]*?</think>", "");
                var pickedArticleId = int.Parse(pickedArticle);
                if (pickedArticleId != -1)
                {
                    Console.WriteLine(article.Items[pickedArticleId].ToString());
                    await PushWaitingAnswer(feedback, $"Instructions: Describe what you see. End your sentance by 'I am looking for an article for you.'. {normalInstructionsToFollow} \n article:{article.Items[pickedArticleId].ToString()}");
                    return "\n" + article.Items[pickedArticleId].ToString();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.WriteLine("Couldn't find anything on" + feed);
            return "";
        }

        public async Task PushWaitingAnswer(Action<string> feedback, string prompt)
        {
            var waitingResponse = "";
            await foreach (var stream in normalModel.GenerateAsync(prompt))
            {
                waitingResponse += stream.Response;
            }
            waitingResponse = Regex.Replace(waitingResponse, @"<think>[\s\S]*?</think>", "");
            feedback.Invoke(waitingResponse);
        }

        public async Task<string> GenerateMatchingTags(Action<string> feedback, string prompt)
        {
            HashSet<string> uniqueCategories = new HashSet<string>();

            foreach (var MediaFeed in mediaFeedList.MediaFeeds)
            {
                foreach (string category in MediaFeed.Categories)
                {
                    uniqueCategories.Add(category);
                }
            }

            var items = "";
            Console.WriteLine("Unique categories:");
            foreach (string category in uniqueCategories)
            {
                items += ", " + category;
                Console.WriteLine("- " + category);
            }

            var pickedCategory = "";
            Console.WriteLine(items);
            var instructions = $"Which one of those categories matches the best the request: {items}, your instructions are to pick one that matches the user prompt best. Return only the category, no explanation. \n Request: {prompt}";
            await foreach (var stream in normalModel.GenerateAsync(instructions))
            {
                pickedCategory += stream.Response;
            }
            pickedCategory = Regex.Replace(pickedCategory, @"<think>[\s\S]*?</think>", "");
            pickedCategory = Regex.Replace(pickedCategory, @"\s", "");
            Console.WriteLine("Picked: " + pickedCategory);
            return pickedCategory;
        }

        public async Task<string> GetAnswer(string history, string message, string user, Action<string> feedback, Action pingAlive)
        {
            ShuffleList(mediaFeedList.MediaFeeds);
            List<string> categoriesToMatch = new List<string> {};
            var pickedCategory = await GenerateMatchingTags(feedback, message);
            categoriesToMatch.Add(pickedCategory);
            categoriesToMatch.Add("Any");
            KeepMatchedFeeds(mediaFeedList.MediaFeeds, categoriesToMatch.ToArray());
            await PushWaitingAnswer(feedback, $"Instructions: End your sentance by 'I am looking for an article.'. {normalInstructionsToFollow} \n prompt:{message}");
            var relevantArticles = "";
            var numberOfArticles = 0;
            foreach (var feed in mediaFeedList.MediaFeeds)
            {
                var newArticle = await GetRelevantArticle(feed.Rss, message, pingAlive, feedback);
                relevantArticles += newArticle;
                if(!string.IsNullOrWhiteSpace(newArticle))
                {
                    numberOfArticles++;
                }

                if(numberOfArticles > 2)
                {
                    break;
                }

            }

            if(numberOfArticles == 0)
            {
                var nothingMessage = "";
                var wordCount = 0;
                await foreach (var stream in normalModel.GenerateAsync("User message: " + message + normalInstructionsToFollow + "Say you couldn't find anything. Do not include any link"))
                {
                    if (wordCount % 500 == 0)
                    {
                        pingAlive.Invoke();
                    }
                    wordCount++;
                    nothingMessage += stream.Response;
                }
                nothingMessage = Regex.Replace(nothingMessage, @"<think>[\s\S]*?</think>", "");
                return nothingMessage;
            }

            var response = "";
            var date = $"This is the year {DateTime.Now.Year}";
            var formatedInstruction = $"Your instructions are: '{newsInstructionsToFollow}'" + date;
            user = $"The user name is: '{user}'";
            message = $"This is the message: '{message}'";
            history = $"This is the history of the conversation: '{history}'";
            pingAlive.Invoke();
            var count = 0;
            await foreach (var stream in newsModel.GenerateAsync(formatedInstruction + relevantArticles + user + message))
            {
                if (count % 500 == 0)
                {
                    pingAlive.Invoke();
                }
                count++;
                response += stream.Response;
            }
            response = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");
            return response;
        }
    }

    class ResponseType
    {
        public bool requireSearch;
        public string promptAnswer;
    }

    public class WebFetcher
    {
        public async Task<string> GetHtmlAsync(string url)
        {
            using var httpClient = new HttpClient();
            string html = await httpClient.GetStringAsync(url);
            return html;
        }
    }

    public class MediaFeeds
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; }
        [JsonPropertyName("rss")]
        public string Rss { get; set; }
    }

    public class MediaFeedList
    {
        [JsonPropertyName("mediaFeeds")]
        public List<MediaFeeds> MediaFeeds { get; set; }
    }
}
