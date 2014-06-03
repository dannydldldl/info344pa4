using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace WebRole1
{

    public class Trie
    {
        public List<string> titleList;
        public List<string> resultList;
        private int min;
        private int max;
        private int middle;


        public Trie()
        {
            this.titleList = new List<string>();
            this.resultList = new List<String>();
            this.min = 0;
            this.max = 0;
            this.middle = 0;
        }


        public void AddTitle(string title)
        {
            title = title.Replace('_', ' ');
            title = title.ToLower();
            titleList.Add(title);
        }

        
        public List<String> BinarySearch(string input)
        {
            resultList.Clear();
            int index = BinarySearchHelper(input);
            for (int i = index; i < index + 10; i++)
            {
                if (i < titleList.Count())
                    resultList.Add(titleList[i]);
                else
                    return resultList;
            }
            return resultList;
        }


        private int BinarySearchHelper(string input)
        {
            min = 0;
            max = titleList.Count() - 1;
            middle = (min + max) / 2;
            while (min < max)
            {
                if (String.Compare(input, titleList[middle]) == 0)
                {
                    return middle;
                }
                else if (String.Compare(input, titleList[middle]) < 0)
                {
                    max = middle - 1;
                    middle = (min + max) / 2;
                }
                else
                {
                    min = middle + 1;
                    middle = (min + max) / 2;
                }
            }
            return middle;
        }


        /*
        public List<string> SearchForPrefix(string input)
        {
            resultList.Clear();
            int index = titleList.BinarySearch(input);
            if(index < 0)
                return new List<string>();
            for (int i = index; i < index + 10; i++)
            {
                if (i < titleList.Count())
                    resultList.Add(titleList[i]);
                else
                    return resultList;
            }
            return resultList;
        }
        
        
        private void SearchForPrefixHelper(Node node, string result)
        {
            if (resultList.Count() >= 10)
                return;
            if (node.eow == true)
                resultList.Add(result);
            foreach (KeyValuePair<char, Node> pair in node.dict)
            {
                string temp = result;
                temp += pair.Key;
                SearchForPrefixHelper(pair.Value, temp);
            }
        }
        */


    }
}
