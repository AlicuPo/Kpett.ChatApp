using Kpett.ChatApp.DTOs.Request.Group;
using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Group;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Filters;
using Kpett.ChatApp.Helper;
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

        public GroupsController(IGroupsService groupsService, IPostService postService)
        {
            _groupsService = groupsService;
            _postService = postService;
        }

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

        // ──────────────────────────────────────────────
        //  POST api/groups
        //  Tạo nhóm mới
        // ──────────────────────────────────────────────
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

        // ──────────────────────────────────────────────
        //  PUT api/groups/{groupId}
        //  Cập nhật thông tin nhóm
        // ──────────────────────────────────────────────
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

        // ──────────────────────────────────────────────
        //  DELETE api/groups/{groupId}
        //  Xóa nhóm (soft delete)
        // ──────────────────────────────────────────────
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

        // ──────────────────────────────────────────────
        //  GET api/groups/{groupId}
        //  Xem chi tiết nhóm theo ID
        // ──────────────────────────────────────────────
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

        // ──────────────────────────────────────────────
        //  GET api/groups/slug/{slug}
        //  Xem chi tiết nhóm theo Slug
        // ──────────────────────────────────────────────
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

        // ──────────────────────────────────────────────
        //  GET api/groups/search?keyword=...&page=1&pageSize=20
        //  Tìm kiếm nhóm
        // ──────────────────────────────────────────────
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

        // ──────────────────────────────────────────────
        //  GET api/groups/me?filterByRole=admin&page=1&pageSize=20
        //  Danh sách nhóm của tôi
        // ──────────────────────────────────────────────
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
