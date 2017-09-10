using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using ErgastApi.Requests;
using ErgastApi.Responses.Models.RaceInfo;

namespace F1DiscordBot
{
    public class RaceResultsCommands
    {
        [Command("results")]
        [Description("Get race results for a specific round.\n" +
                     "You can use `last` (or just omit it) to get the latest results.\n" +
                     "Get results from another another season by specifying it before the round, e.g. first round of 2009: `2009 1`")]
        public async Task RaceResults(CommandContext ctx, [Description("The round number.")]params string[] args)
        {
            await ctx.TriggerTypingAsync();

            (var season, var round) = GetSeasonAndRound(args);

            var resultsRequest = new RaceResultsRequest {Season = season, Round = round};
            var resultsResponse = await Program.ErgastClient.GetResponseAsync(resultsRequest);

            var raceResult = resultsResponse.Races.FirstOrDefault();
            if (raceResult == null)
            {
                await ctx.RespondAsync("Could not find any results for the specified round.");
                return;
            }

            var pitstopsRequest = new PitStopsRequest {Season = season, Round = round};
            var pitstopsResponse = await Program.ErgastClient.GetResponseAsync(pitstopsRequest);
            var racePitstops = pitstopsResponse.Races.FirstOrDefault();

            var embed = GetRaceResultsEmbed(raceResult, racePitstops);

            await ctx.RespondWithSpoilerAsync(embed, "Results sent in DM to avoid spoilers");
        }

        private static (string season, string round) GetSeasonAndRound(string[] args)
        {
            var season = Seasons.Current;
            var round = Rounds.Last;

            switch (args.Length)
            {
                case 0:
                    break;
                case 1:
                    round = args[0];
                    break;
                default:
                    season = args[0];
                    round = args[1];
                    break;
            }

            return (season, round);
        }

        private DiscordEmbed GetRaceResultsEmbed(RaceWithResults race, RaceWithPitStops racePitstops)
        {
            var embed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Aquamarine
            };

            var winner = race.Results.First();
            var fastest = race.Results.OrderBy(x => x.FastestLap?.LapTime ?? TimeSpan.MaxValue).FirstOrDefault();

            embed.AddField("Season", $"{race.Season}", inline: true);
            embed.AddField("Round", $"{race.Round}", inline: true);
            embed.AddField("Date", $"{race.StartTime:yyyy-MM-dd}", inline: true);
            embed.AddField("Race", $"[{race.RaceName}]({race.WikiUrl})", inline: true);
            embed.AddField("Circuit", $"[{race.Circuit.CircuitName}]({race.Circuit.WikiUrl})", inline: true);
            embed.AddField("Location", $"{race.Circuit.Location.Locality}, {race.Circuit.Location.Country}");

            embed.AddField("Total Race Time", $"{winner.TotalRaceTime.Value:h'h 'mm'm 'ss's'}", inline: true);
            embed.AddField("Laps", $"{winner.Laps}", inline: true);

            if (fastest != null)
                embed.AddField("Fastest Lap", $"{GetFlag(fastest.Driver.Nationality)}  {fastest.Driver.FullName} - {fastest.FastestLap.LapTime:m':'ss':'fff} on lap {fastest.FastestLap.LapNumber}");

            embed.AddField("Top 10", GetResultsTable(race.Results, racePitstops?.PitStops, fastest, 0));
            embed.AddField("11-20", GetResultsTable(race.Results, racePitstops?.PitStops, fastest, 10));

            if (race.Results.Count > 20)
                embed.AddField($"21-{race.Results.Count}", GetResultsTable(race.Results, racePitstops?.PitStops, fastest, 20));

            //embed.AddField("Results 1-10", GetResultsWithFlags(race.Results, 0), inline: true);
            //embed.AddField("Results 11-20", GetResultsWithFlags(race.Results, 10), inline: true);

            return embed.Build();
        }

        private static string GetResultsTable(IList<RaceResult> results, IList<PitStopInfo> pitstops, RaceResult fastest, int skip)
        {
            var totalLaps = results.First().Laps;

            var sb = new StringBuilder();

            sb.Append("```");

            if (skip == 0)
            {
                sb.AppendLine("POS      DRIVER               GAP PIT     FASTEST");
                sb.AppendLine("-------------------------------------------------"); // Max 55 wide
            }

            foreach (var result in results.Skip(skip).Take(10))
            {
                var gain = result.Grid - result.Position;
                var gap = result.GapToWinner?.TotalSeconds.ToString("+0.000", CultureInfo.InvariantCulture) ??
                          (totalLaps - result.Laps).ToString("# L;#;#");
                var pits = pitstops?.Count(x => x.DriverId == result.Driver.DriverId).ToString("#;#;-") ?? "NA";
                var status = result.Disqualified ? "DSQ" : result.Retired ? "DNF" : "";
                var isFastest = result == fastest ? "*" : "";


                sb.AppendLine($"{result.PositionText,2} {gain,-5:(+0);(-0);#} {result.Driver.ShortName(),-15} {gap,8} {pits,2} {status,-3}{isFastest,1}{result.FastestLap?.LapTime,8:m':'ss'.'fff}");
            }

            sb.Append("```");

            return sb.ToString();

            // POS      DRIVER              GAP PIT     FASTEST
            // -------------------------------------------------------|
            //  1 (+3)  L. Hamilton              1     2:22.440
            //  2 (-1)  G. Fisichella  +  4.234  1    *2:21.756
            //  3 (+12) S. Vandoorne   + 23.534  2
            //  4 (+1)  A. Giovinazzi  + 31.015  1
            //  5       F. Alonso           1 L  1
            //  6       S. Vettel           2 L  0
            // 11       S. Vettel          10 L  0 DNF 2:22.440

            // POS     DRIVER                GAP   PIT     FASTEST
            // -------------------------------------------------------|
            //  1 (+3) Lewis Hamilton
            //  2 (-1) Sebastian Vettel   +  4.234  1     2:22.440
            //  3 (+12)Stoffel Vandoorne  + 23.534  2
            //  4 (+1) Antonio Giovinazzi + 31.015  1
            //  5      Fernando Alonso         1 L  1
            //  6      Sebastian Vettel        2 L  0
            // 11      Sebastian Vettel       10 L  0 DNF 2:22.440
        }


        private static string GetResultsWithFlags(IList<RaceResult> results, int skip)
        {
            var sb = new StringBuilder();

            foreach (var result in results.Skip(skip).Take(10))
            {
                sb.AppendLine($"`{result.Position:00}`  {GetFlag(result.Driver.Nationality)}  {result.Driver.FullName}");
            }

            return sb.ToString();
        }

        private static string GetFlag(string nationality)
        {
            string code;
            switch (nationality.ToLower())
            {
                case "american":   code = "us"; break;
                case "australian": code = "au"; break;
                case "austrian":   code = "au"; break;
                case "belgian":    code = "be"; break;
                case "brazilian":  code = "br"; break;
                case "british":    code = "gb"; break;
                case "candaian":   code = "ca"; break;
                case "danish":     code = "dk"; break;
                case "dutch":      code = "nl"; break;
                case "finnish":    code = "fi"; break;
                case "french":     code = "fr"; break;
                case "german":     code = "de"; break;
                case "indian":     code = "in"; break;
                case "italian":    code = "it"; break;
                case "mexican":    code = "mx"; break;
                case "russian":    code = "ru"; break;
                case "spanish":    code = "es"; break;
                case "swedish":    code = "se"; break;
                case "swiss":      code = "ch"; break;
                default:
                    return ":gay_pride_flag:";
            }

            return $":flag_{code}:";
        }
    }
}
