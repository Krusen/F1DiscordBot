using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using ErgastApi.Requests;
using ErgastApi.Responses.Models;
using Newtonsoft.Json;

namespace F1DiscordBot
{
    public class AskCommands
    {
        private readonly string LuisEndpoint = ConfigurationManager.AppSettings["LuisEndpoint_F1Bot"] ?? throw new Exception($"Missing app setting 'LuisEndpoint_F1Bot'");

        private readonly IList<string> UnknownQueryRespones = new List<string>
        {
            "I have no idea what you are trying to ask...",
            "You'll have to be more specific than that, sir",
            "Is that even a question?",
            "Why would I know that?"
        };

        [Command("bot")]
        public async Task Ask(CommandContext ctx, [RemainingText] string query)
        {
            await ctx.TriggerTypingAsync();

            var webClient = new WebClient();

            var json = await webClient.DownloadStringTaskAsync(LuisEndpoint + HttpUtility.UrlEncode(query));

            var response = JsonConvert.DeserializeObject<LuisResponse>(json);

            if (response.TopScoringIntent.Intent == IntentType.RaceWinner)
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
                                           response.Entities.FirstOrDefault(x => x.Type == EntityType.RelativeRound)?.Resolution.Values.First();
                }

                var circuitEntity = response.Entities.FirstOrDefault(x => x.Type == EntityType.Circuit);
                if (circuitEntity != null)
                {
                    circuit = await FindCircuitAsync(circuitEntity.Value, resultsRequest.Season);

                    if (circuit == null)
                    {
                        await ctx.RespondAsync(
                            $"I couldn't find a race in **{(resultsRequest.Season == Seasons.Current ? DateTime.Now.Year.ToString() : resultsRequest.Season)}** matching **{circuitEntity.Value}**");
                        return;
                    }
                }
                else
                {
                    await ctx.RespondAsync($"I wasn't able to determine the circuit from your question");
                    return;
                }

                var resultsResponse = await Program.ErgastClient.GetResponseAsync(resultsRequest);

                if (!resultsResponse.Races.Any())
                {
                    await ctx.RespondAsync("I could not find any results matching your question");
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

                await ctx.RespondAsync($"**{driver.Driver.FullName} ({driver.Constructor.Name})** finished **{positionText}** ({driver.Points} points) at the **{race.RaceName}** in **{race.Season}**");
                return;
            }

            await ctx.RespondAsync(UnknownQueryRespones.GetRandom());
        }

        private static async Task<Circuit> FindCircuitAsync(string id, string season)
        {
            var ergastClient = Program.ErgastClient;

            var allCircuitsRequest = new CircuitInfoRequest { Season = season, Limit = 1000 };
            var allCircuitsResponse = await ergastClient.GetResponseAsync(allCircuitsRequest);

            var circuits = allCircuitsResponse.Circuits;

            var ignoreCase = StringComparison.OrdinalIgnoreCase;

            return circuits.FirstOrDefault(x => string.Equals(x.CircuitId, id, ignoreCase)) ??
                   circuits.FirstOrDefault(x => string.Equals(x.CircuitName, id, ignoreCase)) ??
                   circuits.FirstOrDefault(x => string.Equals(x.Location.Country, id, ignoreCase)) ??
                   circuits.FirstOrDefault(x => string.Equals(x.Location.Locality, id, ignoreCase)) ??
                   circuits.FirstOrDefault(x => Regex.IsMatch(x.CircuitName, ".*id.*", RegexOptions.IgnoreCase));
        }
    }

    public class LuisResponse
    {
        public string Query { get; set; }

        public LuisIntent TopScoringIntent { get; set; }

        public IList<LuisEntity> Entities { get; set; }
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
        Circuit,
        Round,
        Season,
        RelativeRound,
        Position
    }

    public enum IntentType
    {
        None,
        ChampionshipWinner,
        RaceResults,
        RaceWinner
    }
}
