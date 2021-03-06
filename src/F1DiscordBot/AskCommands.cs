﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using ErgastApi.Requests;
using ErgastApi.Responses.Models;
using ErgastApi.Responses.Models.RaceInfo;
using Newtonsoft.Json;

namespace F1DiscordBot
{
    public class AskCommands
    {
        private readonly string LuisEndpoint = ConfigurationManager.AppSettings["LuisEndpoint_F1Bot"] ??
                                               throw new Exception($"Missing app setting 'LuisEndpoint_F1Bot'");

        [Command("bot")]
        public async Task Ask(CommandContext ctx, [RemainingText] string query)
        {
            await ctx.TriggerTypingAsync();

            var help =
                "You can ask me about who won a specific race, who finished at a specific position at a specific race, who/which team won a specific season.\n\n" +
                "Examples:\n" +
                "\t`+bot who finished 3rd at monaco?`\n" +
                "\t`+bot where did kimi finish at monza in 2015?`\n" +
                "\t`+bot where did ric finish?` (defaults to last race)\n" +
                "\t`+bot who won in 2009?`\n" +
                "\t`+bot which team won in 2007?`";

            if (string.IsNullOrEmpty(query))
            {
                await ctx.RespondAsync(help);
                return;
            }

            var webClient = new WebClient();

            var json = await webClient.DownloadStringTaskAsync(LuisEndpoint + HttpUtility.UrlEncode(query));

            var response = JsonConvert.DeserializeObject<LuisResponse>(json);

            if (response.TopScoringIntent.Score > 0.30)
            {
                switch (response.TopScoringIntent.Intent)
                {
                    case IntentType.RacePosition:
                        await HandleRacePositionAsync(ctx, response);
                        return;
                    case IntentType.DriverRacePosition:
                        await HandleDriverRacePositionAsync(ctx, response);
                        return;
                    case IntentType.ChampionshipPosition:
                        await HandleChampionshipPositionAsync(ctx, response);
                        return;
                    case IntentType.FastestLap:
                        await HandleFastestLapAsync(ctx, response);
                        return;
                    //case IntentType.DriverFastestLap:
                    //    await HandleDriverFastestLapAsync(ctx, response);
                    //    return;
                }
            }

            await ctx.RespondAsync("I don't know how to answer that.\n\n" + help);
        }

        public static async Task HandleChampionshipPositionAsync(CommandContext ctx, LuisResponse response)
        {
            var championshipType = response.GetEntity(EntityType.Championship)?.Resolution.Values.FirstOrDefault();
            if (championshipType == "wcc")
            {
                await HandleChampionshipPositionConstructorAsync(ctx, response);
            }
            else
            {
                await HandleChampionshipPositionDriverAsync(ctx, response);
            }
        }

        private static async Task HandleChampionshipPositionDriverAsync(CommandContext ctx, LuisResponse response)
        {
            var driverStandingsRequest = new DriverStandingsRequest
            {
                Season = Seasons.Current,
                Limit = 1000
            };

            // Season
            var season = response.GetEntity(EntityType.Season)?.Value;
            if (season != null)
            {
                driverStandingsRequest.Season = season;
            }
            else
            {
                var raceListRequest = new RaceListRequest { Season = driverStandingsRequest.Season };
                var raceList = await Program.ErgastClient.GetResponseAsync(raceListRequest);
                if (raceList.Races.LastOrDefault()?.StartTime > DateTime.UtcNow)
                {
                    driverStandingsRequest.Season = (raceList.Races.First().Season - 1).ToString();
                }
            }

            var driverStandingsResponse = await Program.ErgastClient.GetResponseAsync(driverStandingsRequest);
            if (!driverStandingsResponse.StandingsLists.Any())
            {
                await ctx.RespondAsync($"Unable to find a race for **{driverStandingsRequest.Season}** season, round **{driverStandingsRequest.Round}**");
                return;
            }

            string positionText = null;
            var position = 1;
            var positionEntity = response.GetEntity(EntityType.Position);
            if (positionEntity != null)
            {
                positionText = positionEntity.Value;
                if (int.TryParse(positionEntity.Resolution.Values.First(), out var parsedPosition))
                    position = parsedPosition;
            }

            var driverStandings = driverStandingsResponse.StandingsLists.First();
            var standing = positionText == "last"
                ? driverStandings.Standings.LastOrDefault()
                : driverStandings.Standings.FirstOrDefault(x => x.Position == position);

            await ctx.RespondWithSpoilerAsync(
                $"**{standing.Driver.FullName} ({standing.Constructor.Name})** {(position == 1 ? "won" : $"finished **{position.WithSuffix()}** in")} the WDC in **{driverStandings.Season}**" +
                $" with **{standing.Points} points** and **{standing.Wins} race wins**");
            //$" ahead of **{runnerUp.Driver.FullName} ({runnerUp.Constructor.Name})** by {winner.Points - runnerUp.Points} points");
        }

        private static async Task HandleChampionshipPositionConstructorAsync(CommandContext ctx, LuisResponse response)
        {
            var constructorStandingsRequest = new ConstructorStandingsRequest
            {
                Season = Seasons.Current,
                Limit = 1000
            };

            // Season
            var season = response.GetEntity(EntityType.Season)?.Value;
            if (season != null)
            {
                constructorStandingsRequest.Season = season;
            }
            else
            {
                var raceListRequest = new RaceListRequest { Season = constructorStandingsRequest.Season };
                var raceList = await Program.ErgastClient.GetResponseAsync(raceListRequest);
                if (raceList.Races.LastOrDefault()?.StartTime > DateTime.UtcNow)
                {
                    constructorStandingsRequest.Season = (raceList.Races.First().Season - 1).ToString();
                }
            }

            var constructorStandingsResponse = await Program.ErgastClient.GetResponseAsync(constructorStandingsRequest);
            if (!constructorStandingsResponse.StandingsLists.Any())
            {
                await ctx.RespondAsync($"Unable to find a race for **{constructorStandingsRequest.Season}** season, round **{constructorStandingsRequest.Round}**");
                return;
            }

            string positionText = null;
            var position = 1;
            var positionEntity = response.GetEntity(EntityType.Position);
            if (positionEntity != null)
            {
                positionText = positionEntity.Value;
                if (int.TryParse(positionEntity.Resolution.Values.First(), out var parsedPosition))
                    position = parsedPosition;
            }

            var constructorStandings = constructorStandingsResponse.StandingsLists.First();
            var standing = positionText == "last"
                ? constructorStandings.Standings.LastOrDefault()
                : constructorStandings.Standings.FirstOrDefault(x => x.Position == position);

            await ctx.RespondWithSpoilerAsync(
                $"**{standing.Constructor.Name}** {(position == 1 ? "won" : $"finished **{position.WithSuffix()}** in")} the WCC in **{constructorStandings.Season}**" +
                $" with **{standing.Points} points** and **{standing.Wins} race wins**");
        }

        private static async Task HandleDriverRacePositionAsync(CommandContext ctx, LuisResponse response)
        {
            var resultsRequest = new RaceResultsRequest
            {
                Season = Seasons.Current,
                Limit = 1000
            };

            // Season
            var season = response.GetEntity(EntityType.Season)?.Value;
            if (season != null)
                resultsRequest.Season = season;

            // Round
            var round = response.GetEntity(EntityType.Round)?.Value ??
                        response.GetEntity(EntityType.RelativeRound)?.Resolution.Values.First();

            if (round != null)
                resultsRequest.Round = round;

            var resultsResponse = await Program.ErgastClient.GetResponseAsync(resultsRequest);
            if (!resultsResponse.Races.Any())
            {
                await ctx.RespondAsync($"Unable to find a race for **{resultsRequest.Season}** season, round **{resultsRequest.Round}**");
                return;
            }

            var driverValue = response.GetEntity(EntityType.Driver)?.Value;
            if (driverValue == null)
            {
                await ctx.RespondAsync("Unable to understand which driver you mean");
                return;
            }

            Circuit circuit = null;
            var circuitEntity = response.GetEntity(EntityType.Circuit);
            if (circuitEntity != null)
            {
                var ignoreCase = StringComparison.OrdinalIgnoreCase;
                var circuitValue = circuitEntity.Value;
                var races = resultsResponse.Races;
                circuit = races.FirstOrDefault(x => Regex.IsMatch(x.RaceName, $".*{circuitValue}.*", RegexOptions.IgnoreCase))?.Circuit ??
                          races.FirstOrDefault(x => string.Equals(x.Circuit.CircuitId, circuitValue, ignoreCase))?.Circuit ??
                          races.FirstOrDefault(x => string.Equals(x.Circuit.CircuitName, circuitValue, ignoreCase))?.Circuit ??
                          races.FirstOrDefault(x => string.Equals(x.Circuit.Location.Country, circuitValue, ignoreCase))?.Circuit ??
                          races.FirstOrDefault(x => string.Equals(x.Circuit.Location.Locality, circuitValue, ignoreCase))?.Circuit ??
                          races.FirstOrDefault(x => Regex.IsMatch(x.Circuit.CircuitName, $".*{circuitValue}.*", RegexOptions.IgnoreCase))?.Circuit ??
                          await FindCircuitAsync(circuitEntity.Value, resultsRequest.Season);

                if (circuit == null)
                {
                    await ctx.RespondAsync($"I couldn't find a race in **{(resultsRequest.Season == Seasons.Current ? DateTime.Now.Year.ToString() : resultsRequest.Season)}** matching **{circuitEntity.Value}**");
                    return;
                }
            }

            var race = circuit == null
                ? resultsResponse.Races.Last()
                : resultsResponse.Races.FirstOrDefault(x => x.Circuit.CircuitId == circuit.CircuitId);

            if (race == null)
            {
                // TODO: When circuit is null
                await ctx.RespondAsync($"I'm sorry, I couldn't find a race at **{circuit?.CircuitName}** in **{resultsRequest.Season}**");
                return;
            }

            //var race = resultsResponse.Races.First();
            var results = race.Results;

            var driverResult = GetDriverResult(driverValue, results, x => x.Driver);
            if (driverResult == null)
            {
                await ctx.RespondAsync($"Unable to find a driver matching **{driverValue}** for **{resultsRequest.Season}** season, round **{resultsRequest.Round}**");
                return;
            }

            await ctx.RespondWithSpoilerAsync($"**{driverResult.Driver.FullName} ({driverResult.Constructor.Name})** finished **{driverResult.Position.WithSuffix()}** ({driverResult.Points} points) at the **{race.RaceName}** in **{race.Season}**");
        }

        private static async Task HandleRacePositionAsync(CommandContext ctx, LuisResponse response)
        {
            var positionText = "1st";
            var position = 1;
            Circuit circuit = null;
            var resultsRequest = new RaceResultsRequest
            {
                Season = Seasons.Current,
                Limit = 1000
            };

            foreach (var entity in response.Entities)
            {
                if (entity.Type == EntityType.Season)
                    resultsRequest.Season = entity.Value;

                if (entity.Type == EntityType.Round)
                    resultsRequest.Round = entity.Value;

                if (entity.Type == EntityType.RelativeRound)
                    resultsRequest.Round = entity.Resolution.Values.First();

                if (entity.Type == EntityType.Position)
                {
                    positionText = entity.Value;
                    if (int.TryParse(entity.Resolution.Values.First(), out var parsedPosition))
                        position = parsedPosition;
                }
            }

            if (response.Entities.All(x => x.Type != EntityType.Position))
            {
                resultsRequest.Round = resultsRequest.Round ??
                                       response.GetEntity(EntityType.RelativeRound)?.Resolution.Values.First();
            }

            var resultsResponse = await Program.ErgastClient.GetResponseAsync(resultsRequest);
            if (!resultsResponse.Races.Any())
            {
                await ctx.RespondAsync("I could not find any results matching your question");
                return;
            }

            var circuitEntity = response.GetEntity(EntityType.Circuit);
            if (circuitEntity != null)
            {
                var ignoreCase = StringComparison.OrdinalIgnoreCase;
                var circuitValue = circuitEntity.Value;
                var races = resultsResponse.Races;
                circuit = races.FirstOrDefault(x => Regex.IsMatch(x.RaceName, $".*{circuitValue}.*", RegexOptions.IgnoreCase))?.Circuit ??
                          races.FirstOrDefault(x => string.Equals(x.Circuit.CircuitId, circuitValue, ignoreCase))?.Circuit ??
                          races.FirstOrDefault(x => string.Equals(x.Circuit.CircuitName, circuitValue, ignoreCase))?.Circuit ??
                          races.FirstOrDefault(x => string.Equals(x.Circuit.Location.Country, circuitValue, ignoreCase))?.Circuit ??
                          races.FirstOrDefault(x => string.Equals(x.Circuit.Location.Locality, circuitValue, ignoreCase))?.Circuit ??
                          races.FirstOrDefault(x => Regex.IsMatch(x.Circuit.CircuitName, $".*{circuitValue}.*", RegexOptions.IgnoreCase))?.Circuit ??
                          await FindCircuitAsync(circuitEntity.Value, resultsRequest.Season);

                if (circuit == null)
                {
                    await ctx.RespondAsync($"I couldn't find a race in **{(resultsRequest.Season == Seasons.Current ? DateTime.Now.Year.ToString() : resultsRequest.Season)}** matching **{circuitEntity.Value}**");
                    return;
                }
            }
            else
            {
                await ctx.RespondAsync($"I wasn't able to determine the circuit from your question");
                return;
            }

            var race = resultsResponse.Races.FirstOrDefault(x => x.Circuit.CircuitId == circuit.CircuitId);

            if (race == null)
            {
                await ctx.RespondAsync($"I'm sorry, I couldn't find a race at **{circuit.CircuitName}** in **{resultsRequest.Season}**");
                return;
            }

            var driver = positionText == "last"
                ? race.Results.LastOrDefault()
                : race.Results.FirstOrDefault(x => x.Position == position);
            if (driver == null)
            {
                await ctx.RespondAsync($"Could not find anyone finishing in position {position} at the {race.RaceName} in {race.Season}");
                return;
            }

            await ctx.RespondWithSpoilerAsync($"**{driver.Driver.FullName} ({driver.Constructor.Name})** finished **{position.WithSuffix()}** ({driver.Points} points) at the **{race.RaceName}** in **{race.Season}**");
        }

        private static async Task HandleFastestLapAsync(CommandContext ctx, LuisResponse response)
        {
            var resultsRequest = new RaceResultsRequest
            {
                FastestLapRank = 1,
                Limit = 1000
            };

            foreach (var entity in response.Entities)
            {
                if (entity.Type == EntityType.Season)
                    resultsRequest.Season = entity.Value;

                if (entity.Type == EntityType.Round)
                    resultsRequest.Round = entity.Value;

                if (entity.Type == EntityType.RelativeRound)
                    resultsRequest.Round = entity.Resolution.Values.First();
            }

            var resultsResponse = await Program.ErgastClient.GetResponseAsync(resultsRequest);
            if (!resultsResponse.Races.Any())
            {
                await ctx.RespondAsync("I could not find any results matching your question");
                return;
            }

            var circuitRaceResults = new List<RaceWithResults>();
            var circuitEntity = response.GetEntity(EntityType.Circuit);
            if (circuitEntity != null)
            {
                var ignoreCase = StringComparison.OrdinalIgnoreCase;
                var circuitValue = circuitEntity.Value;
                var races = resultsResponse.Races;
                circuitRaceResults = races.Where(x => Regex.IsMatch(x.RaceName, $".*{circuitValue}.*", RegexOptions.IgnoreCase) ||
                                            string.Equals(x.Circuit.CircuitId, circuitValue, ignoreCase) ||
                                            string.Equals(x.Circuit.CircuitName, circuitValue, ignoreCase) ||
                                            string.Equals(x.Circuit.Location.Country, circuitValue, ignoreCase) ||
                                            string.Equals(x.Circuit.Location.Locality, circuitValue, ignoreCase) ||
                                            Regex.IsMatch(x.Circuit.CircuitName, $".*{circuitValue}.*",
                                                RegexOptions.IgnoreCase))
                    //.Select(x => x.Results.FirstOrDefault())
                    .ToList();

                if (!circuitRaceResults.Any())
                {
                    await ctx.RespondAsync($"I couldn't find any races in **{(resultsRequest.Season == Seasons.Current ? DateTime.Now.Year.ToString() : resultsRequest.Season)}** matching **{circuitEntity.Value}**");
                    return;
                }
            }
            else
            {
                await ctx.RespondAsync($"I wasn't able to determine the circuit from your question");
                return;
            }

            var topResults = circuitRaceResults.OrderBy(x => x.Results.FirstOrDefault()?.FastestLap.LapTime).Take(5).ToList();

            if (topResults.Count == 1)
            {
                var result = topResults.First();
                var fastest = result.Results.First();
                await ctx.RespondAsync($"Fastest lap at **{result.RaceName}** in **{result.Season}** was **{fastest.FastestLap.LapTime:m':'ss'.'fff}** (lap {fastest.FastestLap.LapNumber}) by **{fastest.Driver.FullName}** for **{fastest.Constructor.Name}**");
                return;
            }

            var sb = new StringBuilder();
            sb.Append("```");
            sb.AppendLine("TIME      YEAR  DRIVER               CONSTRUCTOR");
            sb.AppendLine("------------------------------------------------"); // Max 55 wide

            foreach (var result in topResults)
            {
                var fastest = result.Results.First();
                sb.AppendLine($"{fastest.FastestLap.LapTime:m':'ss'.'fff}  {result.Season}  {fastest.Driver.FullName,-20} {fastest.Constructor.Name}");
            }

            sb.Append("```");

            await ctx.RespondAsync(sb.ToString());
        }

        private static async Task<Circuit> FindCircuitAsync(string id, string season)
        {
            var ergastClient = Program.ErgastClient;

            var allCircuitsRequest = new CircuitInfoRequest {Season = season, Limit = 1000};
            var allCircuitsResponse = await ergastClient.GetResponseAsync(allCircuitsRequest);

            var circuits = allCircuitsResponse.Circuits;

            var ignoreCase = StringComparison.OrdinalIgnoreCase;

            return circuits.FirstOrDefault(x => string.Equals(x.CircuitId, id, ignoreCase)) ??
                   circuits.FirstOrDefault(x => string.Equals(x.CircuitName, id, ignoreCase)) ??
                   circuits.FirstOrDefault(x => string.Equals(x.Location.Country, id, ignoreCase)) ??
                   circuits.FirstOrDefault(x => string.Equals(x.Location.Locality, id, ignoreCase)) ??
                   circuits.FirstOrDefault(x => Regex.IsMatch(x.CircuitName, $".*{id}.*", RegexOptions.IgnoreCase));
        }

        public static T GetDriverResult<T>(string id, IList<T> list, Func<T, Driver> driverFn) where T : class
        {
            var ignoreCase = StringComparison.OrdinalIgnoreCase;

            T driver = default;

            if (id.Length == 1)
            {
                driver = list.FirstOrDefault(x => driverFn(x).PermanentNumber.ToString() == id);
            }

            if (driver == null && id.Length == 2)
            {
                driver = list.FirstOrDefault(x => driverFn(x).PermanentNumber.ToString() == id) ??
                         list.FirstOrDefault(x => string.Equals(driverFn(x).DriverId, id, ignoreCase)) ??
                         list.FirstOrDefault(x => string.Equals(driverFn(x).LastName, id, ignoreCase)) ??
                         list.FirstOrDefault(x => string.Equals(driverFn(x).FirstName, id, ignoreCase));
            }

            if (driver == null && id.Length == 3)
            {
                driver = list.FirstOrDefault(x => string.Equals(driverFn(x).Code, id, ignoreCase)) ??
                         list.FirstOrDefault(x => string.Equals(driverFn(x).DriverId, id, ignoreCase)) ??
                         list.FirstOrDefault(x => string.Equals(driverFn(x).LastName, id, ignoreCase)) ??
                         list.FirstOrDefault(x => string.Equals(driverFn(x).FirstName, id, ignoreCase));
            }

            if (driver == null)
            {
                driver = list.FirstOrDefault(x => string.Equals(driverFn(x).LastName, id, ignoreCase)) ??
                         list.FirstOrDefault(x => string.Equals(driverFn(x).FirstName, id, ignoreCase)) ??
                         list.FirstOrDefault(x => string.Equals(driverFn(x).DriverId, id, ignoreCase)) ??
                         list.FirstOrDefault(x => string.Equals(driverFn(x).FullName, id, ignoreCase)) ??
                         list.FirstOrDefault(x => driverFn(x).PermanentNumber.ToString() == id) ??
                         list.FirstOrDefault(x => driverFn(x).LastName.StartsWith(id, ignoreCase)) ??
                         list.FirstOrDefault(x => driverFn(x).FirstName.StartsWith(id, ignoreCase));
            }

            return driver;
        }
    }

    public class LuisResponse
    {
        public string Query { get; set; }

        public LuisIntent TopScoringIntent { get; set; }

        public IList<LuisEntity> Entities { get; set; }

        public LuisEntity GetEntity(EntityType type) => Entities.FirstOrDefault(x => x.Type == type);
    }

    public class LuisIntent
    {
        public IntentType Intent { get; set; }

        public double Score { get; set; }
    }

    public class LuisEntity
    {
        [JsonProperty("entity")]
        public string Value { get; set; }

        public EntityType Type { get; set; }

        public int StartIndex { get; set; }

        public int EndIndex { get; set; }
        public LuisResolution Resolution { get; set; }
    }

    public class LuisResolution
    {
        public IList<string> Values { get; set; }
    }

    public enum EntityType
    {
        Championship,
        Circuit,
        Driver,
        Round,
        Season,
        RelativeRound,
        Position
    }

    public enum IntentType
    {
        None,
        ChampionshipPosition,
        DriverFastestLap,
        DriverRacePosition,
        FastestLap,
        RaceResults,
        RacePosition
    }
}