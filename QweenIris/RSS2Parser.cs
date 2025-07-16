using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.ServiceModel.Syndication;

namespace QweenIris
{
    internal class RSS2Parser
    {
        public List<NewsItem> Items { get; set; }

        public async Task ParseRss(WebFetcher webFetcher, string rssUrl)
        {
            try
            {
                Items = new List<NewsItem>();
                using HttpClient client = new HttpClient();
                using var stream = await client.GetStreamAsync(rssUrl);
                using var reader = XmlReader.Create(stream);
                var feed = SyndicationFeed.Load(reader);

                foreach (var item in feed.Items)
                {
                    Items.Add(new NewsItem
                    {
                        Title = item.Title.Text,
                        Link = item.Links[0].Uri.ToString(),
                        PubDate = item.PublishDate.DateTime,
                        Description = item.Summary?.Text ?? string.Empty
                    });
                }
            } catch
            {
                Console.WriteLine(rssUrl + " Something went wrong skipping.");
            }
        }

        public class NewsItem
        {
            public string Title { get; set; }
            public string Description { get; set; }
            public DateTime PubDate { get; set; }
            public string Link { get; set; }

            public override string ToString()
            {
                return $@"Title: {Title} Description: {Description} PubDate: {PubDate:yyyy-MM-dd HH:mm} Link: {Link}";
            }
        }
    }
}
