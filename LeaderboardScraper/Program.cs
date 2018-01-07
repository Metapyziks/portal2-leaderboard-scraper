using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Threading.Tasks;

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
            [DataMember(Name="appID", Order = 0)]
            public int AppId { get; set; }

            [DataMember(Name="appFriendlyName", Order = 1)]
            public string AppFriendlyName { get; set; }

            [DataMember(Name="leaderboardCount", Order = 2)]
            public int LeaderboardCount { get; set; }
        }

        private readonly DataContractSerializer _leaderboardsSerializer;
        private HttpClient _client;

        public SteamStats()
        {
            _client = new HttpClient();
            _leaderboardsSerializer = new DataContractSerializer(typeof(LeaderboardsResponse));
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

        public void Dispose()
        {
            _client?.Dispose();
            _client = null;
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

            SteamStats.LeaderboardsResponse leaderboards;

            using (var stats = new SteamStats())
            {
                leaderboards = stats.GetLeaderboards(game);
                Console.WriteLine($"Found {leaderboards.Count} leaderboards.");
            }

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
                Console.WriteLine($"  {1}:\tGenerate histogram");

                switch (ReadIndex(0, 1))
                {
                    case 0:
                        goto browsing;
                    case 1:
                        GenerateHistogram(leaderboard);
                        goto selected;
                }
            }
        }

        static void GenerateHistogram(SteamStats.LeaderboardInfo leaderboard)
        {
            Console.WriteLine();
            Console.WriteLine($"Generating histogram for {leaderboard.DisplayName}.");

            Console.ReadKey(true);
        }
    }
}