using LarkTG.Source.Handlers;
using LarkTG.Source.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

dotenv.net.DotEnv.Load();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/bot-.txt",
        rollingInterval: RollingInterval.Day,
        outputTemplate:
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting Dice Game Bot...");

    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSingleton<ITelegramBotService, TelegramBotService>();

    builder.Services.AddDbContext<MainContext>(options => { });

    builder.Services.AddScoped<IGameSessionService, GameSessionService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IGroupService, GroupService>();

    builder.Services.AddScoped<GameValidationService>();

    builder.Services.AddScoped<UpdateHandler>();
    builder.Services.AddScoped<GameCommandHandler>();
    builder.Services.AddScoped<CallbackQueryHandler>();

    builder.Services.AddLogging(loggingBuilder =>
    {
        loggingBuilder.ClearProviders();
        loggingBuilder.AddSerilog();
    });

    var host = builder.Build();

    var botToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN");
    if (string.IsNullOrEmpty(botToken))
    {
        Log.Fatal("TELEGRAM_BOT_TOKEN environment variable is not set!");
        Log.Information("Please set your bot token in the .env file or environment variables.");
        return;
    }

    var dbName = Environment.GetEnvironmentVariable("SQLite_MainCTX_Name") ?? "gamebot.db";
    Log.Information("Using database: {DatabaseName}", dbName);

    using var scope = host.Services.CreateScope();

    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<MainContext>();
        var canConnect = await dbContext.Database.CanConnectAsync();
        if (!canConnect)
        {
            Log.Warning("Database connection test failed, but continuing...");
        }
        else
        {
            Log.Information("Database connection successful");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Database initialization error");
        throw;
    }

    var botService = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();
    await botService.StartAsync();

    Log.Information("Bot started successfully! Press Ctrl+C to stop...");

    var cancellationTokenSource = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cancellationTokenSource.Cancel();
        Log.Information("Shutdown signal received...");
    };

    try { await Task.Delay(-1, cancellationTokenSource.Token); } catch (OperationCanceledException) { }

    Log.Information("Stopping bot...");
    await botService.StopAsync();
    Log.Information("Bot stopped successfully");
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}