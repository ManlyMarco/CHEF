using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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

        /// <summary>
        /// Returns true if the task finished in time, false if the task timed out.
        /// </summary>
        public static Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ContinueWith(resultTask =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (resultTask != task) throw new TimeoutException("Timeout while executing task");
                return task.Result;
            }, cancellationToken);
        }
    }
}