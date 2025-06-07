namespace LarkTG.Source.Services;

public interface IUserService
{
    Task<TUser?> GetOrCreateUserAsync(User telegramUser);
    Task<TUser?> GetUserAsync(long userId);
    Task<bool> UpdateUserAsync(TUser user);
}

public class UserService : IUserService
{
    private readonly MainContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(MainContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TUser?> GetOrCreateUserAsync(User telegramUser)
    {
        try
        {
            var existingUser = await _context.Users.FindAsync(telegramUser.Id);

            if (existingUser is not null)
            {
                var hasChanges = false;

                if (existingUser.FirstName != telegramUser.FirstName)
                {
                    existingUser.FirstName = telegramUser.FirstName;
                    hasChanges = true;
                }

                if (existingUser.Username != telegramUser.Username)
                {
                    existingUser.Username = telegramUser.Username;
                    hasChanges = true;
                }

                if (hasChanges)
                {
                    existingUser.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return existingUser;
            }

            var newUser = new TUser
            {
                ID = telegramUser.Id,
                FirstName = telegramUser.FirstName,
                Username = telegramUser.Username,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new user {UserId} ({FirstName})",
                telegramUser.Id, telegramUser.FirstName);

            return newUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating user {UserId}", telegramUser.Id);
            return null;
        }
    }

    public async Task<TUser?> GetUserAsync(long userId)
    {
        try
        {
            return await _context.Users.FindAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserId}", userId);
            return null;
        }
    }

    public async Task<bool> UpdateUserAsync(TUser user)
    {
        try
        {
            user.UpdatedAt = DateTime.UtcNow;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId}", user.ID);
            return false;
        }
    }
}