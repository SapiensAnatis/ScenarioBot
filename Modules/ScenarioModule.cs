using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using System;
using System.IO;
using System.Threading.Tasks;

namespace ScenarioBot.Modules
{
    public class ScenarioModule : InteractionModuleBase<SocketInteractionContext> {
        #region SlashCommands

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

        [SlashCommand("info", "Writes the main body of text for the current stage of the active scenario.")]
        public async Task WriteScenarioText() {
            Session? s = Program.GetUserSession(Context.User.Id);
            if (s != null) {
                string text = s.GetStage().text;
                await RespondAsync($"**Prompt:** {text}");
            } else {
                await RespondAsync("You are not currently in a scenario. Use the `scenario` command to start one.");
            }
        }

        [SlashCommand("obs", "Display the observations for the current stage of the active scenario.")]
        public async Task WriteScenarioObs() {
            Session? s = Program.GetUserSession(Context.User.Id);
            if (s != null) {
                List<string>? obs = s.GetStage().obs;
                if (obs != null) {
                    await RespondAsync($"**Observations:**\n{String.Join('\n', obs)}");
                } else {
                    await RespondAsync("No obs available for this stage of the scenario.");
                }
            } else {
                await RespondAsync("You are not currently in a scenario. Use the `scenario` command to start one.");
            }
        }

        [SlashCommand("questions", "Go through the questions for the current stage of the active scenario.")]
        public async Task WriteQuestions() {
            Session? s = Program.GetUserSession(Context.User.Id);
            if (s != null) {
                List<Question>? qs = s.GetStage().questions;
                if (qs != null) {
                    // Write the first question with a show answer button
                    var builder = new ComponentBuilder().WithButton(
                        "Show answer", 
                        // identifier:answer:current_index:user_id
                        $"show_answer:0:{Context.User.Id}"
                    );

                    string question = qs.First().question;
                    await RespondAsync($"**Question:**: {question}", components: builder.Build());
                } else {
                    var builder = new ComponentBuilder().WithButton(
                            "Progress scenario",
                            $"progress_scenario:{Context.User.Id}",
                            ButtonStyle.Success
                    );

                    await RespondAsync("No questions available for this stage of the scenario.",
                        components: builder.Build());
                }
            } else {
                await RespondAsync("You are not currently in a scenario. Use the `scenario` command to start one.");
            }
        }

        [SlashCommand("logout", "Test saving stuff")]
        [RequireOwner]
        public async Task Logout() {
            if (Context.User.Id == 586753708432424978) {
                await RespondAsync("Goodbye...");
                await Context.Client.LogoutAsync();
            }
        }

        #endregion

        #region Components
        [ComponentInteraction("scenario-menu")]
        public async Task ScenarioMenuHandler(params string[] selections) {
            var scenario_id = selections.First();
            // Scenario selected. Check if user is currently in scenario.
            if (Program.GetUserSession(Context.User.Id) != null) {
                // They are already playing one
                var builder = new ComponentBuilder()
                    .WithButton("Start new scenario", $"erase_and_start:{scenario_id}", ButtonStyle.Danger);

                await RespondAsync(
                    $"You are already playing a scenario! Erase your progress and start a new one?",
                    components: builder.Build(), ephemeral: true
                );
            } else {
                Program.StartNewScenario(scenario_id, Context.User.Id);
                Session? s = Program.GetUserSession(Context.User.Id);
                string text = s!.scenario_obj.stages[s.stage].text;
                await RespondAsync($"**Prompt:** {text}");
            }
        }

        [ComponentInteraction("erase_and_start:*")]
        public async Task StartScenarioButton(string scenario_id) {
            //await DeleteOriginalResponseAsync();
            Program.ClearUserScenario(Context.User.Id);
            Program.StartNewScenario(scenario_id, Context.User.Id);

            // Start off by writing scenario text
            Session? s = Program.GetUserSession(Context.User.Id);
            string text = s!.scenario_obj.stages[s.stage].text;
            await RespondAsync($"**Prompt:** {text}");
        } 

        [ComponentInteraction("cancel")]
        public async Task CancelButton() {
            Console.Write("Deleting");
            //await DeleteOriginalResponseAsync();
        }
        

        [ComponentInteraction("show_answer:*:*")]
        public async Task ShowAnswer(string index_str, string user_id) {
            ulong owner_id = Convert.ToUInt64(user_id);
            if (owner_id == Context.User.Id) {
                Session? s = Program.GetUserSession(owner_id);
                if (s != null) {
                    List<Question> qs = s.GetStage().questions!;

                    int index = Convert.ToInt32(index_str);
                    string answer = s.GetStage().questions![index].answer;

                    // If last question, add button to progress
                    if (index + 1 == qs.Count()) {
                        var builder = new ComponentBuilder().WithButton(
                            "Progress scenario",
                            $"progress_scenario:{owner_id}",
                            ButtonStyle.Success
                        );

                        await RespondAsync($"**Answer:** {answer}", components: builder.Build());
                    } else {
                        // Otherwise just post it
                        await RespondAsync($"**Answer:** {answer}");
                    }

                    // Show the next question, if there is one
                    index++;
                    if (index < qs.Count()) {
                        string next_q = qs[index].question;
                        var builder = new ComponentBuilder().WithButton(
                                "Show answer", 
                                // identifier:answer:current_index:user_id
                                $"show_answer:{index}:{Context.User.Id}"
                        );
                        
                        await ReplyAsync($"**Question:** {next_q}", components: builder.Build());
                    }
                }
            }
        }

        [ComponentInteraction("progress_scenario:*")]
        public async Task ProgressScenario(string user_id) {
            ulong owner_id = Convert.ToUInt64(user_id);
            if (owner_id == Context.User.Id) {
                Session? s = Program.GetUserSession(owner_id);
                if (s != null) {
                    s.stage++;

                    // If there's still a stage left
                    if (s.stage < s.scenario_obj.stages.Count()) {
                        await RespondAsync($"**Prompt:** {s.GetStage().text}");
                    } else {
                        await RespondAsync("Scenario completed. Well done!");
                        Program.ClearUserScenario(owner_id);
                    }
                }
            }
        }
        #endregion
    }
}