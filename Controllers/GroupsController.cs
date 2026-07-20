using Kpett.ChatApp.DTOs.Request.Group;
using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Group;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Filters;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupsService _groupsService;
        private readonly IPostService _postService;

        /// <summary>Khởi tạo controller với các dependencies.</summary>
        public GroupsController(IGroupsService groupsService, IPostService postService)
        {
            _groupsService = groupsService;
            _postService = postService;
        }

        /// <summary>Lấy cài đặt nhóm (chỉ admin/owner mới xem được).</summary>
        [HttpGet("{groupId}/settings")]
        public async Task<ActionResult<GeneralResponse<GroupSettingsResponse>>> GetGroupSettings(
            string groupId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.GetGroupSettingsAsync(userId, groupId, cancel);

            return Ok(new GeneralResponse<GroupSettingsResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get group settings successfully.",
                Data = result
            });
        }

        /// <summary>Cập nhật cài đặt nhóm (chỉ admin/owner). Partial update — chỉ gửi field muốn thay đổi.</summary>
        [HttpPut("{groupId}/settings")]
        public async Task<ActionResult<GeneralResponse<GroupSettingsResponse>>> UpdateGroupSettings(
            string groupId,
            [FromBody] UpdateGroupSettingsRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.UpdateGroupSettingsAsync(userId, groupId, request, cancel);

            return Ok(new GeneralResponse<GroupSettingsResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Group settings updated successfully.",
                Data = result
            });
        }

        /// <summary>Cập nhật nội quy nhóm (thay thế toàn bộ danh sách rules hiện tại).</summary>
        [HttpPut("{groupId}/rules")]
        public async Task<ActionResult<GeneralResponse<GroupSettingsResponse>>> UpdateGroupRules(
            string groupId,
            [FromBody] UpdateGroupRulesRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.UpdateGroupRulesAsync(userId, groupId, request, cancel);

            return Ok(new GeneralResponse<GroupSettingsResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Group rules updated successfully.",
                Data = result
            });
        }

        /// <summary>Người dùng tham gia nhóm hoặc gửi yêu cầu nếu nhóm yêu cầu phê duyệt.</summary>
        [HttpPost("{groupId}/join")]
        public async Task<ActionResult<GeneralResponse<GroupMembershipActionResponse>>> JoinGroup(
            string groupId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.JoinGroupAsync(userId, groupId, cancel);

            return Ok(new GeneralResponse<GroupMembershipActionResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = result.RequiresApproval ? "Join request sent successfully." : "Joined group successfully.",
                Data = result
            });
        }

        /// <summary>Người dùng rời nhóm. Owner không thể rời nhóm qua endpoint này.</summary>
        [HttpPost("{groupId}/leave")]
        public async Task<ActionResult<GeneralResponse<GroupMembershipActionResponse>>> LeaveGroup(
            string groupId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.LeaveGroupAsync(userId, groupId, cancel);

            return Ok(new GeneralResponse<GroupMembershipActionResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Left group successfully.",
                Data = result
            });
        }

        /// <summary>
        /// Mời danh sách bạn bè vào nhóm.
        /// </summary>
        /// <param name="groupId">ID của nhóm.</param>
        /// <param name="request">Danh sách ID người dùng cần mời.</param>
        /// <param name="cancel">Token hủy bỏ.</param>
        /// <returns>Danh sách lời mời đã tạo và danh sách bỏ qua kèm lý do.</returns>
        /// <response code="200">Xử lý lời mời thành công.</response>
        /// <response code="400">Thiếu request body hoặc danh sách userIds rỗng.</response>
        /// <response code="401">Token không hợp lệ hoặc hết hạn.</response>
        /// <response code="403">Không phải active member hoặc không có quyền mời.</response>
        /// <response code="404">Không tìm thấy nhóm.</response>
        [HttpPost("{groupId}/invitations")]
        public async Task<ActionResult<GeneralResponse<GroupInviteMembersResponse>>> InviteMembers(
            string groupId,
            [FromBody] InviteGroupMembersRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.InviteMembersAsync(userId, groupId, request, cancel);

            return Ok(new GeneralResponse<GroupInviteMembersResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Group invitations processed successfully.",
                Data = result
            });
        }

        /// <summary>Chấp nhận yêu cầu tham gia nhóm (chỉ moderator/admin).</summary>
        [HttpPost("{groupId}/join-requests/{targetUserId}/accept")]
        public async Task<ActionResult<GeneralResponse<GroupMemberResponse>>> AcceptJoinRequest(
            string groupId,
            string targetUserId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.AcceptJoinRequestAsync(userId, groupId, targetUserId, cancel);

            return Ok(new GeneralResponse<GroupMemberResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Join request accepted successfully.",
                Data = result
            });
        }

        /// <summary>Từ chối yêu cầu tham gia nhóm (chỉ moderator/admin).</summary>
        [HttpPost("{groupId}/join-requests/{targetUserId}/decline")]
        public async Task<ActionResult<GeneralResponse<GroupMembershipActionResponse>>> DeclineJoinRequest(
            string groupId,
            string targetUserId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.DeclineJoinRequestAsync(userId, groupId, targetUserId, cancel);

            return Ok(new GeneralResponse<GroupMembershipActionResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Join request declined successfully.",
                Data = result
            });
        }

        /// <summary>Lấy danh sách thành viên đang hoạt động của nhóm (phân trang).</summary>
        [HttpGet("{groupId}/members")]
        public async Task<ActionResult<GeneralResponse<GroupMemberListResponse>>> GetGroupMembers(
            string groupId,
            [FromQuery] GroupMemberListRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.GetGroupMembersAsync(userId, groupId, request, cancel);

            return Ok(new GeneralResponse<GroupMemberListResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get group members successfully.",
                Data = result
            });
        }

        /// <summary>Lấy danh sách yêu cầu tham gia đang chờ duyệt (chỉ moderator/admin).</summary>
        [HttpGet("{groupId}/join-requests")]
        public async Task<ActionResult<GeneralResponse<GroupMemberListResponse>>> GetPendingJoinRequests(
            string groupId,
            [FromQuery] GroupMemberListRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.GetPendingJoinRequestsAsync(userId, groupId, request, cancel);

            return Ok(new GeneralResponse<GroupMemberListResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get pending join requests successfully.",
                Data = result
            });
        }

        /// <summary>Kick thành viên khỏi nhóm (chỉ moderator/admin).</summary>
        [HttpDelete("{groupId}/members/{targetUserId}")]
        public async Task<ActionResult<GeneralResponse<GroupMembershipActionResponse>>> KickMember(
            string groupId,
            string targetUserId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.KickMemberAsync(userId, groupId, targetUserId, cancel);

            return Ok(new GeneralResponse<GroupMembershipActionResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Group member removed successfully.",
                Data = result
            });
        }

        /// <summary>Chặn thành viên khỏi nhóm (chỉ moderator/admin).</summary>
        [HttpPost("{groupId}/members/{targetUserId}/block")]
        public async Task<ActionResult<GeneralResponse<GroupMembershipActionResponse>>> BlockMember(
            string groupId,
            string targetUserId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.BlockMemberAsync(userId, groupId, targetUserId, cancel);

            return Ok(new GeneralResponse<GroupMembershipActionResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Group member blocked successfully.",
                Data = result
            });
        }

        /// <summary>Cập nhật vai trò thành viên (chỉ admin).</summary>
        [HttpPut("{groupId}/members/{targetUserId}/role")]
        public async Task<ActionResult<GeneralResponse<GroupMemberResponse>>> UpdateMemberRole(
            string groupId,
            string targetUserId,
            [FromBody] UpdateGroupMemberRoleRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.UpdateMemberRoleAsync(userId, groupId, targetUserId, request, cancel);

            return Ok(new GeneralResponse<GroupMemberResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Group member role updated successfully.",
                Data = result
            });
        }

        /// <summary>Thu hồi vai trò thành viên (đặt về member, chỉ admin).</summary>
        [HttpDelete("{groupId}/members/{targetUserId}/role")]
        public async Task<ActionResult<GeneralResponse<GroupMemberResponse>>> RevokeMemberRole(
            string groupId,
            string targetUserId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.RevokeMemberRoleAsync(userId, groupId, targetUserId, cancel);

            return Ok(new GeneralResponse<GroupMemberResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Group member role revoked successfully.",
                Data = result
            });
        }

        /// <summary>Lấy danh sách admin và moderator của nhóm.</summary>
        [HttpGet("{groupId}/admins-moderators")]
        public async Task<ActionResult<GeneralResponse<GroupMemberListResponse>>> GetAdminsAndModerators(
            string groupId,
            [FromQuery] GroupMemberListRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.GetAdminsAndModeratorsAsync(userId, groupId, request, cancel);

            return Ok(new GeneralResponse<GroupMemberListResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get group admins and moderators successfully.",
                Data = result
            });
        }

        /// <summary>Lấy danh sách thành viên bị chặn (chỉ moderator/admin).</summary>
        [HttpGet("{groupId}/blocked-members")]
        public async Task<ActionResult<GeneralResponse<GroupMemberListResponse>>> GetBlockedMembers(
            string groupId,
            [FromQuery] GroupMemberListRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.GetBlockedMembersAsync(userId, groupId, request, cancel);

            return Ok(new GeneralResponse<GroupMemberListResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get blocked members successfully.",
                Data = result
            });
        }

        /// <summary>Bỏ chặn thành viên (chỉ moderator/admin).</summary>
        [HttpPost("{groupId}/members/{targetUserId}/unblock")]
        public async Task<ActionResult<GeneralResponse<GroupMembershipActionResponse>>> UnblockMember(
            string groupId,
            string targetUserId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.UnblockMemberAsync(userId, groupId, targetUserId, cancel);

            return Ok(new GeneralResponse<GroupMembershipActionResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Member unblocked successfully.",
                Data = result
            });
        }

        /// <summary>Chuyển quyền sở hữu nhóm (chỉ owner hiện tại).</summary>
        [HttpPost("{groupId}/transfer-ownership")]
        public async Task<ActionResult<GeneralResponse<GroupMemberResponse>>> TransferOwnership(
            string groupId,
            [FromBody] TransferOwnershipRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.TransferOwnershipAsync(userId, groupId, request.TargetUserId, cancel);

            return Ok(new GeneralResponse<GroupMemberResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Group ownership transferred successfully.",
                Data = result
            });
        }

        /// <summary>Ghim/bỏ ghim bài viết trong nhóm (chỉ admin/moderator).</summary>
        [HttpPost("{groupId}/posts/{postId}/pin")]
        public async Task<ActionResult<GeneralResponse<PostFeedResponse>>> TogglePinPost(
            string groupId,
            string postId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postService.TogglePinPostAsync(userId, groupId, postId, cancel);

            return Ok(new GeneralResponse<PostFeedResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Post pin status toggled successfully.",
                Data = result
            });
        }

        /// <summary>Lấy danh sách bài viết trong nhóm (có thể anonymous).</summary>
        [HttpGet("{groupId}/posts")]
        [AllowAnonymous]
        [OptionalAuthorize]
        public async Task<ActionResult<GeneralResponse<PaginatedData<PostFeedResponse>>>> GetGroupPosts(
            string groupId,
            [FromQuery] CursorPaginationRequest request,
            [FromQuery] string? status,
            CancellationToken cancel)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var result = await _postService.GetGroupPostsAsync(currentUserId, groupId, request, status, cancel);

            return Ok(new GeneralResponse<PaginatedData<PostFeedResponse>>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get group posts successfully.",
                Data = result
            });
        }

        /// <summary>Tạo bài viết trong nhóm. Nếu nhóm bật postApproval, bài viết ở trạng thái pending.</summary>
        [HttpPost("{groupId}/posts")]
        public async Task<ActionResult<GeneralResponse<PostFeedResponse>>> CreateGroupPost(
            string groupId,
            [FromBody] PostRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postService.CreateGroupPostAsync(userId, groupId, request, cancel);

            return StatusCode(StatusCodes.Status201Created, new GeneralResponse<PostFeedResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status201Created,
                Message = result.Status == "pending" ? "Group post submitted for approval." : "Group post created successfully.",
                Data = result
            });
        }

        /// <summary>Duyệt/từ chối bài viết trong nhóm (chỉ admin/moderator).</summary>
        [HttpPut("{groupId}/posts/{postId}/status")]
        public async Task<ActionResult<GeneralResponse<PostFeedResponse>>> UpdateGroupPostStatus(
            string groupId,
            string postId,
            [FromBody] UpdateGroupPostStatusRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _postService.UpdateGroupPostStatusAsync(userId, groupId, postId, request, cancel);

            return Ok(new GeneralResponse<PostFeedResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Group post status updated successfully.",
                Data = result
            });
        }

        /// <summary>
        /// Lấy danh sách lời mời tham gia nhóm dành cho người dùng hiện tại.
        /// </summary>
        /// <param name="cancel">Token hủy bỏ.</param>
        /// <returns>Danh sách lời mời đang chờ xử lý.</returns>
        /// <response code="200">Lấy danh sách lời mời thành công.</response>
        /// <response code="401">Token không hợp lệ hoặc hết hạn.</response>
        [HttpGet("invitations")]
        public async Task<ActionResult<GeneralResponse<List<GroupInvitationResponse>>>> GetMyInvitations(
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.GetMyInvitationsAsync(userId, cancel);

            return Ok(new GeneralResponse<List<GroupInvitationResponse>>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Get my invitations successfully.",
                Data = result
            });
        }

        /// <summary>
        /// Chấp nhận lời mời tham gia nhóm.
        /// </summary>
        /// <param name="invitationId">ID của lời mời.</param>
        /// <param name="cancel">Token hủy bỏ.</param>
        /// <returns>Thông tin tư cách thành viên sau khi chấp nhận.</returns>
        /// <response code="200">Chấp nhận lời mời thành công.</response>
        /// <response code="400">Lời mời không còn ở trạng thái chờ.</response>
        /// <response code="401">Token không hợp lệ hoặc hết hạn.</response>
        /// <response code="403">Lời mời không dành cho người dùng hiện tại hoặc đã bị chặn khỏi nhóm.</response>
        /// <response code="404">Không tìm thấy lời mời hoặc nhóm.</response>
        /// <response code="409">Người dùng đã là thành viên của nhóm.</response>
        [HttpPost("invitations/{invitationId}/accept")]
        public async Task<ActionResult<GeneralResponse<GroupMembershipActionResponse>>> AcceptInvitation(
            string invitationId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.AcceptInvitationAsync(userId, invitationId, cancel);

            return Ok(new GeneralResponse<GroupMembershipActionResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Invitation accepted successfully.",
                Data = result
            });
        }

        /// <summary>
        /// Từ chối lời mời tham gia nhóm.
        /// </summary>
        /// <param name="invitationId">ID của lời mời.</param>
        /// <param name="cancel">Token hủy bỏ.</param>
        /// <returns>Thông tin tư cách thành viên sau khi từ chối.</returns>
        /// <response code="200">Từ chối lời mời thành công.</response>
        /// <response code="400">Lời mời không còn ở trạng thái chờ.</response>
        /// <response code="401">Token không hợp lệ hoặc hết hạn.</response>
        /// <response code="403">Lời mời không dành cho người dùng hiện tại.</response>
        /// <response code="404">Không tìm thấy lời mời.</response>
        [HttpPost("invitations/{invitationId}/decline")]
        public async Task<ActionResult<GeneralResponse<GroupMembershipActionResponse>>> DeclineInvitation(
            string invitationId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.DeclineInvitationAsync(userId, invitationId, cancel);

            return Ok(new GeneralResponse<GroupMembershipActionResponse>
            {
                IsSuccess = true,
                StatusCode = StatusCodes.Status200OK,
                Message = "Invitation declined successfully.",
                Data = result
            });
        }

        /// <summary>Tạo nhóm mới. Người tạo tự động là admin.</summary>
        [HttpPost]
        public async Task<ActionResult<GeneralResponse<CreateGroupResponse>>> CreateGroup(
            [FromBody] CreateGroupRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.CreateGroupAsync(userId, request, cancel);

            return StatusCode(StatusCodes.Status201Created, new GeneralResponse<CreateGroupResponse>
            {
                IsSuccess  = true,
                StatusCode = StatusCodes.Status201Created,
                Message    = "Group created successfully.",
                Data       = result
            });
        }

        /// <summary>Cập nhật thông tin nhóm (chỉ admin).</summary>
        [HttpPut("{groupId}")]
        public async Task<ActionResult<GeneralResponse<GroupDetailResponse>>> UpdateGroup(
            string groupId,
            [FromBody] UpdateGroupRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.UpdateGroupAsync(userId, groupId, request, cancel);

            return Ok(new GeneralResponse<GroupDetailResponse>
            {
                IsSuccess  = true,
                StatusCode = StatusCodes.Status200OK,
                Message    = "Group updated successfully.",
                Data       = result
            });
        }

        /// <summary>Xóa mềm nhóm (chỉ owner/admin).</summary>
        [HttpDelete("{groupId}")]
        public async Task<ActionResult<GeneralResponse>> DeleteGroup(
            string groupId,
            [FromBody] DeleteGroupRequest? request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            await _groupsService.DeleteGroupAsync(userId, groupId, request, cancel);

            return Ok(new GeneralResponse
            {
                IsSuccess  = true,
                StatusCode = StatusCodes.Status200OK,
                Message    = "Group deleted successfully."
            });
        }

        /// <summary>Xem chi tiết nhóm theo ID.</summary>
        [HttpGet("{groupId}")]
        public async Task<ActionResult<GeneralResponse<GroupDetailResponse>>> GetGroupById(
            string groupId,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.GetGroupByIdAsync(userId, groupId, cancel);

            return Ok(new GeneralResponse<GroupDetailResponse>
            {
                IsSuccess  = true,
                StatusCode = StatusCodes.Status200OK,
                Message    = "Get group detail successfully.",
                Data       = result
            });
        }

        /// <summary>Xem chi tiết nhóm theo slug.</summary>
        [HttpGet("slug/{slug}")]
        public async Task<ActionResult<GeneralResponse<GroupDetailResponse>>> GetGroupBySlug(
            string slug,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.GetGroupBySlugAsync(userId, slug, cancel);

            return Ok(new GeneralResponse<GroupDetailResponse>
            {
                IsSuccess  = true,
                StatusCode = StatusCodes.Status200OK,
                Message    = "Get group detail successfully.",
                Data       = result
            });
        }

        /// <summary>Tìm kiếm nhóm theo từ khóa, loại quyền riêng tư (phân trang).</summary>
        [HttpGet("search")]
        public async Task<ActionResult<GeneralResponse<SearchGroupResponse>>> SearchGroups(
            [FromQuery] SearchGroupRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.SearchGroupsAsync(userId, request, cancel);

            return Ok(new GeneralResponse<SearchGroupResponse>
            {
                IsSuccess  = true,
                StatusCode = StatusCodes.Status200OK,
                Message    = "Search groups successfully.",
                Data       = result
            });
        }

        /// <summary>Lấy danh sách nhóm của tôi (phân trang, lọc theo vai trò).</summary>
        [HttpGet("me")]
        public async Task<ActionResult<GeneralResponse<MyGroupsResponse>>> GetMyGroups(
            [FromQuery] MyGroupsRequest request,
            CancellationToken cancel)
        {
            var userId = User.GetRequiredUserId();
            var result = await _groupsService.GetMyGroupsAsync(userId, request, cancel);

            return Ok(new GeneralResponse<MyGroupsResponse>
            {
                IsSuccess  = true,
                StatusCode = StatusCodes.Status200OK,
                Message    = "Get my groups successfully.",
                Data       = result
            });
        }
    }
}
