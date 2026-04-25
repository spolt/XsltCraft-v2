using XsltCraft.Application.DTO;

namespace XsltCraft.Application.Interfaces;

public interface IUserManagementService
{
    Task<UserListResponse> ListUsersAsync(string? query, string? role, bool? isActive, int page, int pageSize);
    Task<UserListItem?> GetUserAsync(Guid id);
    Task<(bool Success, string? Error)> SetRoleAsync(Guid targetUserId, Guid adminId, string role);
    Task<(bool Success, string? Error)> SetActiveAsync(Guid targetUserId, Guid adminId, bool isActive);
    Task<(bool Success, string? Error)> CreateUserAsync(CreateManagedUserRequest request);
    Task<(bool Success, string? Error)> ResetPasswordAsync(Guid targetUserId, string newPassword);
    Task<(bool Success, string? Error)> DeleteUserAsync(Guid targetUserId, Guid adminId);
}
