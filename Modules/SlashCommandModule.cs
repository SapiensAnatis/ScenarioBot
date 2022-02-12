using Discord;
using Discord.Interactions;
using Discord.WebSocket;

using System;
using System.IO;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace ScenarioBot.Modules
{
    public class SlashCommandModule : InteractionModuleBase<SocketInteractionContext> {

        public SlashCommandModule() {
            // Load scenario information
            string[] filenames = Directory.GetFiles("scenarios/");
            foreach (string f in filenames) {
                using (StreamReader file = File.OpenText(f))
                using (JsonTextReader reader = new JsonTextReader(file))
                {
                    var s = new Scenario(
                        (JObject)JToken.ReadFrom(reader)
                    );

                    Console.WriteLine(s.name + s.description);
                }
            }
        }

        [SlashCommand("ping", "Pings the bot and returns its latency.")]
        public async Task GreetUserAsync()
            => await RespondAsync(text: $":ping_pong: It took me {Context.Client.Latency}ms to respond to you!", ephemeral: true);

        /*[SlashCommand("scenario", "Starts a scenario.")]
        public async Task Spawn()
        {
            var scenarios = Directory.GetFiles("scenarios");
            
            var menuBuilder = new SelectMenuBuilder();
            foreach (var s in ScenarioLabels) {
                menuBuilder.AddOption(s.Item1, s.Item2);
            }

            await ReplyAsync("Here is a button!", components: builder.Build());
        }*/

        private Tuple<string, string>[] ScenarioLabels;
    }
}