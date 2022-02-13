using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using System;
using System.IO;
using System.Threading.Tasks;

namespace ScenarioBot.Modules
{
    public class SlashCommandModule : InteractionModuleBase<SocketInteractionContext> {

        [SlashCommand("ping", "Pings the bot and returns its latency.")]
        public async Task GreetUserAsync()
            => await RespondAsync(text: $":ping_pong: It took me {Context.Client.Latency}ms to respond to you!", ephemeral: true);

        [SlashCommand("scenario", "Starts a scenario.")]
        public async Task Spawn()
        {
            var menuBuilder = new SelectMenuBuilder()
                .WithPlaceholder("Select an option")
                .WithCustomId("scenario-menu")
                .WithMinValues(1)
                .WithMaxValues(1);
        
            foreach (Info i in Program.GetScenarioDetails()) {
                menuBuilder.AddOption(i.name, i.id, i.description);
            }

            var builder = new ComponentBuilder().WithSelectMenu(menuBuilder);

            await RespondAsync("Choose a scenario from below!", components: builder.Build());
        }

        [ComponentInteraction("scenario-menu")]
        public async Task ScenarioMenuHandler(params string[] selections) {
            var scenario_id = selections.First();
            // Scenario selected. Check if user is currently in scenario.
            if (Program.UserInScenario(Context.User.Id)) {
                // They are already playing one
                var builder = new ComponentBuilder()
                    .WithButton("Start new scenario", $"erase_and_start:{scenario_id}", ButtonStyle.Danger);

                await RespondAsync(
                    $"You are already playing a scenario! Erase your progress and start a new one?",
                    components: builder.Build(), ephemeral: true
                );
            } else {
                Program.StartNewScenario(scenario_id, Context.User.Id);
            }
        }

        [ComponentInteraction("erase_and_start:*")]
        public async Task StartScenarioButton(string scenario_id) {
            Program.ClearUserScenario(Context.User.Id);
            Program.StartNewScenario(scenario_id, Context.User.Id);
        } 
    }
}