using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{
    public class Stats : TableEntity
    {
        public Stats(string memory, string cpu, string queueSize, string tableSize, string totalCrawled, string errorSize,
                     string one, string two, string three, string four, string five,
                     string six, string seven, string eight, string nine, string ten)
        {
            this.PartitionKey = "UniquePK";
            this.RowKey = "UniqueRK";
            this.Memory = memory;
            this.Cpu = cpu;
            this.QueueSize = queueSize;
            this.TableSize = tableSize;
            this.TotalCrawled = totalCrawled;
            this.ErrorSize = errorSize;
            this.One = one;
            this.Two = two;
            this.Three = three;
            this.Four = four;
            this.Five = five;
            this.Six = six;
            this.Seven = seven;
            this.Eight = eight;
            this.Nine = nine;
            this.Ten = ten;
        }

        public Stats() { }
        public string Memory { get; set; }
        public string Cpu { get; set; }
        public string QueueSize { get; set; }
        public string TableSize { get; set; }
        public string TotalCrawled { get; set; }
        public string ErrorSize { get; set; }
        public string One { get; set; }
        public string Two { get; set; }
        public string Three { get; set; }
        public string Four { get; set; }
        public string Five { get; set; }
        public string Six { get; set; }
        public string Seven { get; set; }
        public string Eight { get; set; }
        public string Nine { get; set; }
        public string Ten { get; set; }


    }
}
