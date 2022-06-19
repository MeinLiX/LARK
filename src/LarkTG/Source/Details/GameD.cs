using Telegram.Bot.Types.ReplyMarkups;

namespace LarkTG.Source.Details;

public static class GameD
{
    public static class Constants
    {
        public enum SessionState : byte
        {
            Registration = 0,
            SelectModes,
            Active,
            End,
            ForceEnd,
            Pause,
            Next
        }

        public enum GameMode
        {
            Default = 0, // all dices once
        }

        public enum GameScoreMode
        {
            BestPoint = 0, // Add point all users who have best result.
            WinPoint // Add point all users who have win result.
        }

        public static string GetQuery(SessionState sessionState) => sessionState switch
        {
            SessionState.Registration => "RegistrationQuery",
            SessionState.SelectModes => "SelectModesQuery",
            SessionState.Active => "ActiveQuery",
            SessionState.End => "EndQuery",
            SessionState.Pause => "PauseQuery",
            SessionState.Next => "NextQuery"
        };

        public static string GetQuery(GameMode gameMode) => gameMode switch
        {
            GameMode.Default => "DefaultGameModeQuery"
        };
    }
    public static InlineKeyboardMarkup? GetInlineKeyboardMarkup(Constants.SessionState sessionState) => sessionState switch
    {
        Constants.SessionState.Registration => new InlineKeyboardMarkup(
            new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("Register", Constants.GetQuery(sessionState)),
                InlineKeyboardButton.WithCallbackData("Next", Constants.GetQuery(Constants.SessionState.Next))
            }
        ),
        Constants.SessionState.SelectModes => new InlineKeyboardMarkup(
            new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData("Default", Constants.GetQuery(Constants.GameMode.Default))
            }
        ),
        Constants.SessionState.Active => null,
        Constants.SessionState.End => null,
        Constants.SessionState.Pause => null
    };

    public static string GetPreMessage(Constants.SessionState sessionState) => sessionState switch
    {
        Constants.SessionState.Registration => "Game session started.\nAnyone wishing to play can register!",
        Constants.SessionState.SelectModes => "Please, choose the game mode.",
        Constants.SessionState.Active => "Game started!",
        Constants.SessionState.End => "Game Over!",
        Constants.SessionState.Pause => "Game paused!"
    };
}
