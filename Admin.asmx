using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using System.Web.Services;

namespace WebRole1
{
    /// <summary>
    /// Summary description for Admin
    /// </summary>
    [WebService(Namespace = "http://tempuri.org/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // To allow this Web Service to be called from script, using ASP.NET AJAX, uncomment the following line. 
    [System.Web.Script.Services.ScriptService]
    public class Admin : System.Web.Services.WebService
    {
        public static Trie trie = new Trie();

        [WebMethod]
        public void DownloadFileFromBlobAndReadIt()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference("wikititle");
            if (container.Exists())
            {
                foreach (IListBlobItem item in container.ListBlobs(null, false))
                {
                    if (item.GetType() == typeof(CloudBlockBlob))
                    {
                        CloudBlockBlob blob = (CloudBlockBlob)item;
                        String path = HostingEnvironment.ApplicationPhysicalPath + "\\wikititle.txt";
                        using (FileStream fs = new FileStream(path, FileMode.Create))
                        {
                            blob.DownloadToStream(fs);
                        }
                        ReadFile(path);
                    }
                }
            }
        }

        [WebMethod]
        private void ReadFile(string path)
        {
            using (StreamReader sr = new StreamReader(path))
            {
                while (sr.EndOfStream == false)
                {
                    string title = sr.ReadLine();
                    trie.AddTitle(title);
                }
                sr.Close();
            }
        }

        [WebMethod]
        //[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string[] Search(string input)
        {
            return trie.BinarySearch(input).ToArray();
            //return trie.SearchForPrefix(input).ToArray();
        }

        //--------------------------------------------------------------------------------------------

        [WebMethod]
        public void Start()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue adminQueue = queueClient.GetQueueReference("adminqueue");
            adminQueue.CreateIfNotExists();
            adminQueue.AddMessage(new CloudQueueMessage("start"));
        }

        [WebMethod]
        public void Stop()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue adminQueue = queueClient.GetQueueReference("adminqueue");
            adminQueue.CreateIfNotExists();
            adminQueue.AddMessage(new CloudQueueMessage("stop"));
        }

        [WebMethod]
        public void Clear()
        {
            Stop();
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue adminQueue = queueClient.GetQueueReference("linkqueue");
            adminQueue.CreateIfNotExists();
            adminQueue.Clear();

            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable linkTable = tableClient.GetTableReference("linktable");
            linkTable.DeleteIfExists(); //deletes table
            Thread.Sleep(5000);
            linkTable.CreateIfNotExists(); //creates table again
        }

        [WebMethod]
        public string[] SearchTable(string input)
        {
            if (input != null)
            {
                CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
                CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
                CloudTable table = tableClient.GetTableReference("linktable");

                input = input.ToLower();
                string[] words = input.Split(' ');
                List<Link> linkList = new List<Link>();
                for (int i = 0; i < words.Length; i++)
                {
                    TableQuery<Link> query = new TableQuery<Link>().Where(
                        TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, words[i]));
                    foreach (Link link in table.ExecuteQuery(query))
                    {
                        linkList.Add(link);
                    }
                }
                var result = (from link in linkList
                              group link by new { Url = link.RowKey, Title = link.Title, Time = link.dateTime } into g
                              orderby g.Count() descending, g.Key.Time descending
                              select new { g.Key.Url, g.Key.Title}).Take(10);
                var resultArray = result.ToArray();
                string[] target = new string[20];
                for (int i = 0; i < resultArray.Length; i++)
                {
                    target[i] = decode(resultArray[i].Url.ToString()) + "***" + resultArray[i].Title;
                }
                return target;
            }
            return new string[0];
        }


        public static string decode(string url)
        {
            byte[] keyBytes = System.Convert.FromBase64String(url);
            string returntext = System.Text.Encoding.UTF8.GetString(keyBytes);
            return returntext;
        }

        private static String Encode(String url)
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(url);
            var base64 = System.Convert.ToBase64String(keyBytes);
            return base64.Replace('/', '_');
        }


        [WebMethod]
        //[ScriptMethod(ResponseFormat = ResponseFormat.Json)]
        public string[] UpdateStats()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("statstable");
            TableOperation operation = TableOperation.Retrieve<Stats>("UniquePK", "UniqueRK");
            Stats result = (Stats)table.Execute(operation).Result;
            string[] resultArray = new string[16];
            resultArray[0] = result.Memory;
            resultArray[1] = result.Cpu;
            resultArray[2] = result.QueueSize;
            resultArray[3] = result.TableSize;
            resultArray[4] = result.TotalCrawled;
            resultArray[5] = result.ErrorSize;
            resultArray[6] = result.One;
            resultArray[7] = result.Two;
            resultArray[8] = result.Three;
            resultArray[9] = result.Four;
            resultArray[10] = result.Five;
            resultArray[11] = result.Six;
            resultArray[12] = result.Seven;
            resultArray[13] = result.Eight;
            resultArray[14] = result.Nine;
            resultArray[15] = result.Ten;
            return resultArray;
        }

    }
}
