using Kpett.ChatApp.DTOs.Request.Group;
using Kpett.ChatApp.DTOs.Response.Group;

namespace Kpett.ChatApp.Services.Abstractions
{
    /// <summary>
    /// Service quản lý thao tác thành viên nhóm: tham gia, rời, mời, duyệt, kick, block, phân quyền.
    /// </summary>
    public interface IGroupMemberService
    {
        /// <summary>Lấy danh sách thành viên bị chặn.</summary>
        Task<GroupMemberListResponse> GetBlockedMembersAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>Bỏ chặn thành viên.</summary>
        Task<GroupMembershipActionResponse> UnblockMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);
        /// <summary>
        /// Người dùng tham gia nhóm (hoặc gửi yêu cầu nếu nhóm yêu cầu phê duyệt).
        /// </summary>
        Task<GroupMembershipActionResponse> JoinGroupAsync(string userId, string groupId, CancellationToken cancel = default);

        /// <summary>
        /// Người dùng rời khỏi nhóm.
        /// </summary>
        Task<GroupMembershipActionResponse> LeaveGroupAsync(string userId, string groupId, CancellationToken cancel = default);

		/// <summary>
		/// Mời danh sách bạn bè vào nhóm.
		/// <list type="bullet">
		///   <item>Chỉ mời được bạn bè đang active.</item>
		///   <item>Kiểm tra quyền theo WhoCanInvite (anyone / admin_mod / admin_only).</item>
		///   <item>Các userId không hợp lệ được bỏ qua kèm lý do trong Skipped.</item>
		///   <item>Tối đa 100 người một lần gọi.</item>
		/// </list>
		/// </summary>
		/// <exception cref="NotFoundException">Group không tồn tại hoặc đã bị xóa.</exception>
		/// <exception cref="ForbiddenException">Người gọi không phải active member hoặc không có quyền mời.</exception>
		/// <exception cref="BadRequestException">Thiếu request body hoặc danh sách userIds rỗng.</exception>
		Task<GroupInviteMembersResponse> InviteMembersAsync(string userId, string groupId, InviteGroupMembersRequest request, CancellationToken cancel = default);

        /// <summary>
        /// Chấp nhận yêu cầu tham gia nhóm từ người dùng khác.
        /// </summary>
        Task<GroupMemberResponse> AcceptJoinRequestAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>
        /// Từ chối yêu cầu tham gia nhóm.
        /// </summary>
        Task<GroupMembershipActionResponse> DeclineJoinRequestAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>
        /// Lấy danh sách thành viên đang hoạt động của nhóm.
        /// </summary>
        Task<GroupMemberListResponse> GetGroupMembersAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>
        /// Lấy danh sách yêu cầu tham gia đang chờ duyệt.
        /// </summary>
        Task<GroupMemberListResponse> GetPendingJoinRequestsAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);

        /// <summary>
        /// Kick thành viên khỏi nhóm.
        /// </summary>
        Task<GroupMembershipActionResponse> KickMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>
        /// Chặn thành viên khỏi nhóm.
        /// </summary>
        Task<GroupMembershipActionResponse> BlockMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>
        /// Cập nhật vai trò của thành viên trong nhóm.
        /// </summary>
        Task<GroupMemberResponse> UpdateMemberRoleAsync(string userId, string groupId, string targetUserId, UpdateGroupMemberRoleRequest request, CancellationToken cancel = default);

        /// <summary>
        /// Thu hồi vai trò của thành viên (đặt về member).
        /// </summary>
        Task<GroupMemberResponse> RevokeMemberRoleAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default);

        /// <summary>
        /// Lấy danh sách admin và moderator của nhóm.
        /// </summary>
        Task<GroupMemberListResponse> GetAdminsAndModeratorsAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default);
    }
}


