using Discord;
using Discord.WebSocket;
using Discord.Interactions;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ScenarioBot {
    public class Program
    {
        private static List<Scenario> scenarios = new List<Scenario>();
        private static List<Session> sessions;

        #region Interfaces

        // TODO: I should probably move these out of Program and into another class to reduce
        // clutter..
        public static async Task ReloadScenarios() {
            await Log(new LogMessage(
                LogSeverity.Info, "ReloadScenarios()", "Scenario reload triggered"
            ));

            Program.scenarios.Clear();
            string[] filenames = Directory.GetFiles("scenarios/");
            foreach (string f in filenames) {
                await Log(new LogMessage(
                    LogSeverity.Info, "ReloadScenarios()", $"Loading scenario {f}..."
                ));

                string json = await File.ReadAllTextAsync(f);
                Scenario s = JsonConvert.DeserializeObject<Scenario>(json) 
                    ?? throw new Exception("Deserialized scenario was null!");

                Program.scenarios.Add(s);
            }
        }
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
                scenario_obj = scenario,
                guid = System.Guid.NewGuid().ToString()
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

        public static Session? GetSessionByUser(ulong user_id) {
            return Program.sessions.FirstOrDefault(s => s.user_id == user_id);
        }

        public static Session? GetSessionByGuid(string guid) {
            return Program.sessions.FirstOrDefault(s => s.guid == guid);
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
            
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);

            // Load scenarios
            string[] filenames = Directory.GetFiles("scenarios/");
            foreach (string f in filenames) {
                Log(new LogMessage(
                    LogSeverity.Info, "Main()", $"Loading scenario {f}..."
                ));

                string json = File.ReadAllText(f);
                Scenario s = JsonConvert.DeserializeObject<Scenario>(json) 
                    ?? throw new Exception("Deserialized scenario was null!");

                Program.scenarios.Add(s);
            }

            // Load scenario session data
            string session_json = File.ReadAllText("sessions.json");
            Program.sessions = JsonConvert.DeserializeObject<List<Session>>(session_json)
                ?? throw new Exception("Failed to deserialize session data!");
            
            // Initialize session objects
            foreach (Session sesh in Program.sessions) {
                Scenario to_copy = Program.scenarios.Where(
                    Scenario => Scenario.info.id == sesh.scenario_id
                ).First();
                sesh.scenario_obj = new Scenario(to_copy);
            }

            // Set up timer to save data
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            Task timerTask = Program.SavePeriodically(TimeSpan.FromMinutes(30), tokenSource.Token);

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
                if (IsDebug()) {
                    await commands.RegisterCommandsToGuildAsync(configuration.GetValue<ulong>("testGuild"), true);
                    await commands.RegisterCommandsToGuildAsync(configuration.GetValue<ulong>("eufasGuild"), true);
                } else {
                    await commands.RegisterCommandsGloballyAsync(true);
                }
            };

            client.LoggedOut += async() =>
            {
                // Serialize session data
                string s = JsonConvert.SerializeObject(Program.sessions);
                await File.WriteAllTextAsync("sessions.json", s);
            };

            await services.GetRequiredService<CommandHandler>().InitializeAsync();

            await client.LoginAsync(TokenType.Bot, configuration["token"]);
            await client.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }

        private static Task Quit() {
            
            return Task.CompletedTask;
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
        
        // Save data every so often
        static async Task SavePeriodically(TimeSpan interval, CancellationToken token)
        {
            while (true)
            {
                string s = JsonConvert.SerializeObject(Program.sessions);
                await File.WriteAllTextAsync("sessions.json", s);
                var l = new LogMessage(LogSeverity.Info, "Program.cs", "Saved session data.");
                await Log(l);
                await Task.Delay(interval, token);
            }
        }


        static void OnProcessExit(object sender, EventArgs e)
        {
            // Serialize session data
            string s = JsonConvert.SerializeObject(Program.sessions);
            File.WriteAllText("sessions.json", s);
        }

        #endregion Setup
    }
}
