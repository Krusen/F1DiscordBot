﻿using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using ErgastApi.Requests;
using ErgastApi.Responses.Models.RaceInfo;

namespace F1DiscordBot
{
    public class RaceCommands
    {
        [Command("nextrace"), Aliases("next")]
        [Description("Shows info about the upcoming race.")]
        public async Task NextRace(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            var request = new RaceListRequest
            {
                Season = Seasons.Current,
                Round = Rounds.Next
            };

            var response = await Program.ErgastClient.GetResponseAsync(request);

            var race = response.Races.FirstOrDefault();
            if (race == null)
            {
                await ctx.RespondAsync("No upcoming race. This season must be over.");
                return;
            }

            var embed = GetRaceInfoEmbed(race);

            await ctx.RespondAsync(embed: embed);
        }

        [Command("lastrace"), Aliases("last", "prev", "previousrace")]
        [Description("Shows info about the latest race.")]
        public async Task LastRace(CommandContext ctx)
        {
            await ctx.TriggerTypingAsync();

            var request = new RaceListRequest
            {
                Season = Seasons.Current,
                Round = Rounds.Last
            };

            var response = await Program.ErgastClient.GetResponseAsync(request);

            var race = response.Races.FirstOrDefault();
            if (race == null)
            {
                await ctx.RespondAsync("No previous race. This season must not have started yet.");
                return;
            }

            var embed = GetRaceInfoEmbed(race);

            await ctx.RespondAsync(embed: embed);
        }

        private DiscordEmbed GetRaceInfoEmbed(Race race)
        {
            var embed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Aquamarine
            };

            embed.AddField("Season", $"{race.Season}", inline: true);
            embed.AddField("Round", $"{race.Round}", inline: true);
            embed.AddField("Date", $"{race.StartTime:yyyy-MM-dd}", inline: true);
            embed.AddField($"Race Start ({FormatTimeToRaceStart(race.StartTime)})", $"{race.StartTime:HH':'mm' GMT'} / {race.StartTime.Add(TimeSpan.FromHours(-5)):HH':'mm' EST'} / {race.StartTime.Add(TimeSpan.FromHours(-8)):HH':'mm' PST'}\n" +
                                         $"");
            embed.AddField("Race", $"[{race.RaceName}]({race.WikiUrl})", inline: true);
            embed.AddField("Circuit", $"[{race.Circuit.CircuitName}]({race.Circuit.WikiUrl})", inline: true);
            embed.AddField("Location", $"{race.Circuit.Location.Locality}, {race.Circuit.Location.Country}");

            return embed.Build();
        }

        private static string FormatTimeToRaceStart(DateTime raceStart)
        {
            var utcNow = DateTime.UtcNow;

            var hasStarted = raceStart < utcNow;
            var diff = hasStarted ? utcNow - raceStart : raceStart - utcNow;

            var format = "";

            if (diff.Days > 0)
                format += $"d' day{(diff.Days > 1 ? "s" : "")} '";

            if (diff.Days > 0 || diff.Hours > 0)
                format += $"h' hour{(diff.Hours > 1 ? "s" : "")} '";

            format += $"m' minute{(diff.Minutes > 1 ? "s" : "")}'";

            var output = diff.ToString(format);

            if (hasStarted)
            {
                output += " ago";
            }
            else
            {
                output = "in " + output;
            }

            return output;
        }
    }
}
