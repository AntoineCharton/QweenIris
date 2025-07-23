using OllamaSharp;
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

        public async Task<string> GetRelevantArticle(string feed, string message, Action pingAlive, Action<string, bool> feedback)
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

                var instruction = mediaFeedList.SearchArticleInstructions;
                var pickedArticle = "";
                await foreach (var stream in normalModel.GenerateAsync(newsArticles + instruction + message))
                {
                    pingAlive.Invoke();
                    pickedArticle += stream.Response;
                }
                pickedArticle = Regex.Replace(pickedArticle, @"<think>[\s\S]*?</think>", "");
                var pickedArticleId = int.Parse(pickedArticle);
                if (pickedArticleId != -1)
                {
                    Console.WriteLine(article.Items[pickedArticleId].ToString());
                    await PushWaitingAnswer(feedback, $"{mediaFeedList.WaitingAnswerInstructions} \n {normalInstructionsToFollow} \n article:{article.Items[pickedArticleId].ToString()}");
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

        public async Task PushWaitingAnswer(Action<string, bool> feedback, string prompt)
        {
            var waitingResponse = "";
            await foreach (var stream in normalModel.GenerateAsync(prompt))
            {
                waitingResponse += stream.Response;
            }
            waitingResponse = Regex.Replace(waitingResponse, @"<think>[\s\S]*?</think>", "");
            feedback.Invoke(waitingResponse, false);
        }

        public async Task<string> GenerateMatchingTags(Action<string, bool> feedback, string prompt)
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
            var instructions = $"{mediaFeedList.GenerateMatchingTagsIntro} {items}, {mediaFeedList.GenerateMatchingTagsInstructions} \n Request: {prompt}";
            await foreach (var stream in normalModel.GenerateAsync(instructions))
            {
                pickedCategory += stream.Response;
            }
            pickedCategory = Regex.Replace(pickedCategory, @"<think>[\s\S]*?</think>", "");
            pickedCategory = Regex.Replace(pickedCategory, @"\s", "");
            Console.WriteLine("Picked: " + pickedCategory);
            return pickedCategory;
        }

        public async Task<string> GetAnswer(PromptContext promptContext, Action<string, bool> feedback, Action pingAlive)
        {
            ShuffleList(mediaFeedList.MediaFeeds);
            List<string> categoriesToMatch = new List<string> {};
            var pickedCategory = await GenerateMatchingTags(feedback, promptContext.Prompt);
            categoriesToMatch.Add(pickedCategory);
            categoriesToMatch.Add("Any");
            KeepMatchedFeeds(mediaFeedList.MediaFeeds, categoriesToMatch.ToArray());
            await PushWaitingAnswer(feedback, $"{mediaFeedList.AknowledgeSearchInstructions} {normalInstructionsToFollow} \n prompt:{promptContext.Prompt}");
            var relevantArticles = "";
            var numberOfArticles = 0;
           
            foreach (var feed in mediaFeedList.MediaFeeds)
            {
                var newArticle = await GetRelevantArticle(feed.Rss, promptContext.Prompt, pingAlive, feedback);
                relevantArticles += newArticle;
                if(!string.IsNullOrWhiteSpace(newArticle))
                {
                    numberOfArticles++;
                }

                if(numberOfArticles > 5)
                {
                    break;
                }

            }

            if(numberOfArticles == 0)
            {
                var nothingMessage = "";
                await foreach (var stream in normalModel.GenerateAsync("User message: " + promptContext.Prompt + normalInstructionsToFollow + mediaFeedList.NoArticleFoundInstructions))
                {
                    pingAlive.Invoke();
                    nothingMessage += stream.Response;
                }
                nothingMessage = Regex.Replace(nothingMessage, @"<think>[\s\S]*?</think>", "");
                return nothingMessage;
            }

            /// Not using standard formating. For some reason this works way better with fewer aluscinations.
            var response = "";
            var date = $"{mediaFeedList.dateIntroduction} {DateTime.Now.Year}";
            var formatedInstruction = $"'{newsInstructionsToFollow}'" + date;
            var user = $"{mediaFeedList.userIntroduction} '{promptContext.User}'";
            var message = $"{mediaFeedList.messageIntroduction} '{promptContext.Prompt}'";
            var history = $"{mediaFeedList.historyIntroduction} '{promptContext.History}'";
            pingAlive.Invoke();
            await foreach (var stream in newsModel.GenerateAsync(formatedInstruction + relevantArticles + user + message))
            {
                pingAlive.Invoke();
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
        [JsonPropertyName("searchArticleInstructions")]
        public string SearchArticleInstructions { get; set; }
        [JsonPropertyName("waitingAnswerInstructions")]
        public string WaitingAnswerInstructions { get; set; }
        [JsonPropertyName("generateMatchingTagsIntro")]
        public string GenerateMatchingTagsIntro { get; set; }
        [JsonPropertyName("generateMatchingTagsInstructions")]
        public string GenerateMatchingTagsInstructions { get; set; }
        [JsonPropertyName("aknowledgeSearchInstructions")]
        public string AknowledgeSearchInstructions { get; set; }
        [JsonPropertyName("noArticleFoundInstructions")]
        public string NoArticleFoundInstructions { get; set; }
        [JsonPropertyName("dateIntroduction")]
        public string dateIntroduction { get; set; }
        [JsonPropertyName("userIntroduction")]
        public string userIntroduction { get; set; }
        [JsonPropertyName("messageIntroduction")]
        public string messageIntroduction { get; set; }
        [JsonPropertyName("historyIntroduction")]
        public string historyIntroduction { get; set; }
    }
}
