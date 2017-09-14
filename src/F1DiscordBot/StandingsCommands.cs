using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using DSharpPlus.Interactivity;
using ErgastApi.Requests;
using ErgastApi.Responses.Models.Standings;

namespace F1DiscordBot
{
    [Group("standings", CanInvokeWithoutSubcommand = true)]
    [Description("Show driver or constructor standings for a season, optionally at a specific round in the season.\n" +
                 "Season and round parameters are optional. If not specified the current standings are shown.")]
    public class StandingsCommands
    {
        public async Task ExecuteGroupAsync(CommandContext ctx)
        {
            var interactivity = ctx.Client.GetInteractivityModule();
            await ctx.RespondAsync("Missing parameter. Please specify either **wdc** or **wcc**.");

            var msg = await interactivity.WaitForMessageAsync(
                x => x.Author.Id == ctx.Message.Author.Id && (x.Content.Equals("wdc") || x.Content.Equals("wcc")),
                TimeSpan.FromSeconds(30));

            if (msg != null)
            {
                if (msg.Message.Content == "wdc")
                {
                    await DriverStandings(ctx);
                }
                else if (msg.Message.Content == "wdc")
                {
                    await ConstructorStandings(ctx);
                }
            }
        }

        [Command("wdc"), Aliases("drivers")]
        [Description("Show driver standings (WDC).")]
        public async Task DriverStandings(CommandContext ctx, string season = Seasons.Current, string round = null)
        {
            await ctx.TriggerTypingAsync();

            var driversRequest = new DriverStandingsRequest {Season = season, Round = round};

            var driversResponse= await Program.ErgastClient.GetResponseAsync(driversRequest);

            var driverStandings = driversResponse.StandingsLists.FirstOrDefault();
            if (driverStandings != null)
            {
                var driversEmbed = GetDriverStandingsEmbed(driverStandings);
                await ctx.RespondWithSpoilerAsync(embed: driversEmbed);
            }
            else
            {
                await ctx.RespondAsync("Sorry, no standings found for that query");
            }
        }

        [Command("wcc"), Aliases("constructors")]
        [Description("Show constructor standings (WCC)")]
        public async Task ConstructorStandings(CommandContext ctx, string season = Seasons.Current, string round = null)
        {
            await ctx.TriggerTypingAsync();

            var constructorsRequest = new ConstructorStandingsRequest { Season = season, Round = round };

            var constructorsResponseTask = Program.ErgastClient.GetResponseAsync(constructorsRequest);

            var constructorStandings = (await constructorsResponseTask).StandingsLists.FirstOrDefault();
            if (constructorStandings != null)
            {
                var constructorsEmbed = GetConstructorsEmbed(constructorStandings);
                await ctx.RespondWithSpoilerAsync(embed: constructorsEmbed);
            }
            else
            {
                await ctx.RespondAsync("Sorry, no standings found for that query");
            }
        }

        //public async Task DriverStandings(CommandContext ctx, string season = Seasons.Current, string round = null)
        //{
        //    await ctx.TriggerTypingAsync();

        //    var driversRequest = new DriverStandingsRequest { Season = season, Round = round };
        //    var constructorsRequest = new ConstructorStandingsRequest { Season = season, Round = round };

        //    var driversResponseTask = Program.ErgastClient.GetResponseAsync(driversRequest);
        //    var constructorsResponseTask = Program.ErgastClient.GetResponseAsync(constructorsRequest);

        //    var driverStandings = (await driversResponseTask).StandingsLists.FirstOrDefault();
        //    if (driverStandings != null)
        //    {
        //        var driversEmbed = GetDriverStandingsEmbed(driverStandings);
        //        await ctx.RespondWithSpoilerAsync(driversEmbed);
        //    }

        //    var constructorStandings = (await constructorsResponseTask).StandingsLists.FirstOrDefault();
        //    if (constructorStandings != null)
        //    {
        //        var constructorsEmbed = GetConstructorsEmbed(constructorStandings);
        //        await ctx.RespondWithSpoilerAsync(constructorsEmbed);
        //    }
        //}

        private static DiscordEmbed GetDriverStandingsEmbed(DriverStandingsList standingsList)
        {
            var embed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Aquamarine
            };

            var leader = standingsList.Standings.First();

            embed.AddField("Season", $"{standingsList.Season}", inline: true);
            embed.AddField("Round", $"{standingsList.Round}", inline: true);
            //embed.AddField("Leader", $"{Flags.GetFlag(leader.Driver.Nationality)}  {leader.Driver.FullName} ({leader.Constructor.Name}) with {leader.Points} points and {leader.Wins} wins");
            embed.AddField("Top 10", GetDriverStandingsTable(standingsList, 0));
            embed.AddField("11-20", GetDriverStandingsTable(standingsList, 10));

            if (standingsList.Standings.Count > 20)
                embed.AddField($"21-{standingsList.Standings.Count}", GetDriverStandingsTable(standingsList, 20));

            return embed.Build();
        }

        private static string GetDriverStandingsTable(DriverStandingsList standingsList, int skip)
        {
            var sb = new StringBuilder();

            sb.AppendLine("```");

            if (skip == 0)
            {
                sb.AppendLine(" #  DRIVER               CONSTRUCTOR    PTS WINS");
                sb.AppendLine("------------------------------------------------"); // Max 55 wide
            }

            //  #  Driver               Constructor    Pts Wins
            // -------------------------------------------------------|
            //  1  Lewis Hamilton       Mercedes       365   12
            //  2  Sebastian Vettel     Ferrari        358    5
            //  3  Stoffel Vandoorne    McLaren         25    1
            //  4  Antonio Giovinazzi   Haas F1 Team     0
            //  5  Fernando Alonso      Force India      0
            //  6  Giancarlo Fisichella Manor Marussia   0
            // 11  Sebastian Vettel     Toro Rosso       0

            foreach (var entry in standingsList.Standings.Skip(skip).Take(10))
            {
                sb.AppendLine($"{entry.Position,2}  {entry.Driver.FullName,-20} {entry.Constructor.Name,-14} {entry.Points,3:N0}  {entry.Wins,3}");
            }

            sb.AppendLine("```");

            return sb.ToString();
        }

        private static DiscordEmbed GetConstructorsEmbed(ConstructorStandingsList standingsList)
        {
            var embed = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Aquamarine
            };

            var leader = standingsList.Standings.First();

            embed.AddField("Season", $"{standingsList.Season}", inline: true);
            embed.AddField("Round", $"{standingsList.Round}", inline: true);
            //embed.AddField("Leader", $"{Flags.GetFlag(leader.Constructor.Nationality)}  {leader.Constructor.Name} with {leader.Points} points and {leader.Wins} wins");
            embed.AddField("Standings", GetConstructorStandingsTable(standingsList));

            return embed.Build();
        }

        private static string GetConstructorStandingsTable(ConstructorStandingsList standingsList)
        {
            var sb = new StringBuilder();

            sb.AppendLine("```");

            sb.AppendLine(" #  CONSTRUCTOR    PTS   WINS");
            sb.AppendLine("-----------------------------"); // Max 55 wide

            //  #  Constructor    Pts  Wins
            // -------------------------------------------------------|
            //  1  Mercedes       365    14
            //  2  Ferrari        358     5
            //  3  McLaren         25     1
            //  4  Haas F1 Team     0
            //  5  Manor Marussia   0
            // 11  Toro Rosso       0

            foreach (var entry in standingsList.Standings)
            {
                sb.AppendLine($"{entry.Position,2}  {entry.Constructor.Name,-14} {entry.Points,3:N0}  {entry.Wins,3}");
            }

            sb.AppendLine("```");

            return sb.ToString();
        }
    }
}
