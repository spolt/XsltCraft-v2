namespace XsltCraft.Application.DTO;

public record UserListItem(
    Guid Id,
    string Username,
    string Email,
    string? DisplayName,
    string Role,
    string Plan,
    DateTime? PlanExpiresAt,
    bool IsActive,
    int SaveCount,
    int DownloadCount,
    DateTime? LastActivityAt,
    DateTime? LastLoginAt,
    DateTime CreatedAt
);

public record UserListResponse(
    IReadOnlyList<UserListItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int ActiveCount,
    int InactiveCount,
    int TotalActivityCount
);

public record SetRoleRequest(string Role);

public record SetActiveRequest(bool IsActive);

/// <summary>Admin'in bir kullanıcıya plan ataması (comp/grant). Plan: "Free" | "Pro". ExpiresAt null = süresiz.</summary>
public record SetPlanRequest(string Plan, DateTime? ExpiresAt);

public record CreateManagedUserRequest(
    string Username,
    string Email,
    string? DisplayName,
    string Role,
    string Password
);

public record ResetPasswordRequest(string NewPassword);
