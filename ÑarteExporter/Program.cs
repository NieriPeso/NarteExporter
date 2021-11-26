using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CliFx;
using DiscordChatExporter.Core.Discord;
using DiscordChatExporter.Core.Discord.Data;
using DiscordChatExporter.Core.Utils.Extensions;

namespace DiscordChatExporter.Cli
{
    public static class Program
    {
        private static readonly ulong NIERI_GUILD = 847456853465497601;
        private static readonly ulong NEMES_CHANNEL = 850684454640025610;
        private static readonly ulong NARTE_CHANNEL = 850377793220771850;
        private static readonly Snowflake Ñemes = new(NEMES_CHANNEL);
        private static readonly Snowflake Ñarte = new(NARTE_CHANNEL);
        private static readonly Snowflake NieriServer = new(NIERI_GUILD);

        private static readonly List<DayOfWeek> DayOfWeeks = new(new[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday,
                DayOfWeek.Saturday, DayOfWeek.Sunday,
            }
        );

        public static async Task<int> Main(string[] args)
        {
            DiscordClient discord = new(new AuthToken(AuthTokenKind.Bot, args[1]));
            var ñarteChannel = await discord.GetChannelAsync(Ñarte);

            var startDate = DateTimeOffset.Parse(args[0], styles: DateTimeStyles.RoundtripKind);
            startDate = startDate.Subtract(TimeSpan.FromDays(
                DayOfWeeks.IndexOf(startDate.DayOfWeek)
            ));
            startDate = startDate.Subtract(
                TimeSpan.FromSeconds(startDate.Hour * 60 * 60 + startDate.Minute * 60 + startDate.Second));
            var endDate = startDate.AddDays(6).AddHours(23).AddMinutes(59);
            var ñarteSubmissions = GetSubmissionsPerChannel(ñarteChannel, startDate, endDate, discord);
            var (res, weeklyÑartes) = await ProcessÑarte(ñarteSubmissions);

            var webHttp = "<html><head><meta charset=\"UTF-8\"></head><body><h1>Ñartes de la semana</h1><hr/><ul>";

            webHttp += string.Join("\n",
                weeklyÑartes.Select(CreateListItem));

            webHttp += "\n<br/>";

            webHttp += "<h1>Drops</h1><hr/>\n";

            var ñarteDrops = "token_address,receiver,amount\n" + string.Join("\n",
                res.Select(p =>
                    "0x811496d46838ccf9bba46030168cf4d7d588d04a," + p.Key + "," + p.Value.Count * 18_000));

            webHttp += "<pre>" + ñarteDrops + "</pre>";
            webHttp += "</body></html>";
            Console.WriteLine(webHttp);
            return 0;
        }

        private static async Task<(Dictionary<string, HashSet<DateOnly>> res, List<(string, string)> weeklyÑartes)>
            ProcessÑarte(IAsyncEnumerable<(Message msg, string address)> ñarteSubmissions)
        {
            var res = new Dictionary<string, HashSet<DateOnly>>();
            var weeklyÑartes = new List<(string, string)>();
            await foreach (var (msg, address) in ñarteSubmissions)
            {
                if (res.TryGetValue(address, out var rc))
                {
                    rc.Add(DateOnly.FromDateTime(msg.Timestamp.Date));
                }
                else
                {
                    res[address] = new HashSet<DateOnly>(new[] {DateOnly.FromDateTime(msg.Timestamp.Date)});
                }

                weeklyÑartes.Add((msg.Author.Name + "#" + msg.Author.Discriminator + ": " + msg.Content,
                    msg.Attachments[0].Url));
            }

            return (res, weeklyÑartes);
        }

        private static string CreateListItem((string, string) a)
        {
            var (desc, url) = a;
            return "<li>" + desc + "<br/><img src=\"" + url + "\"/></li>";
        }

        private static async IAsyncEnumerable<(Message msg, string address)> GetSubmissionsPerChannel(
            Channel ñemesChannel,
            DateTimeOffset startDate,
            DateTimeOffset endDate,
            DiscordClient discord)
        {
            await foreach (var msg in discord.GetMessagesAsync(ñemesChannel.Id,
                               Snowflake.FromDate(startDate),
                               Snowflake.FromDate(endDate)))
            {
                var match = Regex.Match(msg.Content, "(0x[A-Fa-f0-9]+)");
                var address = match.Groups.Values.FirstOrDefault((Group?) null)?.Value;

                if (match.Success && address != null && msg.Attachments.Count > 0)
                {
                    yield return (msg, address);
                }
            }
        }

        private static string FormatDictionary<TK>(Dictionary<TK, HashSet<DateOnly>> res) where TK : notnull
        {
            return FormatDictionary(res, d => d.ToShortDateString());
        }

        private static string FormatDictionary<TK, TV>(Dictionary<TK, HashSet<TV>> res, Func<TV, string> formatter)
            where TK : notnull
        {
            return string.Join("\n",
                res.Select(p =>
                        p.Key + ": [" + string.Join(",", p.Value.Select(formatter).ToList()) + "]")
                    .ToList());
        }
    }
}