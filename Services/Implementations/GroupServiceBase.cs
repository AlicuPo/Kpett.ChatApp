using Kpett.ChatApp.Data;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Request.Group;
using Kpett.ChatApp.DTOs.Response.Group;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Models;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Implementations
{
    public abstract class GroupServiceBase
    {
        protected readonly AppDbContext _dbContext;

        protected GroupServiceBase(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // ─── Instance helpers ───

        protected async Task<GroupMember?> GetMembershipAsync(string groupId, string userId, CancellationToken cancel)
        {
            return await _dbContext.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, cancel);
        }

        protected async Task<GroupMember> EnsureActiveMembershipAsync(string groupId, string userId, CancellationToken cancel)
        {
            var member = await GetMembershipAsync(groupId, userId, cancel);
            if (member?.Status != GroupConstants.ActiveStatus)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_A_MEMBER, "You are not an active member of this group.");

            return member;
        }

        protected async Task<GroupMember> EnsureCanManageMembersAsync(Group group, string userId, CancellationToken cancel)
        {
            var member = await EnsureActiveMembershipAsync(group.Id, userId, cancel);
            if (GetRoleRank(group, member) < 1)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group moderators or admins can manage group members.");

            return member;
        }

        protected async Task<GroupMember> EnsureCanManageRolesAsync(Group group, string userId, CancellationToken cancel)
        {
            var member = await EnsureActiveMembershipAsync(group.Id, userId, cancel);
            if (GetRoleRank(group, member) < 2)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins can manage group roles.");

            return member;
        }

        protected async Task<GroupMember> GetActiveTargetMemberAsync(string groupId, string targetUserId, CancellationToken cancel)
        {
            var targetMember = await GetMembershipAsync(groupId, targetUserId, cancel);
            if (targetMember?.Status != GroupConstants.ActiveStatus)
                throw new NotFoundException(ErrorCodes.GROUP.MEMBER_NOT_FOUND, "Active group member not found.");

            return targetMember;
        }

        protected void EnsureCanInvite(Group group, GroupMember currentMember)
        {
            var rank = GetRoleRank(group, currentMember);
            var invitePermission = NormalizePermissionOrDefault(group.WhoCanInvite);

            if (invitePermission == GroupConstants.AdminOnlyPermission && rank < 2)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins can invite members.");

            if (invitePermission == GroupConstants.AdminModPermission && rank < 1)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins or moderators can invite members.");
        }

        protected void EnsureCanActOnTarget(Group group, GroupMember currentMember, GroupMember targetMember)
        {
            if (currentMember.UserId == targetMember.UserId)
                throw new BadRequestException(ErrorCodes.GROUP.SELF_ACTION_INVALID, "You cannot perform this action on yourself.");

            if (group.OwnerUserId == targetMember.UserId)
                throw new ForbiddenException(ErrorCodes.GROUP.OWNER_ACTION_INVALID, "Group owner cannot be modified by this action.");

            if (GetRoleRank(group, currentMember) <= GetRoleRank(group, targetMember))
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "You cannot modify a member with equal or higher role.");
        }

        protected async Task<HashSet<string>> GetActiveFriendIdsAsync(
            string userId,
            IReadOnlyCollection<string> candidateUserIds,
            CancellationToken cancel)
        {
            var activeStatus = Enums.FriendshipStatus.Active.GetDescription();

            return await _dbContext.Friendships
                .AsNoTracking()
                .Where(f =>
                    f.Status == activeStatus &&
                    ((f.UserLowId == userId && candidateUserIds.Contains(f.UserHighId)) ||
                     (f.UserHighId == userId && candidateUserIds.Contains(f.UserLowId))))
                .Select(f => f.UserLowId == userId ? f.UserHighId : f.UserLowId)
                .ToHashSetAsync(cancel);
        }

        protected async Task<Group> GetActiveGroupAsync(string groupId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                throw new NotFoundException(ErrorCodes.GROUP.NOT_FOUND, "Group not found.");

            return await _dbContext.Groups
                .FirstOrDefaultAsync(g => g.Id == groupId && g.Status != GroupConstants.DeletedStatus, cancel)
                ?? throw new NotFoundException(ErrorCodes.GROUP.NOT_FOUND, "Group not found.");
        }

        protected async Task<GroupMemberResponse> BuildMemberResponseAsync(GroupMember member, CancellationToken cancel)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == member.UserId, cancel)
                ?? throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found.");

            return new GroupMemberResponse
            {
                MemberId = member.Id,
                GroupId = member.GroupId,
                UserId = member.UserId,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                IsVerified = user.IsVerified,
                Role = NormalizeRoleOrDefault(member.Role),
                Status = NormalizeMemberStatus(member.Status),
                CreatedAt = member.CreatedAt,
                JoinedAt = member.JoinedAt,
                UpdatedAt = member.UpdatedAt
            };
        }

        protected async Task<GroupMemberListResponse> BuildMemberListResponseAsync(
            string groupId,
            string status,
            GroupMemberListRequest request,
            IReadOnlyCollection<string>? roles,
            CancellationToken cancel)
        {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var normalizedRole = string.IsNullOrWhiteSpace(request.Role)
                ? null
                : NormalizeRoleForWrite(request.Role);

            var query = from member in _dbContext.GroupMembers.AsNoTracking()
                        join user in _dbContext.Users.AsNoTracking()
                            on member.UserId equals user.Id
                        where member.GroupId == groupId && member.Status == status
                        select new { member, user };

            if (roles != null)
                query = query.Where(x => x.member.Role != null && roles.Contains(x.member.Role));

            if (normalizedRole != null)
                query = query.Where(x => x.member.Role == normalizedRole);

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var keyword = request.Keyword.Trim().ToLower();
                query = query.Where(x =>
                    (x.user.Username != null && x.user.Username.ToLower().Contains(keyword)) ||
                    (x.user.DisplayName != null && x.user.DisplayName.ToLower().Contains(keyword)) ||
                    x.user.Email.ToLower().Contains(keyword));
            }

            var total = await query.CountAsync(cancel);

            var items = await query
                .OrderBy(x => GetRolePriority(x.member.Role))
                .ThenByDescending(x => x.member.JoinedAt ?? x.member.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new GroupMemberResponse
                {
                    MemberId = x.member.Id,
                    GroupId = x.member.GroupId,
                    UserId = x.member.UserId,
                    Username = x.user.Username,
                    Email = x.user.Email,
                    DisplayName = x.user.DisplayName,
                    IsVerified = x.user.IsVerified,
                    Role = x.member.Role ?? GroupConstants.MemberRole,
                    Status = x.member.Status ?? GroupConstants.ActiveStatus,
                    CreatedAt = x.member.CreatedAt,
                    JoinedAt = x.member.JoinedAt,
                    UpdatedAt = x.member.UpdatedAt
                })
                .ToListAsync(cancel);

            return new GroupMemberListResponse
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        // ─── Static helpers ───

        protected static GroupMembershipActionResponse BuildMembershipActionResponse(
            GroupMember member,
            bool requiresApproval)
        {
            return new GroupMembershipActionResponse
            {
                GroupId = member.GroupId,
                UserId = member.UserId,
                Status = NormalizeMemberStatus(member.Status),
                Role = NormalizeRoleOrDefault(member.Role),
                RequiresApproval = requiresApproval,
                JoinedAt = member.JoinedAt
            };
        }

        protected static bool RequiresJoinApproval(Group group)
        {
            return group.MemberApproval || NormalizePrivacyOrDefault(group.Type) is GroupConstants.PrivatePrivacy or GroupConstants.HiddenPrivacy;
        }

        protected static int GetRoleRank(Group group, GroupMember member)
        {
            if (group.OwnerUserId == member.UserId)
                return 3;

            return NormalizeRoleOrDefault(member.Role) switch
            {
                GroupConstants.AdminRole => 2,
                GroupConstants.ModeratorRole => 1,
                _ => 0
            };
        }

        protected static void EnsureUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.GROUP.USER_ID_REQUIRED, "User ID cannot be empty.");
        }

        protected static void EnsureRequest<T>(T? request) where T : class
        {
            if (request == null)
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Request body is required.");
        }

        protected static string NormalizePrivacyOrDefault(string? privacy)
        {
            if (string.IsNullOrWhiteSpace(privacy))
                return GroupConstants.PublicPrivacy;

            return privacy.Trim().ToLower() switch
            {
                GroupConstants.PrivatePrivacy => GroupConstants.PrivatePrivacy,
                GroupConstants.HiddenPrivacy => GroupConstants.HiddenPrivacy,
                _ => GroupConstants.PublicPrivacy
            };
        }

        protected static string NormalizePermissionOrDefault(string? permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                return GroupConstants.AnyonePermission;

            return permission.Trim().ToLower() switch
            {
                GroupConstants.AdminModPermission or "admin_and_moderator" or "admins_and_moderators" or "moderators" => GroupConstants.AdminModPermission,
                GroupConstants.AdminOnlyPermission or "admin" or "admins" => GroupConstants.AdminOnlyPermission,
                _ => GroupConstants.AnyonePermission
            };
        }

        protected static string NormalizeRoleForWrite(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new BadRequestException(ErrorCodes.GROUP.ROLE_INVALID, "Role must be admin, moderator, or member.");

            return role.Trim().ToLower() switch
            {
                GroupConstants.AdminRole => GroupConstants.AdminRole,
                GroupConstants.ModeratorRole => GroupConstants.ModeratorRole,
                GroupConstants.MemberRole => GroupConstants.MemberRole,
                _ => throw new BadRequestException(ErrorCodes.GROUP.ROLE_INVALID, "Role must be admin, moderator, or member.")
            };
        }

        protected static string NormalizeRoleOrDefault(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return GroupConstants.MemberRole;

            return role.Trim().ToLower() switch
            {
                GroupConstants.AdminRole => GroupConstants.AdminRole,
                GroupConstants.ModeratorRole => GroupConstants.ModeratorRole,
                _ => GroupConstants.MemberRole
            };
        }

        protected static string NormalizeMemberStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return GroupConstants.ActiveStatus;

            return status.Trim().ToLower() switch
            {
                GroupConstants.ActiveStatus => GroupConstants.ActiveStatus,
                GroupConstants.PendingStatus => GroupConstants.PendingStatus,
                GroupConstants.AcceptedStatus => GroupConstants.AcceptedStatus,
                GroupConstants.DeclinedStatus => GroupConstants.DeclinedStatus,
                GroupConstants.LeftStatus => GroupConstants.LeftStatus,
                GroupConstants.KickedStatus => GroupConstants.KickedStatus,
                GroupConstants.BlockedStatus => GroupConstants.BlockedStatus,
                _ => status.Trim().ToLower()
            };
        }

        protected static bool IsAdmin(string? role)
        {
            return string.Equals(role, GroupConstants.AdminRole, StringComparison.OrdinalIgnoreCase);
        }

        protected static int GetRolePriority(string? role)
        {
            if (role == GroupConstants.AdminRole) return 0;
            if (role == GroupConstants.ModeratorRole) return 1;
            return 2;
        }
    }
}
