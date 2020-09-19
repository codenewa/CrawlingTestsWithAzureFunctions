using HtmlAgilityPack;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace UltimateCentralCrawlerFunction
{
    public static class ApiTrigger
    {
        [FunctionName("ApiTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            var uri = @"https://ultimatecentral.com/";
            var fetcher = new EventsFetcher();
            var crawler = new EventsNodeCrawler(log);
            var eventCalendar = crawler.ParseEvents(fetcher.GetEventsFromPage(uri));


            string responseMessage = JsonConvert.SerializeObject(eventCalendar);

            return new OkObjectResult(responseMessage);
        }
    }

    #region Model
    public class EventNode
    {
        private readonly HtmlNode _node;

        public EventNode(HtmlNode node)
        {
            _node = node;
        }

        private string GetCountryCode(HtmlNode locationNode)
        {
            return locationNode.SelectNodes("//span[contains(@class,'iti-flag')]").FirstOrDefault()?.Attributes["class"].Value.Split(' ')[1];
        }

        public Event GetEvent()
        {
            var locationNode = _node.ParentNode.SelectNodes("ul").Descendants("li").ToList()[0];
            var dateNode = _node.ParentNode.SelectNodes("ul").Descendants("li").ToList()[1];

            var dateRange = new DateRange(dateNode.InnerText.Trim());
            dateRange.Parse();

            return new Event()
            {
                Name = _node.ChildNodes[1].InnerText,
                Uri = _node.ChildNodes[1].Attributes["href"].Value,
                Location = locationNode.InnerText.Trim(),
                Month = dateRange.Month,
                StartDate = dateRange.StartDate,
                EndDate = dateRange.EndDate,
                CountryCode = GetCountryCode(locationNode)
            };
        }

    }
    public class Event
    {
        public string Name { get; set; }
        public string Uri { get; set; }
        public object Location { get; internal set; }
        public string CountryCode { get; internal set; }
        public string Month { get; internal set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
    public class DateRange
    {
        private readonly string _dateRangeStr;

        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }
        public string Month { get; private set; }

        public DateRange(string dateRangeStr)
        {
            _dateRangeStr = dateRangeStr;
        }

        public void Parse()
        {
            var tokens = _dateRangeStr.Split('-', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Any())
            {
                StartDate = DateTime.Parse(tokens[0].Trim());
                if (tokens.Length == 2)
                {
                    EndDate = DateTime.Parse(tokens[1].Trim());
                }
            }
            Month = $"{CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(StartDate.Month)} {StartDate.Year}";
        }

    }
    #endregion

    public class EventsFetcher
    {
        public HtmlNodeCollection GetEventsFromPage(string uri)
        {

            var web = new HtmlWeb();

            var rawDoc = web.Load(uri);

            var eventListNode = rawDoc.DocumentNode.SelectNodes("//h4[contains(@class,'event-name')]");

            return eventListNode;
        }
    }

    public class EventsNodeCrawler
    {
        private readonly ILogger _log;

        public EventsNodeCrawler(ILogger log)
        {
            _log = log;
        }
        public IEnumerable<Event> ParseEvents(HtmlNodeCollection eventNodes)
        {
            var events = new List<Event>();
            foreach (var node in eventNodes)
            {
                try
                {
                    events.Add(new EventNode(node).GetEvent());
                }
                catch (Exception ex)
                {
                    _log.LogError($"{ex.Message} for node: {node.InnerHtml}");
                }
            }

            return events;
        }
    }
}
