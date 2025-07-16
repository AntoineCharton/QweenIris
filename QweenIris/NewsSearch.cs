using OllamaSharp;
using OllamaSharp.Models.Chat;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace QweenIris
{
    internal class NewsSearch : IAnswer
    {
        private readonly OllamaApiClient ollama;
        private readonly WebFetcher webFetcher;
        private string instructionsToFollow;
        MediaFeedList mediaFeedList;

        public NewsSearch(OllamaApiClient model)
        {
            webFetcher = new WebFetcher();
            // set up the client
            ollama = model;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NewsSearchList.json");
            string json = File.ReadAllText(path, Encoding.UTF8);
            mediaFeedList = JsonSerializer.Deserialize<MediaFeedList>(json);
        }

        public NewsSearch SetInstructions(string instructions)
        {
            instructionsToFollow = instructions;
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

        public async Task<string> GetRelevantArticle(string feed, string message, Action pingAlive)
        {
            
            var npr = new RSS2Parser();
            try
            {
                await npr.ParseRss(webFetcher, feed);
                var newsArticles = "";
                for (var i = 0; i < npr.Items.Count; i++)
                {
                    newsArticles += $"--{i}--";
                    newsArticles += npr.Items[i].ToString() + "\n";
                }

                var pickCount = 0;
                var instruction = "Pick one article here that would match the user request. Only output the number associated with that article. If nothing is found just output -1. No explanation, no extra text — just the number.";
                var pickedArticle = "";
                await foreach (var stream in ollama.GenerateAsync(newsArticles + instruction + message))
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
                    Console.WriteLine(npr.Items[pickedArticleId].ToString());
                    return "\n" + npr.Items[pickedArticleId].ToString();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.WriteLine("Couldn't find anything on" + feed);
            return "";
        }

        public async Task<string> GetAnswer(string history, string message, string user, Action<string> feedback, Action pingAlive)
        {
            ShuffleList(mediaFeedList.MediaFeeds);
            feedback.Invoke("Looking online for articles.");
            var relevantArticles = "";
            var numberOfArticles = 0;
            foreach (var feed in mediaFeedList.MediaFeeds)
            {
                var newArticle = await GetRelevantArticle(feed.Rss, message, pingAlive);
                relevantArticles += newArticle;
                if(!string.IsNullOrWhiteSpace(newArticle))
                {
                    numberOfArticles++;
                }

                if(numberOfArticles > 1)
                {
                    break;
                }

            }
            var synthetised = relevantArticles;
            var response = "";
            var date = $"This is the date {DateTime.Now}";
            var formatedInstruction = $"Your instructions are: '{instructionsToFollow}'" + date;
            user = $"The user name is: '{user}'";
            message = $"This is the message: '{message}'";
            var parsedInformations = synthetised;
            pingAlive.Invoke();
            var count = 0;
            await foreach (var stream in ollama.GenerateAsync(formatedInstruction + parsedInformations + user + message))
            {
                if (count % 500 == 0)
                {
                    pingAlive.Invoke();
                }
                count++;
                response += stream.Response;
            }
            Console.WriteLine(response);
            string output = Regex.Replace(response, @"<think>[\s\S]*?</think>", "");
            return output;
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
