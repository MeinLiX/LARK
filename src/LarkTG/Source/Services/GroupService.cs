namespace LarkTG.Source.Services;

public interface IGroupService
{
    Task<TGroup?> GetOrCreateGroupAsync(Chat telegramChat);
    Task<TGroup?> GetGroupAsync(long groupId);
    Task<bool> UpdateGroupAsync(TGroup group);
}

public class GroupService : IGroupService
{
    private readonly MainContext _context;
    private readonly ILogger<GroupService> _logger;

    public GroupService(MainContext context, ILogger<GroupService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<TGroup?> GetOrCreateGroupAsync(Chat telegramChat)
    {
        if (telegramChat.Type != ChatType.Group &&
            telegramChat.Type != ChatType.Supergroup)
        {
            return null;
        }

        try
        {
            var existingGroup = await _context.Groups.FindAsync(telegramChat.Id);

            if (existingGroup is not null)
            {
                if (existingGroup.Title != telegramChat.Title)
                {
                    existingGroup.Title = telegramChat.Title ?? existingGroup.Title;
                    existingGroup.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }

                return existingGroup;
            }

            var newGroup = new TGroup
            {
                ID = telegramChat.Id,
                Title = telegramChat.Title ?? "Unnamed Group",
                CreatedAt = DateTime.UtcNow
            };

            _context.Groups.Add(newGroup);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new group {GroupId} ({Title})",
                telegramChat.Id, telegramChat.Title);

            return newGroup;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating/updating group {GroupId}", telegramChat.Id);
            return null;
        }
    }

    public async Task<TGroup?> GetGroupAsync(long groupId)
    {
        try
        {
            return await _context.Groups.FindAsync(groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting group {GroupId}", groupId);
            return null;
        }
    }

    public async Task<bool> UpdateGroupAsync(TGroup group)
    {
        try
        {
            group.UpdatedAt = DateTime.UtcNow;
            _context.Groups.Update(group);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating group {GroupId}", group.ID);
            return false;
        }
    }
}
