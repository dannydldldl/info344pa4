using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Diagnostics;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.Storage;
using System.Configuration;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System.IO;
using System.Xml.Linq;
using System.Xml;
using System.Globalization;
using HtmlAgilityPack;

namespace WorkerRole1
{

    public class WorkerRole : RoleEntryPoint
    {
        public Boolean on;
        public HashSet<String> disallowedWordSet;
        public HashSet<String> visitedUrlSet;
        public DateTime currentDateTime;
        public CloudQueue adminQueue;
        public CloudQueue linkQueue;
        public CloudTable linkTable;
        public CloudTable statsTable;
        public WebClient webClient;
        public HtmlWeb htmlWeb;
        public int queueSize;
        public int tableSize;
        public int errorSize;
        public string[] last10;
        public int last10Counter;

        public override void Run()
        {
            // This is a sample worker implementation. Replace with your logic.
            Trace.TraceInformation("WorkerRole1 entry point called");

            this.on = false;
            this.disallowedWordSet = new HashSet<String>();
            this.visitedUrlSet = new HashSet<String>();
            this.currentDateTime = DateTime.Now;
            QueueConnection();
            TableConnection();
            this.webClient = new WebClient();
            this.htmlWeb = new HtmlWeb();
            this.queueSize = 0;
            this.tableSize = 0;
            this.errorSize = 0;
            this.last10 = new string[10];
            this.last10Counter = 0;

            while (true)
            {
                if (CheckAdminQueue())
                    AddToTableAndCrawl();
                UpdatePerformance();

                Thread.Sleep(500);
                Trace.TraceInformation("Working");
            }
        }

        //----------------------------------------------------------------------------------------------

        public Boolean CheckAdminQueue()
        {
            string command = GetMessageFromQueue(adminQueue);
            if (command != null)
            {
                if (command.Equals("start"))
                {
                    on = true;
                    InitializeQueue("http://www.cnn.com/robots.txt");
                    //InitializeQueue("http://sportsillustrated.cnn.com/robots.txt");
                }
                else if (command.Equals("stop"))
                    on = false;
            }
            return on;
        }


        //--------------------------------------------------------------------------------------------------------------------


        public void TableConnection()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            linkTable = tableClient.GetTableReference("linktable");
            linkTable.CreateIfNotExists();
            statsTable = tableClient.GetTableReference("statstable");
            statsTable.CreateIfNotExists();
        }

        public void QueueConnection()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            adminQueue = queueClient.GetQueueReference("adminqueue");
            adminQueue.CreateIfNotExists(); //debug use, must exist
            linkQueue = queueClient.GetQueueReference("linkqueue");
            linkQueue.CreateIfNotExists();
        }


        //-----------------------------------------------------------------------------------------------------------------


        public void InitializeQueue(string robotsTxt)
        {
            Uri uri = new Uri(robotsTxt);
            string host = uri.Host;
            string readRobotsTxt = webClient.DownloadString(robotsTxt);
            List<string> sitemapList = new List<string>();
            using (StringReader stringReader = new StringReader(readRobotsTxt))
            {
                string line;
                while ((line = stringReader.ReadLine()) != null)
                {
                    if (line.StartsWith("Sitemap:"))
                        sitemapList.Add(line.Substring(9));
                    else if (line.StartsWith("Disallow:"))
                        disallowedWordSet.Add(line.Substring(10));
                }
            }
            foreach (string sitemap in sitemapList)
            {
                if (!on) { return; }
                CrawlXml(sitemap, host);
            }
        }

        public void CrawlXml(string sitemap, string host)
        {
            XmlDocument XmlDoc = new XmlDocument();
            XmlDoc.Load(sitemap);
            //more xml to process
            if (XmlDoc.GetElementsByTagName("sitemap").Count > 0)
            {
                foreach (XmlNode sitemapNode in XmlDoc.GetElementsByTagName("sitemap"))
                {
                    if (!on) { return; }
                    //CNN
                    if (host == "www.cnn.com")
                    {
                        if (LessThan2Months(sitemapNode["lastmod"].InnerText))
                        {
                            CrawlXml(sitemapNode["loc"].InnerText, host);
                        }
                        //SI
                    }
                    else
                    {
                        string loc = sitemapNode["loc"].InnerText;
                        if (loc.Contains("nba"))
                        {
                            CrawlXml(loc, host);
                        }
                    }
                }
                //hits last xml, add url to queue
            }
            else
            {
                foreach (XmlNode xmlNode in XmlDoc.GetElementsByTagName("url"))
                {
                    if (!on) { return; }
                    string loc = xmlNode["loc"].InnerText;
                    AddToQueue(loc);
                }
            }
        }


        //-------------------------------------------------------------------------------------------------------------


        public void AddToTableAndCrawl()
        {
            string url = GetMessageFromQueue(linkQueue);
            if (url != null)
            {
                try
                {
                    //HtmlWeb htmlWeb = new HtmlWeb();
                    HtmlDocument document = htmlWeb.Load(url);
                    AddToTable(url, document);
                    Crawl(url, document);
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred: '{0}'", e);
                    errorSize++;
                }
            }
        }

        public void AddToTable(string url, HtmlDocument document)
        {
            string title = document.DocumentNode.SelectSingleNode("//title").InnerText;
            HtmlNode dateTime = document.DocumentNode.SelectSingleNode("//meta[@http-equiv='last-modified']");
            if (dateTime != null)
                AddToTableHelper(title, url, dateTime);
            else
            {
                dateTime = document.DocumentNode.SelectSingleNode("//meta[@name='last-published']");
                AddToTableHelper(title, url, dateTime);
            }
        }

        //help to find if dateTime exists
        public void AddToTableHelper(string title, string url, HtmlNode htmlNode)
        {
            DateTime dateTime;
            if (htmlNode != null)
                DateTime.TryParse(htmlNode.Attributes["content"].Value, out dateTime);
            else
                dateTime = new DateTime(1989, 11, 16); //my birthday, does not mean anything
            string lowerCaseTitle = title.ToLower();
            string[] words = lowerCaseTitle.Split(' ');
            foreach (string word in words)
            {
                if (!on) { return; }
                TableOperation insertOperation = TableOperation.InsertOrReplace(new Link(word, new Uri(url), title, dateTime));
                linkTable.Execute(insertOperation);
                tableSize++;
            }
        }

        public void Crawl(string url, HtmlDocument document)
        {
            Uri uri = new Uri(url);
            foreach (HtmlNode link in document.DocumentNode.SelectNodes("//a[@href]"))
            {
                if (!on) { return; } //stop the loop
                string href = link.Attributes["href"].Value;
                if (IsValidUrl(href))
                {
                    if (!href.StartsWith("http"))
                        href = "http://" + uri.Host + href;
                    if (href.Contains("cnn.com"))
                    {
                        AddToQueue(href);
                        AddToLast10(href);
                    }
                }
            }
        }


        public void AddToLast10(string href)
        {
            if (last10Counter > 9)
                last10Counter = 0;
            last10[last10Counter] = href;
            last10Counter++;
        }


        public Boolean IsValidUrl(string href)
        {
            if (href.EndsWith("html") || href.EndsWith("htm") || href.Contains(".html?") || href.Contains(".htm?"))
            {
                foreach (string disallowedWord in disallowedWordSet)
                {
                    if (href.Contains(disallowedWord))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }


        //HelpMethods--------------------------------------------------------------------------------------------------------


        public string GetMessageFromQueue(CloudQueue queue)
        {
            CloudQueueMessage message = queue.GetMessage();
            if (message == null)
                return null;
            queue.DeleteMessage(message);
            if (queue.Name == "linkqueue")
                queueSize--;
            return message.AsString;
        }


        public Boolean LessThan2Months(string lastmodString)
        {
            DateTime lastmodDateTime;
            DateTime.TryParse(lastmodString, out lastmodDateTime);
            return DateTime.Compare(lastmodDateTime, currentDateTime) < 90;
        }


        public void AddToQueue(string url)
        {
            if (!visitedUrlSet.Contains(url))
            {
                linkQueue.AddMessage(new CloudQueueMessage(url));
                queueSize++;
                visitedUrlSet.Add(url);
            }
        }


        public void UpdatePerformance()
        {
            string memory = GetAvailableMemory().ToString();
            string cpu = GetCpuUtilization().ToString();
            string temp1 = queueSize.ToString();
            string temp2 = tableSize.ToString();
            string temp3 = visitedUrlSet.Count.ToString();
            string temp4 = errorSize.ToString();
            TableOperation retrieveOperation = TableOperation.Retrieve<Stats>("UniquePK", "UniqueRK");
            TableResult result = statsTable.Execute(retrieveOperation);
            Stats updateStats = (Stats)result.Result;

            updateStats.Memory = memory;
            updateStats.Cpu = cpu;
            updateStats.QueueSize = temp1;
            updateStats.TableSize = temp2;
            updateStats.TotalCrawled = temp3;
            updateStats.ErrorSize = temp4;
            updateStats.One = last10[0];
            updateStats.Two = last10[1];
            updateStats.Three = last10[2];
            updateStats.Four = last10[3];
            updateStats.Five = last10[4];
            updateStats.Six = last10[5];
            updateStats.Seven = last10[6];
            updateStats.Eight = last10[7];
            updateStats.Nine = last10[8];
            updateStats.Ten = last10[9];

            TableOperation updateOperation = TableOperation.Replace(updateStats);
            statsTable.Execute(updateOperation);
        }


        public string GetAvailableMemory()
        {
            PerformanceCounter memProcess = new PerformanceCounter("Memory", "Available MBytes");
            return memProcess.NextValue().ToString() + "mbs";
        }


        public string GetCpuUtilization()
        {
            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            float temp = cpuCounter.NextValue();
            Thread.Sleep(500);
            float cpuUsage = cpuCounter.NextValue();
            string cpuUsageString = cpuUsage.ToString();
            return cpuUsageString + "%";
        }


        //--------------------------------------------------------------------------------------------------------

        public override bool OnStart()
        {
            // Set the maximum number of concurrent connections 
            ServicePointManager.DefaultConnectionLimit = 12;

            // For information on handling configuration changes
            // see the MSDN topic at http://go.microsoft.com/fwlink/?LinkId=166357.

            return base.OnStart();
        }


    }
}
