using Kpett.ChatApp.DTOs.Request.Group;
using Kpett.ChatApp.DTOs.Response.Group;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class GroupsController : ControllerBase
    {
        private readonly IGroupsService _groupsService;

        public GroupsController(IGroupsService groupsService)
        {
            _groupsService = groupsService;
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
