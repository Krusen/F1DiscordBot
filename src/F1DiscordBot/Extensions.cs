using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using ErgastApi.Responses.Models;

namespace F1DiscordBot
{
    public static class Extensions
    {
        public static async Task RespondWithSpoilerAsync(this CommandContext ctx, DiscordEmbed embed, string spoilerFreeMsg = null)
        {
            if (ctx.Message.Channel?.Name == "spoilers" || ctx.Channel.IsPrivate)
            {
                await ctx.RespondAsync(embed: embed);
            }
            else
            {
                var dmChannel = await ctx.Member.CreateDmChannelAsync();
                await dmChannel.SendMessageAsync(embed: embed);

                spoilerFreeMsg = spoilerFreeMsg ?? "Answered in DM to avoid spoilers";
                await ctx.RespondAsync(spoilerFreeMsg);
            }
        }

        public static string ShortName(this Driver driver)
        {
            return $"{driver.FirstName[0]}. {driver.LastName}";
        }
    }
}
