using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LeaderboardScraper
{
    class SteamStats : IDisposable
    {
        [DataContract]
        public enum SortMethod
        {
            [EnumMember(Value="0")]
            Descending = 0,
            
            [EnumMember(Value="1")]
            Ascending = 1
        }

        [DataContract]
        public enum DisplayType
        {
            [EnumMember(Value="1")]
            Score = 1,
            
            [EnumMember(Value="3")]
            Time = 3
        }

        [DataContract(Name="leaderboard", Namespace="")]
        [KnownType(typeof(SortMethod))]
        [KnownType(typeof(DisplayType))]
        public class LeaderboardInfo
        {
            [DataMember(Name="url", Order = 0)]
            public string Url { get; set; }

            [DataMember(Name="lbid", Order = 1)]
            public int LeaderboardId { get; set; }

            [DataMember(Name="name", Order = 2)]
            public string Name { get; set; }

            [DataMember(Name="display_name", Order = 3)]
            public string DisplayName { get; set; }
            
            [DataMember(Name="entries", Order = 4)]
            public int Entries { get; set; }
            
            [DataMember(Name="sortmethod", Order = 5)]
            public SortMethod SortMethod { get; set; }
            
            [DataMember(Name="displaytype", Order = 6)]
            public DisplayType DisplayType { get; set; }

            private string[] _nameParts;

            public string[] NameParts => _nameParts ?? (_nameParts = Name.Split('_'));

            public bool IsInPath(IEnumerable<string> path)
            {
                var i = 0;
                foreach (var part in path) {
                    if (i >= NameParts.Length) return false;
                    if (NameParts[i] != part) return false;
                    ++i;
                }

                return true;
            }
        }

        [CollectionDataContract(Name="response", Namespace="")]
        [KnownType(typeof(LeaderboardInfo))]
        public class LeaderboardsResponse : Collection<LeaderboardInfo>
        {
            [DataMember(Name="appID", Order=0)]
            public int AppId { get; set; }

            [DataMember(Name="appFriendlyName", Order=1)]
            public string AppFriendlyName { get; set; }

            [DataMember(Name="leaderboardCount", Order=2)]
            public int LeaderboardCount { get; set; }
        }
        
        [DataContract(Name="entry", Namespace="")]
        public class EntryInfo
        {
            [DataMember(Name="steamid", Order=0)]
            public ulong SteamId { get; set; }

            [DataMember(Name="score", Order=1)]
            public int Score { get; set; }

            [DataMember(Name="rank", Order=2)]
            public int Rank { get; set; }

            [DataMember(Name="ugcid", Order=3)]
            public ulong UgcId { get; set; }
        }

        [DataContract(Name="response", Namespace="")]
        [KnownType(typeof(EntryInfo))]
        public class LeaderboardEntriesResponse
        {
            [DataMember(Name="appID", Order=0)]
            public int AppId { get; set; }

            [DataMember(Name="appFriendlyName", Order=1)]
            public string AppFriendlyName { get; set; }
            
            [DataMember(Name="leaderboardID", Order=2)]
            public int LeaderboardId { get; set; }
            
            [DataMember(Name="totalLeaderboardEntries", Order=3)]
            public int TotalLeaderboardEntries { get; set; }
            
            [DataMember(Name="entryStart", Order=4)]
            public int EntryStart { get; set; }
            
            [DataMember(Name="entryEnd", Order=5)]
            public int EntryEnd { get; set; }
            
            [DataMember(Name="nextRequestURL", Order=6)]
            public string NextRequestUrl { get; set; }
            
            [DataMember(Name="resultCount", Order=7)]
            public int ResultCount { get; set; }

            [DataMember(Name="entries", Order=8)]
            public Collection<EntryInfo> Entries { get; } = new Collection<EntryInfo>();
        }

        private readonly DataContractSerializer _leaderboardsSerializer;
        private readonly DataContractSerializer _leaderEntriesSerializer;
        private HttpClient _client;

        public SteamStats()
        {
            _client = new HttpClient();
            _leaderboardsSerializer = new DataContractSerializer(typeof(LeaderboardsResponse));
            _leaderEntriesSerializer = new DataContractSerializer(typeof(LeaderboardEntriesResponse));
        }
        
        public LeaderboardsResponse GetLeaderboards(string game)
        {
            var task = GetLeaderboardsAsync(game);
            task.Wait();
            return task.Result;
        }

        public async Task<LeaderboardsResponse> GetLeaderboardsAsync(string game)
        {
            using (var stream = await _client.GetStreamAsync($"http://steamcommunity.com/stats/{game}/leaderboards/?xml=1"))
            {
                return (LeaderboardsResponse) _leaderboardsSerializer.ReadObject(stream);
            }
        }
        
        public LeaderboardEntriesResponse GetLeaderboardEntries(string url)
        {
            var task = GetLeaderboardEntriesAsync(url);
            task.Wait();
            return task.Result;
        }

        public async Task<LeaderboardEntriesResponse> GetLeaderboardEntriesAsync(string url)
        {
            using (var stream = await _client.GetStreamAsync(url))
            {
                return (LeaderboardEntriesResponse) _leaderEntriesSerializer.ReadObject(stream);
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            _client = null;
        }
    }

    class HistogramData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("intervalSize")]
        public int IntervalSize { get; set; }

        [JsonProperty("minScore")]
        public int MinScore { get; set; }

        [JsonProperty("maxScore")]
        public int MaxScore { get; set; }

        [JsonProperty("totalEntries")]
        public int TotalLeaderboardEntries { get; set; }
        
        [JsonProperty("requestedEntries")]
        public int RequestedLeaderboardEntries { get; set; }

        [JsonProperty("nextRequestUrl")]
        public string NextRequestUrl { get; set; }

        [JsonProperty("values")]
        public int[] Values { get; private set; }

        public HistogramData() { }

        public HistogramData(SteamStats.LeaderboardInfo leaderboard)
        {
            Name = leaderboard.Name;
            IntervalSize = leaderboard.DisplayType == SteamStats.DisplayType.Score ? 1 : 50;
            MinScore = 0;
            MaxScore = leaderboard.DisplayType == SteamStats.DisplayType.Score ? 100 : 10 * 60 * 100;
            NextRequestUrl = leaderboard.Url;
            RequestedLeaderboardEntries = 0;
            TotalLeaderboardEntries = leaderboard.Entries;
            Values = new int[(MaxScore - MinScore + IntervalSize - 1) / IntervalSize];
        }
    }

    class Program
    {
        static int ReadIndex(int min, int max)
        {
            int index;

            Console.WriteLine();
            do Console.Write("Enter an index: ");
            while (!int.TryParse(Console.ReadLine(), out index) || index < min || index > max);

            return index;
        }

        static void Main(string[] args)
        {
            const string game = "Portal2";

            using (var stats = new SteamStats())
            {
                var leaderboards = stats.GetLeaderboards(game);
                Console.WriteLine($"Found {leaderboards.Count} leaderboards.");

                if (args.Contains("--fuck-my-shit-up"))
                {
                    DownloadEveryLeaderboard(stats, leaderboards);
                }
                else
                {
                    InteractiveMenu(stats, leaderboards);
                }
            }
        }

        static void DownloadEveryLeaderboard(SteamStats stats, SteamStats.LeaderboardsResponse leaderboards)
        {
            Console.WriteLine();
            Console.WriteLine($"Generating histogram for every leaderboard.");
            Console.WriteLine("Press any key to cancel...");

            var cancellationSource = new CancellationTokenSource();

            var task = DownloadLeaderboardsAsync(stats, leaderboards, cancellationSource.Token);
            
            task.ContinueWith((t, state) =>
            {
                Console.WriteLine();
                Console.WriteLine("Press any key to continue...");
            }, null, cancellationSource.Token);

            Console.ReadKey(false);

            if (!task.IsCompleted)
            {
                cancellationSource.Cancel();
            }
        }

        static async Task DownloadLeaderboardsAsync(SteamStats stats, IEnumerable<SteamStats.LeaderboardInfo> leaderboards, CancellationToken cancel)
        {
            foreach (var leaderboard in leaderboards)
            {
                Console.WriteLine();
                Console.WriteLine($"Processing {leaderboard.DisplayName}...");
                await GenerateHistogramAsync(stats, leaderboard, cancel);
            }
        }

        static void InteractiveMenu(SteamStats stats, SteamStats.LeaderboardsResponse leaderboards)
        {
            var path = new List<string>();

            SteamStats.LeaderboardInfo leaderboard;

            browsing:
            {
                Console.WriteLine();
                Console.WriteLine($"Browsing /{string.Join("/", path)}:");

                var entries = leaderboards
                    .Where(x => x.IsInPath(path) && x.NameParts.Length > path.Count)
                    .GroupBy(x => x.NameParts[path.Count])
                    .OrderBy(x => x.Count() == 1 ? 1 : 0)
                    .ToArray();

                if (path.Count > 0)
                {
                    Console.WriteLine($"  {0}:\t../");
                }

                var i = 1;
                foreach (var entry in entries)
                {
                    Console.WriteLine($"  {i++}:\t{(entry.Count() == 1 ? entry.First().DisplayName : entry.Key + "/")}");
                }

                i = ReadIndex(path.Count > 0 ? 0 : 1, entries.Length);

                if (i == 0) path.RemoveAt(path.Count - 1);
                else if(entries[i - 1].Count() == 1)
                {
                    leaderboard = entries[i - 1].First();
                    goto selected;
                }
                else path.Add(entries[i - 1].Key);

                goto browsing;
            }

            selected:
            {
                Console.WriteLine();
                Console.WriteLine($"Selected {leaderboard.DisplayName} ({leaderboard.Entries:N} entries).");
                Console.WriteLine($"  {0}:\tBack to browse");
                Console.WriteLine($"  {1}:\tGenerate histogram data");

                switch (ReadIndex(0, 1))
                {
                    case 0:
                        goto browsing;
                    case 1:
                        Console.WriteLine();
                        Console.WriteLine($"Generating histogram for {leaderboard.DisplayName}.");
                        Console.WriteLine("Press any key to cancel...");
                        Console.WriteLine();

                        var cancellationSource = new CancellationTokenSource();
                        var task = GenerateHistogramAsync(stats, leaderboard, cancellationSource.Token);

                        task.ContinueWith((t, state) =>
                        {
                            Console.WriteLine();
                            Console.WriteLine("Press any key to continue...");
                        }, null, cancellationSource.Token);

                        Console.ReadKey(false);

                        if (!task.IsCompleted)
                        {
                            cancellationSource.Cancel();
                        }

                        goto selected;
                }
            }
        }

        static string GetLeaderboardOutputPath(SteamStats.LeaderboardInfo leaderboard)
        {
            var asmDir = Path.GetDirectoryName(typeof(Program).GetTypeInfo().Assembly.Location);
            var leaderboardDir = Path.Combine(asmDir, "leaderboards");
            return Path.Combine(leaderboardDir, $"{leaderboard.Name}.json");
        }

        static async Task GenerateHistogramAsync(SteamStats stats, SteamStats.LeaderboardInfo leaderboard, CancellationToken cancel)
        {
            var destFilePath = GetLeaderboardOutputPath(leaderboard);
            var destFileDir = Path.GetDirectoryName(destFilePath);

            if (!Directory.Exists(destFileDir)) Directory.CreateDirectory(destFileDir);

            var data = File.Exists(destFilePath)
                ? JsonConvert.DeserializeObject<HistogramData>(File.ReadAllText(destFilePath))
                : new HistogramData(leaderboard);

            while (!cancel.IsCancellationRequested && data.RequestedLeaderboardEntries < data.TotalLeaderboardEntries)
            {
                SteamStats.LeaderboardEntriesResponse entries;
                
                try
                {
                    entries = await stats.GetLeaderboardEntriesAsync(data.NextRequestUrl);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    break;
                }

                if (cancel.IsCancellationRequested) return;

                Console.WriteLine($"- Fetched {entries.EntryEnd} of {entries.TotalLeaderboardEntries} entries...");

                foreach (var entry in entries.Entries)
                {
                    if (entry.Score < data.MinScore) continue;
                    if (entry.Score >= data.MaxScore)
                    {
                        Console.WriteLine("- Reached max score.");
                        data.RequestedLeaderboardEntries = data.TotalLeaderboardEntries;
                        break;
                    }

                    var index = (entry.Score - data.MinScore) / data.IntervalSize;

                    data.Values[index]++;
                }

                if (entries.EntryEnd > data.RequestedLeaderboardEntries)
                {
                    data.RequestedLeaderboardEntries = entries.EntryEnd;
                    data.TotalLeaderboardEntries = entries.TotalLeaderboardEntries;
                    data.NextRequestUrl = entries.NextRequestUrl;
                }

                File.WriteAllText(destFilePath, JsonConvert.SerializeObject(data));
            }
        }
    }
}