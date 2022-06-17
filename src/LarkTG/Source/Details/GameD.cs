namespace LarkTG.Source.Details;

public static class GameD
{
    public static class Constants
    {
        public enum SessionState : byte
        {
            Beginning = 0,
            Active,
            End,
            Pause
        }

        public enum GameMode
        {
            BestPoint = 1, // Add point all users who have best result.
            WinPoint // Add point all users who have win result.
        }
    }
}
