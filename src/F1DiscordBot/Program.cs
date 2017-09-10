using System;
using System.Configuration;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using ErgastApi.Client;

namespace F1DiscordBot
{
    internal class Program
    {
        public static DiscordClient Client { get; set; }
        public static CommandsNextModule Commands { get; set; }

        public static ErgastClient ErgastClient { get; set; }

        private static void Main() => MainAsync().GetAwaiter().GetResult();

        private static async Task MainAsync()
        {
            var cfg = new DiscordConfiguration
            {
                Token = ConfigurationManager.AppSettings["DiscordToken_F1Bot"],
                TokenType = TokenType.Bot,

                AutoReconnect = true,
                LogLevel = LogLevel.Info,
                UseInternalLogHandler = true
            };

            // then we want to instantiate our client
            Client = new DiscordClient(cfg);

            // next, let's hook some events, so we know what's going on
            Client.Ready += Client_Ready;
            Client.GuildAvailable += Client_GuildAvailable;
            Client.ClientErrored += Client_ClientErrored;

            // up next, let's set up our commands
            var ccfg = new CommandsNextConfiguration
            {
#if DEBUG
                StringPrefix = "++",
#else
                CustomPrefixPredicate = msg => Task.FromResult(msg.Content.StartsWith("show me", StringComparison.OrdinalIgnoreCase) ? 7 : -1),
                StringPrefix = "+",
#endif

                // enable responding in direct messages
                EnableDms = true,

                // enable mentioning the bot as a command prefix
                EnableMentionPrefix = true
            };

            // and hook them up
            Commands = Client.UseCommandsNext(ccfg);

            // let's hook some command events, so we know what's
            // going on
            Commands.CommandExecuted += Commands_CommandExecuted;
            Commands.CommandErrored += Commands_CommandErrored;

            // up next, let's register our commands
            Commands.RegisterCommands<RaceCommands>();
            Commands.RegisterCommands<RaceResultsCommands>();
            Commands.RegisterCommands<StandingsCommands>();
            Commands.RegisterCommands<DriverCommands>();

            Client.UseInteractivity();

            ErgastClient = new ErgastClient();

            await Client.ConnectAsync();

            await Task.Delay(-1);
        }

        private static Task Client_Ready(ReadyEventArgs e)
        {
            // let's log the fact that this event occured
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "ExampleBot", "Client is ready to process events.", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        private static Task Client_GuildAvailable(GuildCreateEventArgs e)
        {
            // let's log the name of the guild that was just
            // sent to our client
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "ExampleBot", $"Guild available: {e.Guild.Name}", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        private static Task Client_ClientErrored(ClientErrorEventArgs e)
        {
            // let's log the name of the guild that was just
            // sent to our client
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "ExampleBot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        private static Task Commands_CommandExecuted(CommandExecutionEventArgs e)
        {
            // let's log the name of the guild that was just
            // sent to our client
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info, "ExampleBot", $"{e.Context.User.Username} successfully executed '{e.Command.QualifiedName}'", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        private static async Task Commands_CommandErrored(CommandErrorEventArgs e)
        {
            // let's log the name of the guild that was just
            // sent to our client
            e.Context.Client.DebugLogger.LogMessage(
                LogLevel.Error,
                "ExampleBot",
                $"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored:" +
                $"\n{e.Exception}",
                DateTime.Now);

            // let's check if the error is a result of lack
            // of required permissions
            if (e.Exception is ChecksFailedException ex)
            {
                // yes, the user lacks required permissions,
                // let them know

                var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");

                // let's wrap the response into an embed
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = $"{emoji} You do not have the permissions required to execute this command.",
                    Color = DiscordColor.Red
                }.Build();
                await e.Context.RespondAsync("", embed: embed);
            }

            if (e.Exception is CommandNotFoundException)
                return;

            var errorEmbed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Red)
                .AddField($"{e.Exception.GetType()}", $"{e.Exception.Message ?? "<no message>"}");

            //if (e.Context.User.Id == e.Context.Client.CurrentApplication.Owner.Id)
            //    errorEmbed.AddField("StackTrace", $"```{e.Exception}```");

            await e.Context.RespondAsync(embed: errorEmbed);
        }
    }
}
