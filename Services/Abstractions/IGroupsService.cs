using Kpett.ChatApp.DTOs.Request.Group;
using Kpett.ChatApp.DTOs.Response.Group;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Models;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service qu?n l? nhóm: CRUD nhóm, cài ð?t, t?m ki?m và u? quy?n thao tác thành viên.
    /// </summary>
    public interface IGroupsService
    {
        /// <summary>T?o nhóm m?i, ch? s? h?u m?c ð?nh là ngý?i t?o.</summary>
        Task<CreateGroupResponse> CreateGroupAsync(string userId, CreateGroupRequest request, CancellationToken cancel = default);

        /// <summary>C?p nh?t thông tin nhóm (tên, mô t?, ?nh, quy?n riêng tý, ngôn ng?, n?i quy).</summary>
        Task<GroupDetailResponse> UpdateGroupAsync(string userId, string groupId, UpdateGroupRequest request, CancellationToken cancel = default);

        /// <summary>Xoá m?m nhóm (chuy?n tr?ng thái deleted).</summary>
        Task DeleteGroupAsync(string userId, string groupId, DeleteGroupRequest? request = null, CancellationToken cancel = default);

        /// <summary>L?y chi ti?t nhóm theo ID.</summary>
        Task<GroupDetailResponse> GetGroupByIdAsync(string userId, string groupId, CancellationToken cancel = default);

        /// <summary>L?y chi ti?t nhóm theo slug.</summary>
        Task<GroupDetailResponse> GetGroupBySlugAsync(string userId, string slug, CancellationToken cancel = default);

        /// <summary>T?m ki?m nhóm theo t? khoá, lo?i quy?n riêng tý, ngôn ng? (phân trang).</summary>
        Task<SearchGroupResponse> SearchGroupsAsync(string userId, SearchGroupRequest request, CancellationToken cancel = default);

        /// <summary>L?y danh sách nhóm c?a ngý?i dùng (phân trang, l?c theo vai tr?).</summary>
        Task<MyGroupsResponse> GetMyGroupsAsync(string userId, MyGroupsRequest request, CancellationToken cancel = default);

        /// <summary>L?y cài ð?t nhóm.</summary>
        Task<GroupSettingsResponse> GetGroupSettingsAsync(string userId, string groupId, CancellationToken cancel = default);

        /// <summary>C?p nh?t cài ð?t nhóm (quy?n riêng tý, ai có th? ðãng/m?i, phê duy?t, ngôn ng?, n?i quy).</summary>
        Task<GroupSettingsResponse> UpdateGroupSettingsAsync(string userId, string groupId, UpdateGroupSettingsRequest request, CancellationToken cancel = default);

        /// <summary>C?p nh?t n?i quy nhóm.</summary>
        Task<GroupSettingsResponse> UpdateGroupRulesAsync(string userId, string groupId, UpdateGroupRulesRequest request, CancellationToken cancel = default);

        // ??? Member operations (delegated to IGroupMemberService) ???

		/// <summary>
		/// Ngý?i dùng tham gia nhóm.
		/// <list type="bullet">
		///   <item>N?u nhóm public và không yêu c?u phê duy?t: vào ngay, status = active.</item>
		///   <item>N?u nhóm private/hidden ho?c memberApproval = true: t?o yêu c?u, status = pending.</item>
		///   <item>N?u có invitation pending: t? ð?ng ch?p nh?n và vào nhóm.</item>
		/// </list>
		/// </summary>
		Task<GroupMembershipActionResponse> JoinGroupAsync(string userId, string groupId, CancellationToken cancel = default);

		/// <summary>
		/// Ngý?i dùng r?i nhóm. Owner không th? r?i nhóm b?ng endpoint này.
		/// </summary>
		Task<GroupMembershipActionResponse> LeaveGroupAsync(string userId, string groupId, CancellationToken cancel = default);

		/// <summary>
		/// M?i danh sách b?n bè vào nhóm.
		/// <list type="bullet">
		///   <item>Ch? m?i ðý?c b?n bè ðang active.</item>
		///   <item>Ki?m tra quy?n theo WhoCanInvite trong group settings (anyone / admin_mod / admin_only).</item>
		///   <item>Các trý?ng h?p không h?p l? ðý?c li?t kê trong Skipped kèm l? do.</item>
		///   <item>T?i ða 100 ngý?i m?t l?n m?i.</item>
		/// </list>
		/// </summary>
		Task<GroupInviteMembersResponse> InviteMembersAsync(string userId, string groupId, InviteGroupMembersRequest request, CancellationToken cancel = default);

        /// <summary>Ch?p nh?n yêu c?u tham gia nhóm.</summary>
        Task<GroupMemberResponse> AcceptJoinRequestAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>T? ch?i yêu c?u tham gia nhóm.</summary>
        Task<GroupMembershipActionResponse> DeclineJoinRequestAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>L?y danh sách thành viên ðang ho?t ð?ng c?a nhóm.</summary>
        Task<GroupMemberListResponse> GetGroupMembersAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>L?y danh sách yêu c?u tham gia ðang ch?.</summary>
        Task<GroupMemberListResponse> GetPendingJoinRequestsAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>Kick thành viên kh?i nhóm.</summary>
        Task<GroupMembershipActionResponse> KickMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>Ch?n thành viên kh?i nhóm.</summary>
        Task<GroupMembershipActionResponse> BlockMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>C?p nh?t vai tr? thành viên.</summary>
        Task<GroupMemberResponse> UpdateMemberRoleAsync(string userId, string groupId, string targetUserId, UpdateGroupMemberRoleRequest request, CancellationToken cancel = default);

        /// <summary>Thu h?i vai tr? thành viên (ð?t v? member).</summary>
        Task<GroupMemberResponse> RevokeMemberRoleAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>L?y danh sách admin và moderator c?a nhóm.</summary>
        Task<GroupMemberListResponse> GetAdminsAndModeratorsAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>L?y danh sách thành viên b? ch?n.</summary>
        Task<GroupMemberListResponse> GetBlockedMembersAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>B? ch?n thành viên.</summary>
        Task<GroupMembershipActionResponse> UnblockMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>Chuy?n quy?n s? h?u nhóm.</summary>
        Task<GroupMemberResponse> TransferOwnershipAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

		/// <summary>
		/// L?y danh sách l?i m?i tham gia nhóm dành cho ngý?i dùng hi?n t?i.
		/// Ch? tr? v? các l?i m?i ðang ? tr?ng thái pending.
		/// </summary>
		Task<List<GroupInvitationResponse>> GetMyInvitationsAsync(string userId, CancellationToken cancel = default);

		/// <summary>
		/// Ch?p nh?n l?i m?i tham gia nhóm.
		/// <list type="bullet">
		///   <item>Ch? ngý?i ðý?c m?i m?i có th? ch?p nh?n.</item>
		///   <item>N?u ð? có GroupMember c? (left/kicked): ph?c h?i v?i status = active.</item>
		///   <item>N?u chýa có: t?o m?i GroupMember v?i status = active.</item>
		///   <item>Invitation ðý?c ðánh d?u accepted.</item>
		/// </list>
		/// </summary>
		Task<GroupMembershipActionResponse> AcceptInvitationAsync(string userId, string invitationId, CancellationToken cancel = default);

		/// <summary>
		/// T? ch?i l?i m?i tham gia nhóm.
		/// Ch? ngý?i ðý?c m?i m?i có th? t? ch?i. Invitation ðý?c ðánh d?u declined.
		/// </summary>
		Task<GroupMembershipActionResponse> DeclineInvitationAsync(string userId, string invitationId, CancellationToken cancel = default);

        // ??? Internal CRUD ???

        /// <summary>L?y entity Group theo ID (ném NotFoundException n?u không t?m th?y).</summary>
        Task<Group> GetByIdAsync(string id);

        /// <summary>L?y entity Group theo slug (ném NotFoundException n?u không t?m th?y).</summary>
        Task<Group> GetBySlugAsync(string slug);

        /// <summary>T?o entity Group m?i.</summary>
        Task<Group> CreateAsync(Group group);

        /// <summary>C?p nh?t entity Group.</summary>
        Task<Group> UpdateAsync(Group group);

        /// <summary>Xoá entity Group.</summary>
        Task DeleteAsync(string id);

        /// <summary>T?m ki?m Group theo t? khoá, quy?n riêng tý, ngôn ng? (phân trang).</summary>
        Task<(List<Group> Items, int TotalCount)> SearchAsync(string? keyword, GroupPrivacy? privacy, string? language, GroupSortBy sortBy, int page, int pageSize);

        /// <summary>L?y danh sách Group mà ngý?i dùng là thành viên.</summary>
        Task<(List<Group> Items, int TotalCount)> GetByMemberAsync(string userId, GroupMemberRole? filterByRole, int page, int pageSize);

        /// <summary>Ki?m tra Group có t?n t?i theo ID.</summary>
        Task<bool> ExistsAsync(string id);

        /// <summary>Ki?m tra slug ð? t?n t?i chýa.</summary>
        Task<bool> SlugExistsAsync(string slug);
    }
}


