using LarkTG.Source.Handlers;
using LarkTG.Source.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

dotenv.net.DotEnv.Load();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/bot-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<ITelegramBotService, TelegramBotService>();
builder.Services.AddScoped<IGameSessionService, GameSessionService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<UpdateHandler>();
builder.Services.AddScoped<GameCommandHandler>();
builder.Services.AddScoped<CallbackQueryHandler>();
builder.Services.AddScoped<GameValidationService>();
builder.Services.AddDbContext<MainContext>();

builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog());

var host = builder.Build();

using var scope = host.Services.CreateScope();
var botService = scope.ServiceProvider.GetRequiredService<ITelegramBotService>();

await botService.StartAsync();

Console.WriteLine("Bot started! Press Enter to stop...");
Console.ReadLine();

await botService.StopAsync();