#define DEBUG

using Discord;
using Discord.WebSocket;
using Discord.Interactions;

using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ScenarioBot {
    public class Program
    {

        public static void Main(string[] args) {
            IConfiguration config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: true)
                    .Build();

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
    }
}
