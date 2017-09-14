using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using ErgastApi.Responses.Models;

namespace F1DiscordBot
{
    public static class Extensions
    {
        public static async Task RespondWithSpoilerAsync(this CommandContext ctx, string message = null, DiscordEmbed embed = null, string spoilerFreeMsg = null)
        {
            if (ctx.Message.Channel?.Name == "spoilers" || ctx.Channel.IsPrivate)
            {
                await ctx.RespondAsync(message, embed: embed);
            }
            else
            {
                var dmChannel = await ctx.Member.CreateDmChannelAsync();
                await dmChannel.SendMessageAsync(message, embed: embed);

                spoilerFreeMsg = spoilerFreeMsg ?? "Answered in DM to avoid spoilers";
                await ctx.RespondAsync(spoilerFreeMsg);
            }
        }

        public static string ShortName(this Driver driver)
        {
            return $"{driver.FirstName[0]}. {driver.LastName}";
        }

        public static DiscordEmbedBuilder AddField(this DiscordEmbedBuilder builder, string name, object value, bool inline = false)
        {
            return builder.AddField(name, value.ToString(), inline);
        }

        public static string WithSuffix(this int num)
        {
            if (num.ToString().EndsWith("11")) return num + "th";
            if (num.ToString().EndsWith("12")) return num + "th";
            if (num.ToString().EndsWith("13")) return num + "th";
            if (num.ToString().EndsWith("1")) return num + "st";
            if (num.ToString().EndsWith("2")) return num + "nd";
            if (num.ToString().EndsWith("3")) return num + "rd";
            return num + "th";
        }

        public static string WithSuffix(this double num) => WithSuffix((int) Math.Round(num, MidpointRounding.AwayFromZero));

        public static T GetRandom<T>(this IList<T> list)
        {
            if (!list.Any())
                return default;

            var rnd = new Random();
            return list[rnd.Next(0, list.Count)];
        }
    }
}
