using System.Collections.Generic;
using System.Linq;

namespace APIViewWeb.Extensions
{
    public static class LinqExtensions
    {
        public static IEnumerable<T> InterleavedUnion<T>(this IEnumerable<T> first, IEnumerable<T> second)
        {
            var firstList = first.ToList();
            var secondList = second.ToList();
            var result = new List<T>();

            int i = 0, j = 0;
            while (i < firstList.Count && j < secondList.Count)
            {
                if (firstList[i].Equals(secondList[j]))
                {
                    result.Add(firstList[i]);
                    i++;
                    j++;
                }
                else if (secondList.Contains(firstList[i]))
                {
                    result.Add(secondList[j]);
                    j++;
                }
                else
                {
                    result.Add(firstList[i]);
                    i++;
                }
            }

            while (i < firstList.Count)
            {
                result.Add(firstList[i]);
                i++;
            }

            while (j < secondList.Count)
            {
                result.Add(secondList[j]);
                j++;
            }

            return result;
        }
    }
}
