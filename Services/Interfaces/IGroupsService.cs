using Kpett.ChatApp.DTOs.Request.Group;
using Kpett.ChatApp.DTOs.Response.Group;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Models;

namespace Kpett.ChatApp.Services.Interfaces
{
    /// <summary>
    /// Service quản lý nhóm: CRUD nhóm, cài đặt, tìm kiếm và uỷ quyền thao tác thành viên.
    /// </summary>
    public interface IGroupsService
    {
        /// <summary>Tạo nhóm mới, chủ sở hữu mặc định là người tạo.</summary>
        Task<CreateGroupResponse> CreateGroupAsync(string userId, CreateGroupRequest request, CancellationToken cancel = default);

        /// <summary>Cập nhật thông tin nhóm (tên, mô tả, ảnh, quyền riêng tư, ngôn ngữ, nội quy).</summary>
        Task<GroupDetailResponse> UpdateGroupAsync(string userId, string groupId, UpdateGroupRequest request, CancellationToken cancel = default);

        /// <summary>Xoá mềm nhóm (chuyển trạng thái deleted).</summary>
        Task DeleteGroupAsync(string userId, string groupId, DeleteGroupRequest? request = null, CancellationToken cancel = default);

        /// <summary>Lấy chi tiết nhóm theo ID.</summary>
        Task<GroupDetailResponse> GetGroupByIdAsync(string userId, string groupId, CancellationToken cancel = default);

        /// <summary>Lấy chi tiết nhóm theo slug.</summary>
        Task<GroupDetailResponse> GetGroupBySlugAsync(string userId, string slug, CancellationToken cancel = default);

        /// <summary>Tìm kiếm nhóm theo từ khoá, loại quyền riêng tư, ngôn ngữ (phân trang).</summary>
        Task<SearchGroupResponse> SearchGroupsAsync(string userId, SearchGroupRequest request, CancellationToken cancel = default);

        /// <summary>Lấy danh sách nhóm của người dùng (phân trang, lọc theo vai trò).</summary>
        Task<MyGroupsResponse> GetMyGroupsAsync(string userId, MyGroupsRequest request, CancellationToken cancel = default);

        /// <summary>Lấy cài đặt nhóm.</summary>
        Task<GroupSettingsResponse> GetGroupSettingsAsync(string userId, string groupId, CancellationToken cancel = default);

        /// <summary>Cập nhật cài đặt nhóm (quyền riêng tư, ai có thể đăng/mời, phê duyệt, ngôn ngữ, nội quy).</summary>
        Task<GroupSettingsResponse> UpdateGroupSettingsAsync(string userId, string groupId, UpdateGroupSettingsRequest request, CancellationToken cancel = default);

        /// <summary>Cập nhật nội quy nhóm.</summary>
        Task<GroupSettingsResponse> UpdateGroupRulesAsync(string userId, string groupId, UpdateGroupRulesRequest request, CancellationToken cancel = default);

        // ─── Member operations (delegated to IGroupMemberService) ───

		/// <summary>
		/// Người dùng tham gia nhóm.
		/// <list type="bullet">
		///   <item>Nếu nhóm public và không yêu cầu phê duyệt: vào ngay, status = active.</item>
		///   <item>Nếu nhóm private/hidden hoặc memberApproval = true: tạo yêu cầu, status = pending.</item>
		///   <item>Nếu có invitation pending: tự động chấp nhận và vào nhóm.</item>
		/// </list>
		/// </summary>
		Task<GroupMembershipActionResponse> JoinGroupAsync(string userId, string groupId, CancellationToken cancel = default);

		/// <summary>
		/// Người dùng rời nhóm. Owner không thể rời nhóm bằng endpoint này.
		/// </summary>
		Task<GroupMembershipActionResponse> LeaveGroupAsync(string userId, string groupId, CancellationToken cancel = default);

		/// <summary>
		/// Mời danh sách bạn bè vào nhóm.
		/// <list type="bullet">
		///   <item>Chỉ mời được bạn bè đang active.</item>
		///   <item>Kiểm tra quyền theo WhoCanInvite trong group settings (anyone / admin_mod / admin_only).</item>
		///   <item>Các trường hợp không hợp lệ được liệt kê trong Skipped kèm lý do.</item>
		///   <item>Tối đa 100 người một lần mời.</item>
		/// </list>
		/// </summary>
		Task<GroupInviteMembersResponse> InviteMembersAsync(string userId, string groupId, InviteGroupMembersRequest request, CancellationToken cancel = default);

        /// <summary>Chấp nhận yêu cầu tham gia nhóm.</summary>
        Task<GroupMemberResponse> AcceptJoinRequestAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>Từ chối yêu cầu tham gia nhóm.</summary>
        Task<GroupMembershipActionResponse> DeclineJoinRequestAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>Lấy danh sách thành viên đang hoạt động của nhóm.</summary>
        Task<GroupMemberListResponse> GetGroupMembersAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>Lấy danh sách yêu cầu tham gia đang chờ.</summary>
        Task<GroupMemberListResponse> GetPendingJoinRequestsAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>Kick thành viên khỏi nhóm.</summary>
        Task<GroupMembershipActionResponse> KickMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>Chặn thành viên khỏi nhóm.</summary>
        Task<GroupMembershipActionResponse> BlockMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>Cập nhật vai trò thành viên.</summary>
        Task<GroupMemberResponse> UpdateMemberRoleAsync(string userId, string groupId, string targetUserId, UpdateGroupMemberRoleRequest request, CancellationToken cancel = default);

        /// <summary>Thu hồi vai trò thành viên (đặt về member).</summary>
        Task<GroupMemberResponse> RevokeMemberRoleAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>Lấy danh sách admin và moderator của nhóm.</summary>
        Task<GroupMemberListResponse> GetAdminsAndModeratorsAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>Lấy danh sách thành viên bị chặn.</summary>
        Task<GroupMemberListResponse> GetBlockedMembersAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>Bỏ chặn thành viên.</summary>
        Task<GroupMembershipActionResponse> UnblockMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>Chuyển quyền sở hữu nhóm.</summary>
        Task<GroupMemberResponse> TransferOwnershipAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

		/// <summary>
		/// Lấy danh sách lời mời tham gia nhóm dành cho người dùng hiện tại.
		/// Chỉ trả về các lời mời đang ở trạng thái pending.
		/// </summary>
		Task<List<GroupInvitationResponse>> GetMyInvitationsAsync(string userId, CancellationToken cancel = default);

		/// <summary>
		/// Chấp nhận lời mời tham gia nhóm.
		/// <list type="bullet">
		///   <item>Chỉ người được mời mới có thể chấp nhận.</item>
		///   <item>Nếu đã có GroupMember cũ (left/kicked): phục hồi với status = active.</item>
		///   <item>Nếu chưa có: tạo mới GroupMember với status = active.</item>
		///   <item>Invitation được đánh dấu accepted.</item>
		/// </list>
		/// </summary>
		Task<GroupMembershipActionResponse> AcceptInvitationAsync(string userId, string invitationId, CancellationToken cancel = default);

		/// <summary>
		/// Từ chối lời mời tham gia nhóm.
		/// Chỉ người được mời mới có thể từ chối. Invitation được đánh dấu declined.
		/// </summary>
		Task<GroupMembershipActionResponse> DeclineInvitationAsync(string userId, string invitationId, CancellationToken cancel = default);

        // ─── Internal CRUD ───

        /// <summary>Lấy entity Group theo ID (ném NotFoundException nếu không tìm thấy).</summary>
        Task<Group> GetByIdAsync(string id);

        /// <summary>Lấy entity Group theo slug (ném NotFoundException nếu không tìm thấy).</summary>
        Task<Group> GetBySlugAsync(string slug);

        /// <summary>Tạo entity Group mới.</summary>
        Task<Group> CreateAsync(Group group);

        /// <summary>Cập nhật entity Group.</summary>
        Task<Group> UpdateAsync(Group group);

        /// <summary>Xoá entity Group.</summary>
        Task DeleteAsync(string id);

        /// <summary>Tìm kiếm Group theo từ khoá, quyền riêng tư, ngôn ngữ (phân trang).</summary>
        Task<(List<Group> Items, int TotalCount)> SearchAsync(string? keyword, GroupPrivacy? privacy, string? language, GroupSortBy sortBy, int page, int pageSize);

        /// <summary>Lấy danh sách Group mà người dùng là thành viên.</summary>
        Task<(List<Group> Items, int TotalCount)> GetByMemberAsync(string userId, GroupMemberRole? filterByRole, int page, int pageSize);

        /// <summary>Kiểm tra Group có tồn tại theo ID.</summary>
        Task<bool> ExistsAsync(string id);

        /// <summary>Kiểm tra slug đã tồn tại chưa.</summary>
        Task<bool> SlugExistsAsync(string slug);
    }
}
