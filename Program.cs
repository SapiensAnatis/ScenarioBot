#define DEBUG

using Discord;
using Discord.WebSocket;
using Discord.Interactions;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;

namespace ScenarioBot {
    public class Program
    {
        private static List<Scenario> scenarios = new List<Scenario>();
        private static List<Session> sessions;

        #region Interfaces
        public static Scenario ScenarioFactory(string scenario_id) {
            // Create new scenario without having to read JSON again
            return new Scenario(
                Program.scenarios.Where(
                    Scenario => Scenario.info.id == scenario_id
                ).First()
            );
        }

        public static void StartNewScenario(string scenario_id, ulong user_id) {
            Scenario scenario = Program.ScenarioFactory(scenario_id);

            Session sess = new Session() {
                scenario_id = scenario_id,
                user_id = user_id,
                scenario_obj = scenario
            };

            Program.sessions.Add(sess);
        }

        public static List<Info> GetScenarioDetails() {
            var result = new List<Info>();
            foreach (Scenario s in Program.scenarios) {
                result.Add(
                    new Info() {
                        name = s.info.name,
                        id = s.info.id,
                        description = s.info.description
                    }
                );
            }

            return result;
        }

        public static Session GetUserSession(ulong user_id) {
            return Program.sessions.First(s => s.user_id == user_id);
        }

        public static bool UserInScenario(ulong user_id) {
            return (
                Program.sessions.Where(s => s.user_id == user_id).Count() > 0
            );
        }

        public static void ClearUserScenario(ulong user_id) {
            Program.sessions.RemoveAll(
                s => s.user_id == user_id
            );
        }

        #endregion

        #region Setup

        public static void Main(string[] args) {
            IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true)
                    .Build();
            
            // Load scenarios
            string[] filenames = Directory.GetFiles("scenarios/");
            foreach (string f in filenames) {
                Console.WriteLine("Reading scenario " + f);

                string json = File.ReadAllText(f);
                Scenario s = JsonConvert.DeserializeObject<Scenario>(json);

                Program.scenarios.Add(s);
            }

            // Load scenario session data
            string session_json = File.ReadAllText("sessions.json");
            Program.sessions = JsonConvert.DeserializeObject<List<Session>>(session_json);
            
            // Initialize scenario objects for each
            foreach (Session sesh in Program.sessions) {
                Scenario to_copy = Program.scenarios.Where(
                    Scenario => Scenario.info.id == sesh.scenario_id
                ).First();
                sesh.scenario_obj = new Scenario(to_copy);
            }

            MainAsync(config).GetAwaiter().GetResult();
        }

        public static async Task MainAsync(IConfiguration configuration) {
            using var services = ConfigureServices(configuration);

            var client = services.GetRequiredService<DiscordSocketClient>();
            var commands = services.GetRequiredService<InteractionService>();
                
            client.Log += Log;
            commands.Log += Log;

            client.Ready += async() =>
            {
                if (IsDebug())
                    await commands.RegisterCommandsToGuildAsync(configuration.GetValue<ulong>("testGuild"), true);
                else
                    await commands.RegisterCommandsGloballyAsync(true);
            };

            await services.GetRequiredService<CommandHandler>().InitializeAsync();

            await client.LoginAsync(TokenType.Bot, configuration["token"]);
            await client.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }

        private static Task Log(LogMessage msg) {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        static ServiceProvider ConfigureServices(IConfiguration configuration)
            => new ServiceCollection()
                .AddSingleton(configuration)
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<CommandHandler>()
                .BuildServiceProvider();

        static bool IsDebug() {
            #if DEBUG
                return true;
            #else
                return false;
            #endif
        }
        #endregion Setup
    }
}
