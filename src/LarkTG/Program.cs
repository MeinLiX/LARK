using LarkTG.Source;

dotenv.net.DotEnv.Load();

#region required Environments
string TELEGRAM_BOT_TOKEN = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? throw new NullReferenceException("Telegram bot token");
#endregion

using CancellationTokenSource cts = new();

TelegramBot telegramBot = new(TELEGRAM_BOT_TOKEN, cts.Token);

await telegramBot.StartAsync();

Console.ReadLine();
cts.Cancel();
