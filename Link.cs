using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class Link : TableEntity
    {
        public Link(string word, Uri address, string title, DateTime dateTime)
        {
            this.PartitionKey = word;
            this.RowKey = Encode(address.OriginalString);
            this.Title = title;
            this.dateTime = dateTime;
        }

        public Link() { }
        public string Title { get; set; }
        public DateTime dateTime { get; set; }

        private static String Encode(String url)
        {
            var keyBytes = System.Text.Encoding.UTF8.GetBytes(url);
            var base64 = System.Convert.ToBase64String(keyBytes);
            return base64.Replace('/', '_');
        }
    }
}
