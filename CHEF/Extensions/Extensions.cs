using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CHEF.Extensions
{
    public static class Extensions
    {
        public static string TrimmedValue(this Capture c)
        {
            return c.Value.Trim();
        }

        public static IEnumerable<Y> Attempt<T, Y>(this IEnumerable<T> source, Func<T, Y> action)
        {
            foreach (var c in source)
            {
                Y result;
                try
                {
                    result = action(c);
                }
                catch (Exception e)
                {
                    Logger.Log(e.ToString());
                    continue;
                }
                yield return result;
            }
        }
    }
}