using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace F1DiscordBot
{
    public class MiscCommands
    {
        [Command("ping")]
        public async Task Ping(CommandContext ctx)
        {
            await ctx.RespondAsync($"Latency: {ctx.Client.Ping}ms");
        }
    }
}
