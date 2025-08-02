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
        private readonly OllamaApiClient thinkingModel;
        private readonly WebFetcher webFetcher;
        private string normalInstructionsToFollow;
        MediaFeedList mediaFeedList;
        IAnswer fallBackAnswer;
        CancellationToken cancellationToken;

        public NewsSearch(OllamaApiClient newsModel, OllamaApiClient thinkingModel, IAnswer fallbackAnswer, CancellationToken cancellationToken)
        {
            fallBackAnswer = fallbackAnswer;
            webFetcher = new WebFetcher();
            // set up the client
            this.newsModel = newsModel;
            this.thinkingModel = thinkingModel;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NewsSearchList.json");
            string json = File.ReadAllText(path, Encoding.UTF8);
            mediaFeedList = JsonSerializer.Deserialize<MediaFeedList>(json);
            this.cancellationToken = cancellationToken;
        }

        public NewsSearch SetInstructions(string normalInstructions)
        {
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

        async Task<bool> IsArticleRelevant(RSS2Parser.NewsItem item, OllamaApiClient model, string prompt, Action pingAlive)
        {
            var isArticleRelevant = "";
            await foreach (var stream in model.GenerateAsync("Instructions: yes or no is this article related to the prompt, just yes or no, no explanation, just the words \n article:" + 
                item.ToString() +
                "\nPrompt: " + prompt))
            {
                pingAlive.Invoke();
                isArticleRelevant += stream.Response;
            }
            isArticleRelevant = Regex.Replace(isArticleRelevant, @"<think>[\s\S]*?</think>", "");

            bool isRelevant = isArticleRelevant.IndexOf("yes", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isRelevant)
            {
                Console.WriteLine(isArticleRelevant + " " + item.Title);
                return true;
            }

            return false;
        }

        async Task<string> RemoveHTMLAndMarkdownLinksFromText(string item, Action pingAlive)
        {
            var itemWithoutHTML = "";
            await foreach (var stream in newsModel.GenerateAsync("Instructions: remove html, markdown and urls from this text \n text:" + item))
            {
                pingAlive.Invoke();
                itemWithoutHTML += stream.Response;
            }
            itemWithoutHTML = Regex.Replace(itemWithoutHTML, @"<think>[\s\S]*?</think>", "");

            return itemWithoutHTML;
        }

        async Task<string> FormatArticle(string item, string formatInstruction, Action pingAlive)
        {
            var formatedArticle = "";
            await foreach (var stream in newsModel.GenerateAsync($"Instructions: {formatInstruction} Text: {item}" ))
            {
                pingAlive.Invoke();
                formatedArticle += stream.Response;
            }
            formatedArticle = Regex.Replace(formatedArticle, @"<think>[\s\S]*?</think>", "");

            return formatedArticle;
        }


        public async Task<string> GetRelevantArticle(string feed, PromptContext message, Action pingAlive, Action<string, bool> feedback)
        {
            var article = new RSS2Parser();
            try
            {
                await article.ParseRss(webFetcher, feed);
                List<RSS2Parser.NewsItem> relevantItems = new List<RSS2Parser.NewsItem>();
                var newsCount = 0;
                for (var i = 0; i < MathF.Min(article.Items.Count, 30); i++)
                {
                    if(await IsArticleRelevant(article.Items[i], newsModel, message.Prompt, pingAlive))
                    {
                        if (await IsArticleRelevant(article.Items[i], thinkingModel, message.Prompt, pingAlive)) // We make sur the article is relevant with a stronger but slower model
                        {
                            newsCount++;
                            var selectedItem = article.Items[i];
                            selectedItem.Description = await RemoveHTMLAndMarkdownLinksFromText(selectedItem.Description, pingAlive);
                            relevantItems.Add(selectedItem);
                            break;
                        }
                    }
                }

                var pickedArticles = "";

                for(var i = 0; i < relevantItems.Count; i++)
                {
                    var formatedArticle = await FormatArticle(relevantItems[i].ToString(), message.NewsSearchInstructions, pingAlive);
                    pickedArticles += formatedArticle + $"\n[Article]({relevantItems[i].Link})";
                    ;
                    pickedArticles += "\n";
                }

                return pickedArticles;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            Console.WriteLine("Couldn't find anything on" + feed);
            return "";
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
            await foreach (var stream in newsModel.GenerateAsync(instructions))
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
            var relevantArticles = "";
            var numberOfArticles = 0;
            feedback.Invoke("I am looking for an article", true);
            foreach (var feed in mediaFeedList.MediaFeeds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var newArticle = await GetRelevantArticle(feed.Rss, promptContext, pingAlive, feedback);
                relevantArticles += (newArticle + "\n---");
                if(!string.IsNullOrWhiteSpace(newArticle))
                {
                    if(numberOfArticles == 0)
                    {
                        feedback.Invoke("I found one article!", true);
                    }
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
                await foreach (var stream in newsModel.GenerateAsync("User message: " + promptContext.Prompt + normalInstructionsToFollow + mediaFeedList.NoArticleFoundInstructions))
                {
                    pingAlive.Invoke();
                    nothingMessage += stream.Response;
                }
                nothingMessage = Regex.Replace(nothingMessage, @"<think>[\s\S]*?</think>", "");
                nothingMessage = await fallBackAnswer.GetAnswer(promptContext, feedback, pingAlive);
                return nothingMessage;
            }

            return relevantArticles;
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
        [JsonPropertyName("generateMatchingTagsIntro")]
        public string GenerateMatchingTagsIntro { get; set; }
        [JsonPropertyName("generateMatchingTagsInstructions")]
        public string GenerateMatchingTagsInstructions { get; set; }
        [JsonPropertyName("noArticleFoundInstructions")]
        public string NoArticleFoundInstructions { get; set; }
    }
}
