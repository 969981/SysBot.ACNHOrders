using System;
using System.Threading;
using System.Threading.Tasks;
using SysBot.Base;
using SysBot.ACNHOrders.Twitch;
using SysBot.ACNHOrders.Signalr;

namespace SysBot.ACNHOrders
{
    public static class BotRunner
    {
        public static async Task RunFrom(CrossBotConfig config, CancellationToken cancel, TwitchConfig? tConfig = null)
        {
            // Native preflight never constructs CrossBot or starts external command services.
            if (config.NativePreflightOnly)
            {
                config.AcceptingCommands = false;
#if PLATFORM_CANDIDATE
                await NativeAcnhPreflight.RunAsync(config, cancel).ConfigureAwait(false);
                return;
#else
                throw new InvalidOperationException("Native preflight requires the Platform candidate build.");
#endif
            }
            // Set up logging for Console Window
            CompatibilityLogForwarder.Add(Logger);
            static void Logger(string msg, string identity) => Console.WriteLine(GetMessage(msg, identity));
            static string GetMessage(string msg, string identity) => $"> [{DateTime.Now:hh:mm:ss}] - {identity}: {msg}";

            var bot = new CrossBot(config);

            var sys = new SysCord(bot);

            Globals.Self = sys;
            Globals.Bot = bot;
            Globals.Hub = QueueHub.CurrentInstance;
            GlobalBan.UpdateConfiguration(config);

            bot.Log("Starting Discord.");
#pragma warning disable 4014
            Task.Run(() => sys.MainAsync(config.Token, cancel), cancel);
#pragma warning restore 4014


            if (tConfig != null && !string.IsNullOrWhiteSpace(tConfig.Token))
            {
                bot.Log("Starting Twitch.");
                var _ = new TwitchCrossBot(tConfig, bot);
            }

            if (!string.IsNullOrWhiteSpace(config.SignalrConfig.URIEndpoint))
            {
                bot.Log("Starting Web.");
                var _ = new SignalrCrossBot(config.SignalrConfig, bot);
            }

            if (config.SkipConsoleBotCreation)
            {
                await Task.Delay(-1, cancel).ConfigureAwait(false);
                return;
            }

            // The compatibility candidate never automatically reconnects or replays.
            // RunCandidateAsync owns the physical device lock before any device I/O.
            await bot.RunCandidateAsync(cancel).ConfigureAwait(false);
        }
    }
}
