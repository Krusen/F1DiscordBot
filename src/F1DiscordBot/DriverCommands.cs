using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using ErgastApi.Requests;
using ErgastApi.Responses;
using ErgastApi.Responses.Models;

namespace F1DiscordBot
{
    public class DriverCommands
    {
        [Command("driver"), Aliases("drivers")]
        public async Task DriverInfo(CommandContext ctx, [RemainingText] string id)
        {
            await ctx.TriggerTypingAsync();

            var driver = await FindDriverAsync(id);

            if (driver == null)
            {
                await ctx.RespondAsync($"No driver found matching **{id}**");
                return;
            }

            var qualiResults = (await GetQualifyingResultsAsync(driver)).Races.SelectMany(x => x.QualifyingResults).ToList();
            var poles = qualiResults.Count(x => x.Position == 1);
            var bestQuali = qualiResults.OrderBy(x => x.Position).FirstOrDefault()?.Position;
            var avgQuali = qualiResults.Any() ? qualiResults.Average(x => x.Position) : (double?)null;

            var grouped = qualiResults.GroupBy(x => x.Position).OrderByDescending(x => x.Count()).ThenBy(x => x.Key).FirstOrDefault();
            var mostFrequentQuali = grouped?.Key;
            var mostFrequentQualiCount = grouped?.Count();


            var raceResults = (await GetRaceResultsAsync(driver)).Races.SelectMany(x => x.Results).ToList();
            var constructors = raceResults.Select(x => x.Constructor.Name).Distinct().ToList();
            var totalRaces = raceResults.Count;
            var wins = raceResults.Count(x => x.Position == 1);
            var podiums = raceResults.Count(x => x.Position <= 3);
            var bestFinish = raceResults.OrderBy(x => x.Position).FirstOrDefault()?.Position;
            var avgFinishPosition = raceResults.Average(x => x.Position);
            var totalPoints = raceResults.Sum(x => x.Points);

            var groupedRaces = raceResults.GroupBy(x => x.Position).OrderByDescending(x => x.Count()).ThenBy(x => x.Key).FirstOrDefault();
            var mostFrequentFinish = groupedRaces?.Key;
            var mostFrequentFinishCount = groupedRaces?.Count();

            var embed = GetEmbed(driver, constructors, totalRaces, wins, podiums, bestFinish, avgFinishPosition, totalPoints, poles,
                bestQuali, avgQuali, mostFrequentFinish, mostFrequentFinishCount, mostFrequentQuali,
                mostFrequentQualiCount);

            await ctx.RespondAsync(embed: embed);
        }

        // TODO: Use object instead of all these parameters
        private static DiscordEmbed GetEmbed(Driver driver, IList<string> constructors, int totalRaces, int wins, int podiums,
            int? bestFinish, double avgFinishPosition, double totalPoints, int poles, int? bestQuali, double? avgQuali,
            int? mostFrequestFinish, int? mostFrequestFinishCount,
            int? mostFrequentQuali, int? mostFrequentQualiCount)
        {
            var embed = new DiscordEmbedBuilder {Color = DiscordColor.Gold};


            embed.AddField("Driver", GetDriverFieldValue(driver));
            if (driver.DateOfBirth != null)
            {
                var age = GetAge(driver.DateOfBirth.Value);
                embed.AddField("Date of Birth", $"{driver.DateOfBirth:yyyy-MM-dd} (Age {age}){(age < 20 ? " :baby:" : age > 35 ? " :older_man:" : "")}");
            }

            embed.AddField("Teams", string.Join("\n", constructors));

            embed.AddField("Races", totalRaces.ToString());
            embed.AddField("Total Points", totalPoints.ToString(CultureInfo.InvariantCulture));
            embed.AddField("Wins :first_place:", wins, true);
            embed.AddField("Podiums :third_place:", podiums, true);
            embed.AddField("Poles :trophy:", poles.ToString(), true);

            embed.AddField("Best Finish", bestFinish?.WithSuffix() ?? "-");
            embed.AddField("Avg. Finish", avgFinishPosition.WithSuffix(), true);
            if (mostFrequestFinish != null)
            {
                embed.AddField(
                    "Most Frequent Finish",
                    $"{mostFrequestFinish?.WithSuffix()} ({mostFrequestFinishCount} {(mostFrequestFinishCount == 1 ? "time" : "times")} - {Percentage(mostFrequestFinishCount.Value, totalRaces)}%)",
                    true);
            }

            if (bestQuali != null)
                embed.AddField("Best Qualifying", bestQuali?.WithSuffix());
            if (avgQuali != null)
                embed.AddField("Avg. Qualifying", avgQuali?.WithSuffix(), true);
            if (mostFrequentQuali != null)
            {
                embed.AddField(
                    "Most Frequent Qualifying",
                    $"{mostFrequentQuali?.WithSuffix()} ({mostFrequentQualiCount} {(mostFrequentQualiCount == 1 ? "time" : "times")} - {Percentage(mostFrequentQualiCount.Value, totalRaces)}%)",
                    true);
            }

            embed.AddField("Wikipedia", driver.WikiUrl);

            return embed.Build();
        }

        private static int Percentage(double count, double total) => (int) Math.Round(count * 100 / total, MidpointRounding.AwayFromZero);

        private static string GetDriverFieldValue(Driver driver)
        {
            var output = $"{Flags.GetFlag(driver.Nationality)}";

            output += $" {driver.FullName}";

            if (driver.Code != null)
                output += $" ({driver.Code})";

            if (driver.PermanentNumber != null)
                output += $" #{driver.PermanentNumber}";

            return output;
        }

        private static int GetAge(DateTime dateOfBirth)
        {
            var utcNow = DateTime.UtcNow;
            var age = utcNow.Year - dateOfBirth.Year;

            var birthDayCurrentYear = new DateTime(utcNow.Year, dateOfBirth.Month, dateOfBirth.Day);

            if (utcNow < birthDayCurrentYear)
                age--;

            return age;
        }

        private static Task<QualifyingResultsResponse> GetQualifyingResultsAsync(Driver driver)
        {
            var request = new QualifyingResultsRequest {DriverId = driver.DriverId, Limit = 1000};
            return Program.ErgastClient.GetResponseAsync(request);
        }

        private static Task<RaceResultsResponse> GetRaceResultsAsync(Driver driver)
        {
            var request = new RaceResultsRequest {DriverId = driver.DriverId, Limit = 1000};
            return Program.ErgastClient.GetResponseAsync(request);
        }

        private static async Task<Driver> FindDriverAsync(string id)
        {
            var ergastClient = Program.ErgastClient;

            var allDriversRequest = new DriverInfoRequest { Limit = 1000 };
            var allDriversResponse = await ergastClient.GetResponseAsync(allDriversRequest);

            var drivers = allDriversResponse.Drivers.OrderByDescending(x => x.DateOfBirth).ToList();

            var ignoreCase = StringComparison.OrdinalIgnoreCase;

            Driver driver = null;

            if (id.Length == 1)
            {
                driver = drivers.FirstOrDefault(x => x.PermanentNumber.ToString() == id);
            }

            if (driver == null && id.Length == 2)
            {
                driver = drivers.FirstOrDefault(x => x.PermanentNumber.ToString() == id) ??
                         drivers.FirstOrDefault(x => string.Equals(x.DriverId, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => string.Equals(x.Code, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => string.Equals(x.LastName, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => string.Equals(x.FirstName, id, ignoreCase));
            }

            if (driver == null && id.Length == 3)
            {
                driver = drivers.FirstOrDefault(x => string.Equals(x.Code, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => string.Equals(x.DriverId, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => string.Equals(x.LastName, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => string.Equals(x.FirstName, id, ignoreCase));
            }

            if (driver == null)
            {
                driver = drivers.FirstOrDefault(x => string.Equals(x.DriverId, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => string.Equals(x.Code, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => string.Equals(x.LastName, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => string.Equals(x.FullName, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => string.Equals(x.FirstName, id, ignoreCase)) ??
                         drivers.FirstOrDefault(x => x.PermanentNumber.ToString() == id);
            }

            return driver;
        }
    }
}
