using LarkTG.Source.DataBase.Context;
using LarkTG.Source.DataBase.Models.EmojiGame;
using LarkTG.Source.DataBase.Models.Telegram;
using LarkTG.Source.Details;
using LarkTG.Source.Exceptions;

namespace LarkTG.Source.DataBase.Controller;

internal class EmojiGameSessionController
{
    private readonly MainCTX _mainCTX;
    public EmojiGameSessionController(MainCTX mainCTX)
    {
        _mainCTX = mainCTX;
    }

    public async Task<EmojiGameSession?> GetGameNotEndOrDefault(MagicBox mb) => await GetGameNotEndOrDefault(mb.Group);

    public async Task<EmojiGameSession?> GetGameNotEndOrDefault(TGroup? Group)
    => Group switch
    {
        null => null,
        _ => await _mainCTX.EmojiGameSessions
                            .Where(egs => Group.ID == egs.GroupOwner.ID &&
                                (
                                    egs.SessionState != GameD.Constants.SessionState.End &&
                                    egs.SessionState != GameD.Constants.SessionState.ForceEnd
                                ))
                            .Include(egs => egs.GroupOwner)
                            .Include(egs => egs.InGameUsers)
                            .Include(egs => egs.Rounds)
                            .ThenInclude(round => round.UsersScore)
                            .FirstOrDefaultAsync()
    };

    public async Task<EmojiGameSession> StartNewSessionAsync(MagicBox mb)
    {
        if (mb.ActiveSession is not null || await GetGameNotEndOrDefault(mb) is not null)
        {
            throw new GameException("Not support multi sessions game.");
        }

        var egs = new EmojiGameSession(mb.Group);
        egs.InGameUsers.Add(mb.User);

        _mainCTX.EmojiGameSessions.Add(egs);

        await _mainCTX.SaveChangesAsync();

        return egs;
    }

    public async Task<bool> UpdateSessionStateAsync(MagicBox mb, GameD.Constants.SessionState sessionState)
    {
        try
        {
            if (mb.ActiveSession is null)
            {
                return false;
            }

            mb.ActiveSession.SessionState = sessionState;
            await _mainCTX.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// return true when user will be register also false (when will be unregister)
    public async Task<bool> SwitchRegistrationUserInSessionAsync(MagicBox mb)
    {
        var foundUserRegistration = mb.ActiveSession.InGameUsers.FirstOrDefault(u => u.ID == mb.User.ID);
        if (foundUserRegistration is not null)
        {
            mb.ActiveSession.InGameUsers.Remove(foundUserRegistration);
        }
        else
        {
            mb.ActiveSession.InGameUsers.Add(mb.User);
        }

        await _mainCTX.SaveChangesAsync();
        return foundUserRegistration is null;
    }


    public async Task<bool> UserPlayRoundAsyc(MagicBox mb, EmojiGameSessionRound activeRound, Dice dice)
    {
        var activeUser = activeRound.UsersScore.FirstOrDefault(u => u.TUserID == mb.User.ID);
        if (activeUser is null)
        {
            throw new NullReferenceException("Dude, you not registred in game!");
        }

        if (activeUser.Played)
        {
            return false;
        }
        else
        {
            activeUser.Score = dice.Value;
            activeUser.Played = true;

            if (!activeRound.UsersScore.Any(u => u.Played == false))
            {
                activeRound.Played = true;
                activeRound.Active = false;
            }
        }
        await _mainCTX.SaveChangesAsync();
        return true;
    }

    public async Task StartDefaultGameAsync(MagicBox mb)
    {
        if (mb.ActiveSession is null) { return; }

        foreach (var dice in DiceD.DiceNames.Select(dn => dn.Key))
        {
            mb.ActiveSession.Rounds.Add(new EmojiGameSessionRound(mb.ActiveSession.InGameUsers)
            {
                Played = false,
                DiceMode = dice
            });
        }

        mb.ActiveSession.Rounds.First(r => r.Played == false).Active = true; //select first active round

        await _mainCTX.SaveChangesAsync();
    }

    public async Task<TUser> GetBestUser(MagicBox mb)
    {
        long bestUserId = (await GetUsersScore(mb)).OrderByDescending(u => u.Score).First().TUserID;
        return await _mainCTX.Users.FirstAsync(u => u.ID == bestUserId);
    }
    public async Task<List<EmojiGameTUserScore>> GetUsersScore(MagicBox mb)
    {
        var rounds = mb.ActiveSession.Rounds;
        List<EmojiGameTUserScore> usersScore = new();
        rounds.ForEach(round => round.UsersScore.ForEach(userscore =>
                                {
                                    var foundedUser = usersScore.FirstOrDefault(u => u.TUserID == userscore.TUserID);
                                    if (foundedUser is null)
                                    {
                                        usersScore.Add(userscore);
                                    }
                                    else
                                    {
                                        foundedUser.Score += userscore.Score;
                                    }
                                })
                      );
        return usersScore;
    }
    public async Task<string> GetUsersScoreToString(MagicBox mb)
    {
        return string.Join("\n", (await GetUsersScore(mb)).OrderByDescending(u => u.Score).Select(u => $"{_mainCTX.Users.First(user => user.ID == u.TUserID).ToString()} ({u.Score})"));
    }

    public async Task SetActiveRound(MagicBox mb, EmojiGameSessionRound round)
    {
        round.Active = true;
        await _mainCTX.SaveChangesAsync();
    }

}
