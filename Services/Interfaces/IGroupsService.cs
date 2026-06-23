using Kpett.ChatApp.DTOs.Request.Group;
using Kpett.ChatApp.DTOs.Response.Group;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Models;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IGroupsService
    {

        // Tạo / Sửa / Xóa
        Task<CreateGroupResponse> CreateGroupAsync(string userId, CreateGroupRequest request, CancellationToken cancel = default);
        Task<GroupDetailResponse> UpdateGroupAsync(string userId, string groupId, UpdateGroupRequest request, CancellationToken cancel = default);
        Task DeleteGroupAsync(string userId, string groupId, DeleteGroupRequest? request = null, CancellationToken cancel = default);


        // Xem chi tiết
        Task<GroupDetailResponse> GetGroupByIdAsync(string userId, string groupId, CancellationToken cancel = default);
        Task<GroupDetailResponse> GetGroupBySlugAsync(string userId, string slug, CancellationToken cancel = default);

        // Tìm kiếm
        Task<SearchGroupResponse> SearchGroupsAsync(string userId, SearchGroupRequest request, CancellationToken cancel = default);

        // Danh sách nhóm của tôi
        Task<MyGroupsResponse> GetMyGroupsAsync(string userId, MyGroupsRequest request, CancellationToken cancel = default);

        // Cài đặt nhóm
        Task<GroupSettingsResponse> GetGroupSettingsAsync(string userId, string groupId, CancellationToken cancel = default);
        Task<GroupSettingsResponse> UpdateGroupSettingsAsync(string userId, string groupId, UpdateGroupSettingsRequest request, CancellationToken cancel = default);
        Task<GroupSettingsResponse> UpdateGroupRulesAsync(string userId, string groupId, UpdateGroupRulesRequest request, CancellationToken cancel = default);

        // Quản lý thành viên
        Task<GroupMembershipActionResponse> JoinGroupAsync(string userId, string groupId, CancellationToken cancel = default);
        Task<GroupMembershipActionResponse> LeaveGroupAsync(string userId, string groupId, CancellationToken cancel = default);
        Task<GroupInviteMembersResponse> InviteMembersAsync(string userId, string groupId, InviteGroupMembersRequest request, CancellationToken cancel = default);
        Task<GroupMemberResponse> AcceptJoinRequestAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);
        Task<GroupMembershipActionResponse> DeclineJoinRequestAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);
        Task<GroupMemberListResponse> GetGroupMembersAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);
        Task<GroupMemberListResponse> GetPendingJoinRequestsAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);
        Task<GroupMembershipActionResponse> KickMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);
        Task<GroupMembershipActionResponse> BlockMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);
        Task<GroupMemberResponse> UpdateMemberRoleAsync(string userId, string groupId, string targetUserId, UpdateGroupMemberRoleRequest request, CancellationToken cancel = default);
        Task<GroupMemberResponse> RevokeMemberRoleAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);
        Task<GroupMemberListResponse> GetAdminsAndModeratorsAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);


        Task<Group> GetByIdAsync(string id);
        Task<Group> GetBySlugAsync(string slug);
        Task<Group> CreateAsync(Group group);
        Task<Group> UpdateAsync(Group group);
        Task DeleteAsync(string id);

        Task<(List<Group> Items, int TotalCount)> SearchAsync(string? keyword, GroupPrivacy? privacy, string? language, GroupSortBy sortBy, int page, int pageSize);

        Task<(List<Group> Items, int TotalCount)> GetByMemberAsync(string userId, GroupMemberRole? filterByRole, int page, int pageSize);
        Task<bool> ExistsAsync(string id);
        Task<bool> SlugExistsAsync(string slug);

    }
}
