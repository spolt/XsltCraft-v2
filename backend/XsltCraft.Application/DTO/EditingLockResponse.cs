namespace XsltCraft.Application.DTO;

public sealed class EditingLockResponse
{
    // Başka bir kullanıcı şu an aktif olarak düzenliyorsa true (istek sahibi salt-okunur olmalı).
    public bool LockedByOther { get; set; }
    public Guid? EditingUserId { get; set; }
    public string? EditingUserName { get; set; }
    public DateTime UpdatedAt { get; set; }
}
