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
            Session? s = Program.GetSessionByUser(Context.User.Id);
            if (s != null) {
                string text = s.GetStage().text;
                await RespondAsync($"**Prompt:** {text}");
            } else {
                await RespondAsync(
                    "You are not currently in a scenario. Use the `scenario` command to start one.",
                    ephemeral: true
                );
            }
        }

        [SlashCommand("obs", "Display the observations for the current stage of the active scenario.")]
        public async Task WriteScenarioObs() {
            Session? s = Program.GetSessionByUser(Context.User.Id);
            if (s != null) {
                List<string>? obs = s.GetStage().obs;
                if (obs != null) {
                    await RespondAsync($"**Observations:**\n{String.Join('\n', obs)}");
                } else {
                    await RespondAsync("No obs available for this stage of the scenario.");
                }
            } else {
                await RespondAsync(
                    "You are not currently in a scenario. Use the `scenario` command to start one.",
                    ephemeral: true    
                );
            }
        }

        [SlashCommand("questions", "Go through the questions for the current stage of the active scenario.")]
        public async Task WriteQuestions() {
            Session? s = Program.GetSessionByUser(Context.User.Id);
            if (s == null) {
                await RespondAsync(
                    "You are not currently in a scenario. Use the `/scenario` command to start one.",
                    ephemeral: true
                );
                return;
            }

            List<Question>? qs = s.GetStage().questions;
            if (qs != null) {
                // Write the first question with a show answer button
                var builder = new ComponentBuilder().WithButton(
                    "Show answer", 
                    // identifier:answer:current_index:session_guid
                    $"show_answer:0:{s.guid}"
                );

                string question = qs.First().question;
                await RespondAsync($"**Question:**: {question}", components: builder.Build());
            } else {
                var builder = new ComponentBuilder().WithButton(
                        "Progress scenario",
                        $"progress_scenario:{s.guid}:{s.stage}",
                        ButtonStyle.Success
                );

                await RespondAsync("No questions available for this stage of the scenario.",
                    components: builder.Build());
            }
        }

        [SlashCommand("logout", "Disconnect the bot from Discord.")]
        [RequireOwner]
        public async Task Logout() {
            await RespondAsync("Goodbye...");
            await Context.Client.LogoutAsync();
        }

        [SlashCommand("reload", "Reload the scenario list.")]
        [RequireOwner]
        public async Task ReloadScenarios() {
            await Program.ReloadScenarios();
            await RespondAsync("Reload successful.", ephemeral: true);
        }

        #endregion

        #region Components
        [ComponentInteraction("scenario-menu")]
        public async Task ScenarioMenuHandler(params string[] selections) {
            var scenario_id = selections.First();

            Session? s = Program.GetSessionByUser(Context.User.Id);
            // Scenario selected. Check if user is currently in scenario.
            if (s == null) {
                Program.StartNewScenario(scenario_id, Context.User.Id);
                s = Program.GetSessionByUser(Context.User.Id);
                string text = s!.scenario_obj.stages[s.stage].text;
                await RespondAsync($"**Prompt:** {text}");
            } else {
                // They are already playing one
                var builder = new ComponentBuilder()
                    // label:session_guid:new_scenario_id
                    // guid uniquely identifies the Session, to stop the button being pressed by randoms
                    .WithButton("Start new scenario", $"erase_and_start:{s.guid}:{scenario_id}", ButtonStyle.Danger);

                await RespondAsync(
                    $"You are already playing a scenario! Erase your progress and start a new one?",
                    components: builder.Build(), ephemeral: true
                );
            }
        }

        [ComponentInteraction("erase_and_start:*:*")]
        public async Task StartScenarioButton(string session_guid, string scenario_id) {
            // Ensure that the user's session is still the one we were trying to delete.
            Session? to_delete = Program.GetSessionByGuid(session_guid);
            Session? user_session = Program.GetSessionByUser(Context.User.Id);

            if (to_delete == null || user_session == null) {
                await RespondAsync("This prompt has expired.", ephemeral: true);
                return;
            } else if (to_delete.guid != user_session.guid) {
                await RespondAsync("This prompt has expired.", ephemeral: true);
                return;
            }

            Program.ClearUserScenario(Context.User.Id);
            Program.StartNewScenario(scenario_id, Context.User.Id);

            // Start off by writing scenario text
            Session? s = Program.GetSessionByUser(Context.User.Id);
            string text = s!.scenario_obj.stages[s.stage].text;
            await RespondAsync($"**Prompt:** {text}");
        } 
        
        // TODO: Consider embedding the answer and next prompt in button id? Otherwise it could
        // advance the user's existing scenario if they scroll back.
        [ComponentInteraction("show_answer:*:*")]
        public async Task ShowAnswer(string index_str, string session_guid) {
            Session? s = Program.GetSessionByGuid(session_guid);

            if (s == null) {
                await RespondAsync("This scenario has expired.", ephemeral: true);
                return;
            }

            ulong owner_id = Convert.ToUInt64(s.user_id);
            if (owner_id != Context.User.Id) {
                await RespondAsync("This isn't your scenario!", ephemeral: true);
                return;
            }

            List<Question> qs = s.GetStage().questions!;

            int index = Convert.ToInt32(index_str);
            string answer = s.GetStage().questions![index].answer;

            // If last question, add button to progress
            if (index + 1 == qs.Count()) {
                var builder = new ComponentBuilder().WithButton(
                    "Progress scenario",
                    $"progress_scenario:{s.guid}:{s.stage}",
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
                        // identifier:answer:current_index:session_guid
                        $"show_answer:{index}:{s.guid}"
                );
                
                await ReplyAsync($"**Question:** {next_q}", components: builder.Build());
            }
        }

        [ComponentInteraction("progress_scenario:*:*")]
        public async Task ProgressScenario(string session_guid, string session_stage) {
            Session? s = Program.GetSessionByGuid(session_guid);

            if (s == null) {
                await RespondAsync("This scenario has expired.", ephemeral: true);
                return;
            }

            if (s.user_id != Context.User.Id) {
                await RespondAsync("This isn't your scenario!", ephemeral: true);
                return;
            }

            if (Convert.ToInt32(session_stage) != s.stage) {
                await RespondAsync("This prompt has expired.", ephemeral: true);
                return;
            }

            s.stage++;

            // If there's still a stage left
            if (s.stage < s.scenario_obj.stages.Count()) {
                await RespondAsync($"**Prompt:** {s.GetStage().text}");
            } else {
                await RespondAsync("Scenario completed. Well done!");
                Program.ClearUserScenario(s.user_id);
            }
        }
        #endregion
    }
}