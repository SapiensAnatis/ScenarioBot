// Largely adapted from https://github.com/discord-net/Discord.Net/blob/dev/samples/InteractionFramework/CommandHandler.cs

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ScenarioBot
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _commands;
        private readonly IServiceProvider _services;

        public CommandHandler(DiscordSocketClient client, InteractionService commands, IServiceProvider services)
        {
            _client = client;
            _commands = commands;
            _services = services;
        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            _client.InteractionCreated += HandleInteraction;

            _commands.SlashCommandExecuted += SlashCommandExecuted;
            _commands.ContextCommandExecuted += ContextCommandExecuted;
            _commands.ComponentCommandExecuted += ComponentCommandExecuted;
        }

        private Task SlashCommandExecuted(SlashCommandInfo info, Discord.IInteractionContext ctx, IResult result)
        {
            ctx.Interaction.RespondAsync("slash");
            return Task.CompletedTask;
        }

        private Task ContextCommandExecuted(ContextCommandInfo info, Discord.IInteractionContext ctx, IResult result)
        {
            ctx.Interaction.RespondAsync("context");
            return Task.CompletedTask;
        }

        private Task ComponentCommandExecuted(ComponentCommandInfo info, Discord.IInteractionContext ctx, IResult result)
        {
            ctx.Interaction.RespondAsync("component");
            return Task.CompletedTask;
        }



        private async Task HandleInteraction (SocketInteraction arg)
        {
            try
            {
                var ctx = new SocketInteractionContext(_client, arg);
                await _commands.ExecuteCommandAsync(ctx, _services);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                if(arg.Type == InteractionType.ApplicationCommand)
                    await arg.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        }
    }
}
