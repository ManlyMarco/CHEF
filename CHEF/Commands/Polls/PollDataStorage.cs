using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;

namespace CHEF.Commands.Polls;

public class PollDataStorage
{
    private static string _pollStoragePath;
    private static readonly Timer _DataStoreTimer = new(5000);

    public static ConcurrentDictionary<string, PollData> Polls { get; } = new();

    public static async Task InitDataStorage()
    {
        string pollStoragePath;

        lock (_DataStoreTimer)
        {
            if (_pollStoragePath != null) return;

            // Poll data is stored in a file
            pollStoragePath = Path.GetFullPath("PollData.json");

            _pollStoragePath = pollStoragePath;
        }

        // Read the file if it exists
        if (File.Exists(pollStoragePath))
        {
            try
            {
                Logger.Log("POLLS > Reading poll data from " + pollStoragePath);

                var json = await File.ReadAllTextAsync(pollStoragePath);
                var polls = JsonSerializer.Deserialize<List<PollData>>(json);
                if (polls == null) throw new InvalidDataException("Failed to deserialize poll data from file");
                foreach (var poll in polls)
                    Polls.AddOrUpdate(poll.PollId, poll, (_, _) => poll);
            }
            catch (Exception e)
            {
                Logger.Log($"POLLS > Failed to read poll data from file - {e}");
            }
        }

        _DataStoreTimer.AutoReset = false;
        _DataStoreTimer.Enabled = false;
        _DataStoreTimer.Elapsed += DataStoreTimerOnElapsed;
    }

    public static void TriggerDataStore()
    {
        _DataStoreTimer.Stop();
        _DataStoreTimer.Start();
    }

    private static void DataStoreTimerOnElapsed(object sender, ElapsedEventArgs eventArgs)
    {
        // Save the poll data to a file
        try
        {
            lock (_DataStoreTimer)
            {
                var json = JsonSerializer.Serialize(Polls.Values.ToList());
                File.WriteAllText(_pollStoragePath, json);
            }
            Logger.Log("POLLS > Saved poll data to file");
        }
        catch (Exception e)
        {
            Logger.Log($"POLLS > Failed to save poll data to file - {e}");
        }
    }
}
