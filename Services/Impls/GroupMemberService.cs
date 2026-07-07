using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Request.Group;
using Kpett.ChatApp.DTOs.Response.Group;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Services.Impls
{
    /// <summary>
    /// Service quản lý thao tác thành viên nhóm: tham gia, rời, mời, duyệt, kick, block, phân quyền.
    /// </summary>
    public class GroupMemberService : IGroupMemberService
    {
        private const string ActiveStatus = "active";
        private const string DeletedStatus = "deleted";
        private const string PendingStatus = "pending";
        private const string AcceptedStatus = "accepted";
        private const string DeclinedStatus = "declined";
        private const string LeftStatus = "left";
        private const string KickedStatus = "kicked";
        private const string BlockedStatus = "blocked";
        private const string AdminRole = "admin";
        private const string ModeratorRole = "moderator";
        private const string MemberRole = "member";
        private const string PublicPrivacy = "public";
        private const string PrivatePrivacy = "private";
        private const string HiddenPrivacy = "hidden";
        private const string AnyonePermission = "anyone";
        private const string AdminModPermission = "admin_mod";
        private const string AdminOnlyPermission = "admin_only";

        private readonly AppDbContext _dbContext;

        /// <summary>
        /// Khởi tạo service với database context.
        /// </summary>
        public GroupMemberService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <inheritdoc />
        public async Task<GroupMembershipActionResponse> JoinGroupAsync(
            string userId,
            string groupId,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await GetActiveGroupAsync(groupId, cancel);
            var now = DateTime.UtcNow;
            var existingMember = await _dbContext.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == group.Id && m.UserId == userId, cancel);

            if (existingMember?.Status == ActiveStatus)
                throw new ConflictException(ErrorCodes.GROUP.ALREADY_MEMBER, "You are already a member of this group.");

            if (existingMember?.Status == PendingStatus)
                throw new ConflictException(ErrorCodes.GROUP.JOIN_REQUEST_PENDING, "Your join request is already pending.");

            if (existingMember?.Status == BlockedStatus)
                throw new ForbiddenException(ErrorCodes.GROUP.MEMBER_BLOCKED, "You are blocked from this group.");

            var invitation = await _dbContext.GroupInvitations
                .FirstOrDefaultAsync(i =>
                    i.GroupId == group.Id &&
                    i.InviteeUserId == userId &&
                    i.Status == PendingStatus,
                    cancel);

            var requiresApproval = invitation == null && RequiresJoinApproval(group);
            var nextStatus = requiresApproval ? PendingStatus : ActiveStatus;

            if (existingMember == null)
            {
                existingMember = new GroupMember
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = group.Id,
                    UserId = userId,
                    CreatedAt = now,
                    CreatedByUserId = userId
                };
                _dbContext.GroupMembers.Add(existingMember);
            }

            existingMember.Status = nextStatus;
            existingMember.Role = MemberRole;
            existingMember.UpdatedAt = now;
            existingMember.UpdatedByUserId = userId;
            existingMember.JoinedAt = nextStatus == ActiveStatus ? now : null;

            if (invitation != null && nextStatus == ActiveStatus)
                invitation.Status = AcceptedStatus;

            await _dbContext.SaveChangesAsync(cancel);

            return BuildMembershipActionResponse(existingMember, requiresApproval);
        }

        /// <inheritdoc />
        public async Task<GroupMembershipActionResponse> LeaveGroupAsync(
            string userId,
            string groupId,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await GetActiveGroupAsync(groupId, cancel);
            var member = await GetMembershipAsync(group.Id, userId, cancel)
                ?? throw new NotFoundException(ErrorCodes.GROUP.MEMBER_NOT_FOUND, "Group membership not found.");

            if (member.Status != ActiveStatus && member.Status != PendingStatus)
                throw new NotFoundException(ErrorCodes.GROUP.MEMBER_NOT_FOUND, "Group membership not found.");

            if (group.OwnerUserId == userId && member.Status == ActiveStatus)
                throw new ForbiddenException(ErrorCodes.GROUP.OWNER_ACTION_INVALID, "Group owner cannot leave the group.");

            member.Status = LeftStatus;
            member.Role = MemberRole;
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedByUserId = userId;
            member.JoinedAt = null;

            await _dbContext.SaveChangesAsync(cancel);

            return BuildMembershipActionResponse(member, requiresApproval: false);
        }

        /// <inheritdoc />
        public async Task<GroupInviteMembersResponse> InviteMembersAsync(
            string userId,
            string groupId,
            InviteGroupMembersRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            var group = await GetActiveGroupAsync(groupId, cancel);
            var currentMember = await EnsureActiveMembershipAsync(group.Id, userId, cancel);
            EnsureCanInvite(group, currentMember);

            var inviteeIds = request.UserIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(100)
                .ToList();

            if (inviteeIds.Count == 0)
                throw new BadRequestException(ErrorCodes.GROUP.INVITE_INVALID, "At least one invitee user ID is required.");

            var response = new GroupInviteMembersResponse();
            var now = DateTime.UtcNow;
            var users = await _dbContext.Users
                .AsNoTracking()
                .Where(u => inviteeIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToHashSetAsync(cancel);

            var activeFriendIds = await GetActiveFriendIdsAsync(userId, inviteeIds, cancel);

            foreach (var inviteeId in inviteeIds)
            {
                if (inviteeId == userId)
                {
                    response.Skipped.Add(new GroupInviteSkippedResponse { UserId = inviteeId, Reason = "self" });
                    continue;
                }

                if (!users.Contains(inviteeId))
                {
                    response.Skipped.Add(new GroupInviteSkippedResponse { UserId = inviteeId, Reason = "user_not_found" });
                    continue;
                }

                if (!activeFriendIds.Contains(inviteeId))
                {
                    response.Skipped.Add(new GroupInviteSkippedResponse { UserId = inviteeId, Reason = "not_friend" });
                    continue;
                }

                var member = await GetMembershipAsync(group.Id, inviteeId, cancel);
                if (member?.Status == ActiveStatus)
                {
                    response.Skipped.Add(new GroupInviteSkippedResponse { UserId = inviteeId, Reason = "already_member" });
                    continue;
                }

                if (member?.Status == BlockedStatus)
                {
                    response.Skipped.Add(new GroupInviteSkippedResponse { UserId = inviteeId, Reason = "blocked" });
                    continue;
                }

                if (member?.Status == PendingStatus)
                {
                    response.Skipped.Add(new GroupInviteSkippedResponse { UserId = inviteeId, Reason = "join_request_pending" });
                    continue;
                }

                var invitation = await _dbContext.GroupInvitations
                    .FirstOrDefaultAsync(i => i.GroupId == group.Id && i.InviteeUserId == inviteeId, cancel);

                if (invitation?.Status == PendingStatus)
                {
                    response.Skipped.Add(new GroupInviteSkippedResponse { UserId = inviteeId, Reason = "invitation_pending" });
                    continue;
                }

                if (invitation == null)
                {
                    invitation = new GroupInvitation
                    {
                        Id = Guid.NewGuid().ToString(),
                        GroupId = group.Id,
                        InviteeUserId = inviteeId
                    };
                    _dbContext.GroupInvitations.Add(invitation);
                }

                invitation.InvitedByUserId = userId;
                invitation.Status = PendingStatus;
                invitation.CreatedAt = now;

                response.Invitations.Add(new GroupInvitationResponse
                {
                    Id = invitation.Id,
                    GroupId = invitation.GroupId,
                    InvitedByUserId = invitation.InvitedByUserId,
                    InviteeUserId = invitation.InviteeUserId,
                    Status = invitation.Status ?? PendingStatus,
                    CreatedAt = invitation.CreatedAt
                });
            }

            await _dbContext.SaveChangesAsync(cancel);

            return response;
        }

        /// <inheritdoc />
        public async Task<GroupMemberResponse> AcceptJoinRequestAsync(
            string userId,
            string groupId,
            string targetUserId,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureCanManageMembersAsync(group, userId, cancel);

            var member = await GetMembershipAsync(group.Id, targetUserId, cancel);
            if (member?.Status != PendingStatus)
                throw new NotFoundException(ErrorCodes.GROUP.JOIN_REQUEST_NOT_FOUND, "Pending join request not found.");

            member.Status = ActiveStatus;
            member.Role = MemberRole;
            member.JoinedAt = DateTime.UtcNow;
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedByUserId = userId;

            await _dbContext.SaveChangesAsync(cancel);

            return await BuildMemberResponseAsync(member, cancel);
        }

        /// <inheritdoc />
        public async Task<GroupMembershipActionResponse> DeclineJoinRequestAsync(
            string userId,
            string groupId,
            string targetUserId,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureCanManageMembersAsync(group, userId, cancel);

            var member = await GetMembershipAsync(group.Id, targetUserId, cancel);
            if (member?.Status != PendingStatus)
                throw new NotFoundException(ErrorCodes.GROUP.JOIN_REQUEST_NOT_FOUND, "Pending join request not found.");

            member.Status = DeclinedStatus;
            member.Role = MemberRole;
            member.JoinedAt = null;
            member.UpdatedAt = DateTime.UtcNow;
            member.UpdatedByUserId = userId;

            await _dbContext.SaveChangesAsync(cancel);

            return BuildMembershipActionResponse(member, requiresApproval: false);
        }

        /// <inheritdoc />
        public async Task<GroupMemberListResponse> GetGroupMembersAsync(
            string userId,
            string groupId,
            GroupMemberListRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureActiveMembershipAsync(group.Id, userId, cancel);

            return await BuildMemberListResponseAsync(group.Id, ActiveStatus, request, roles: null, cancel);
        }

        /// <inheritdoc />
        public async Task<GroupMemberListResponse> GetPendingJoinRequestsAsync(
            string userId,
            string groupId,
            GroupMemberListRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureCanManageMembersAsync(group, userId, cancel);

            return await BuildMemberListResponseAsync(group.Id, PendingStatus, request, roles: null, cancel);
        }

        /// <inheritdoc />
        public async Task<GroupMembershipActionResponse> KickMemberAsync(
            string userId,
            string groupId,
            string targetUserId,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await GetActiveGroupAsync(groupId, cancel);
            var currentMember = await EnsureCanManageMembersAsync(group, userId, cancel);
            var targetMember = await GetActiveTargetMemberAsync(group.Id, targetUserId, cancel);

            EnsureCanActOnTarget(group, currentMember, targetMember);

            targetMember.Status = KickedStatus;
            targetMember.Role = MemberRole;
            targetMember.JoinedAt = null;
            targetMember.UpdatedAt = DateTime.UtcNow;
            targetMember.UpdatedByUserId = userId;

            await _dbContext.SaveChangesAsync(cancel);

            return BuildMembershipActionResponse(targetMember, requiresApproval: false);
        }

        /// <inheritdoc />
        public async Task<GroupMembershipActionResponse> BlockMemberAsync(
            string userId,
            string groupId,
            string targetUserId,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await GetActiveGroupAsync(groupId, cancel);
            var currentMember = await EnsureCanManageMembersAsync(group, userId, cancel);

            if (userId == targetUserId)
                throw new BadRequestException(ErrorCodes.GROUP.SELF_ACTION_INVALID, "You cannot block yourself.");

            if (group.OwnerUserId == targetUserId)
                throw new ForbiddenException(ErrorCodes.GROUP.OWNER_ACTION_INVALID, "Group owner cannot be blocked.");

            var targetExists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == targetUserId, cancel);
            if (!targetExists)
                throw new NotFoundException(ErrorCodes.USER.NOT_FOUND, "User not found.");

            var targetMember = await GetMembershipAsync(group.Id, targetUserId, cancel);
            if (targetMember != null)
                EnsureCanActOnTarget(group, currentMember, targetMember);

            if (targetMember == null)
            {
                targetMember = new GroupMember
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = group.Id,
                    UserId = targetUserId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = userId
                };
                _dbContext.GroupMembers.Add(targetMember);
            }

            targetMember.Status = BlockedStatus;
            targetMember.Role = MemberRole;
            targetMember.JoinedAt = null;
            targetMember.UpdatedAt = DateTime.UtcNow;
            targetMember.UpdatedByUserId = userId;

            await _dbContext.SaveChangesAsync(cancel);

            return BuildMembershipActionResponse(targetMember, requiresApproval: false);
        }

        /// <inheritdoc />
        public async Task<GroupMemberResponse> UpdateMemberRoleAsync(
            string userId,
            string groupId,
            string targetUserId,
            UpdateGroupMemberRoleRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            var group = await GetActiveGroupAsync(groupId, cancel);
            var currentMember = await EnsureCanManageRolesAsync(group, userId, cancel);
            var targetMember = await GetActiveTargetMemberAsync(group.Id, targetUserId, cancel);
            var nextRole = NormalizeRoleForWrite(request.Role);

            EnsureCanActOnTarget(group, currentMember, targetMember);

            targetMember.Role = nextRole;
            targetMember.UpdatedAt = DateTime.UtcNow;
            targetMember.UpdatedByUserId = userId;

            await _dbContext.SaveChangesAsync(cancel);

            return await BuildMemberResponseAsync(targetMember, cancel);
        }

        /// <inheritdoc />
        public Task<GroupMemberResponse> RevokeMemberRoleAsync(
            string userId,
            string groupId,
            string targetUserId,
            CancellationToken cancel = default)
        {
            return UpdateMemberRoleAsync(
                userId,
                groupId,
                targetUserId,
                new UpdateGroupMemberRoleRequest { Role = MemberRole },
                cancel);
        }

        /// <inheritdoc />
        public async Task<GroupMemberListResponse> GetBlockedMembersAsync(
            string userId,
            string groupId,
            GroupMemberListRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureCanManageMembersAsync(group, userId, cancel);

            return await BuildMemberListResponseAsync(group.Id, BlockedStatus, request, roles: null, cancel);
        }

        /// <inheritdoc />
        public async Task<GroupMembershipActionResponse> UnblockMemberAsync(
            string userId,
            string groupId,
            string targetUserId,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureCanManageMembersAsync(group, userId, cancel);

            var targetMember = await GetMembershipAsync(group.Id, targetUserId, cancel);
            if (targetMember?.Status != BlockedStatus)
                throw new NotFoundException(ErrorCodes.GROUP.MEMBER_NOT_FOUND, "Blocked member not found.");

            if (targetMember.UserId == userId)
                throw new BadRequestException(ErrorCodes.GROUP.SELF_ACTION_INVALID, "You cannot unblock yourself.");

            targetMember.Status = LeftStatus;
            targetMember.Role = MemberRole;
            targetMember.JoinedAt = null;
            targetMember.UpdatedAt = DateTime.UtcNow;
            targetMember.UpdatedByUserId = userId;

            await _dbContext.SaveChangesAsync(cancel);

            return BuildMembershipActionResponse(targetMember, requiresApproval: false);
        }

        /// <inheritdoc />
        public async Task<GroupMemberListResponse> GetAdminsAndModeratorsAsync(
            string userId,
            string groupId,
            GroupMemberListRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureActiveMembershipAsync(group.Id, userId, cancel);

            return await BuildMemberListResponseAsync(
                group.Id,
                ActiveStatus,
                request,
                new[] { AdminRole, ModeratorRole },
                cancel);
        }

        // ──── Private helpers ────

        private async Task<GroupMember?> GetMembershipAsync(string groupId, string userId, CancellationToken cancel)
        {
            return await _dbContext.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, cancel);
        }

        private async Task<GroupMember> EnsureActiveMembershipAsync(string groupId, string userId, CancellationToken cancel)
        {
            var member = await GetMembershipAsync(groupId, userId, cancel);
            if (member?.Status != ActiveStatus)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_A_MEMBER, "You are not an active member of this group.");

            return member;
        }

        private async Task<GroupMember> EnsureCanManageMembersAsync(Group group, string userId, CancellationToken cancel)
        {
            var member = await EnsureActiveMembershipAsync(group.Id, userId, cancel);
            if (GetRoleRank(group, member) < 1)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group moderators or admins can manage group members.");

            return member;
        }

        private async Task<GroupMember> EnsureCanManageRolesAsync(Group group, string userId, CancellationToken cancel)
        {
            var member = await EnsureActiveMembershipAsync(group.Id, userId, cancel);
            if (GetRoleRank(group, member) < 2)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins can manage group roles.");

            return member;
        }

        private async Task<GroupMember> GetActiveTargetMemberAsync(string groupId, string targetUserId, CancellationToken cancel)
        {
            var targetMember = await GetMembershipAsync(groupId, targetUserId, cancel);
            if (targetMember?.Status != ActiveStatus)
                throw new NotFoundException(ErrorCodes.GROUP.MEMBER_NOT_FOUND, "Active group member not found.");

            return targetMember;
        }

        private void EnsureCanInvite(Group group, GroupMember currentMember)
        {
            var rank = GetRoleRank(group, currentMember);
            var invitePermission = NormalizePermissionOrDefault(group.WhoCanInvite);

            if (invitePermission == AdminOnlyPermission && rank < 2)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins can invite members.");

            if (invitePermission == AdminModPermission && rank < 1)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins or moderators can invite members.");
        }

        private void EnsureCanActOnTarget(Group group, GroupMember currentMember, GroupMember targetMember)
        {
            if (currentMember.UserId == targetMember.UserId)
                throw new BadRequestException(ErrorCodes.GROUP.SELF_ACTION_INVALID, "You cannot perform this action on yourself.");

            if (group.OwnerUserId == targetMember.UserId)
                throw new ForbiddenException(ErrorCodes.GROUP.OWNER_ACTION_INVALID, "Group owner cannot be modified by this action.");

            if (GetRoleRank(group, currentMember) <= GetRoleRank(group, targetMember))
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "You cannot modify a member with equal or higher role.");
        }

        private async Task<HashSet<string>> GetActiveFriendIdsAsync(
            string userId,
            IReadOnlyCollection<string> candidateUserIds,
            CancellationToken cancel)
        {
            var activeStatus = FriendshipStatus.Active.GetDescription();

            return await _dbContext.Friendships
                .AsNoTracking()
                .Where(f =>
                    f.Status == activeStatus &&
                    ((f.UserLowId == userId && candidateUserIds.Contains(f.UserHighId)) ||
                     (f.UserHighId == userId && candidateUserIds.Contains(f.UserLowId))))
                .Select(f => f.UserLowId == userId ? f.UserHighId : f.UserLowId)
                .ToHashSetAsync(cancel);
        }

        private async Task<GroupMemberResponse> BuildMemberResponseAsync(GroupMember member, CancellationToken cancel)
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

        private async Task<GroupMemberListResponse> BuildMemberListResponseAsync(
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
                    Role = x.member.Role ?? MemberRole,
                    Status = x.member.Status ?? ActiveStatus,
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

        private static GroupMembershipActionResponse BuildMembershipActionResponse(
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

        private static bool RequiresJoinApproval(Group group)
        {
            return group.MemberApproval || NormalizePrivacyOrDefault(group.Type) is PrivatePrivacy or HiddenPrivacy;
        }

        private static int GetRoleRank(Group group, GroupMember member)
        {
            if (group.OwnerUserId == member.UserId)
                return 3;

            return NormalizeRoleOrDefault(member.Role) switch
            {
                AdminRole => 2,
                ModeratorRole => 1,
                _ => 0
            };
        }

        private static void EnsureUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new BadRequestException(ErrorCodes.GROUP.USER_ID_REQUIRED, "User ID cannot be empty.");
        }

        private static void EnsureRequest<T>(T? request) where T : class
        {
            if (request == null)
                throw new BadRequestException(ErrorCodes.VALIDATION.REQUIRED, "Request body is required.");
        }

        private async Task<Group> GetActiveGroupAsync(string groupId, CancellationToken cancel)
        {
            if (string.IsNullOrWhiteSpace(groupId))
                throw new NotFoundException(ErrorCodes.GROUP.NOT_FOUND, "Group not found.");

            return await _dbContext.Groups
                .FirstOrDefaultAsync(g => g.Id == groupId && g.Status != DeletedStatus, cancel)
                ?? throw new NotFoundException(ErrorCodes.GROUP.NOT_FOUND, "Group not found.");
        }

        private static string NormalizePrivacyOrDefault(string? privacy)
        {
            if (string.IsNullOrWhiteSpace(privacy))
                return PublicPrivacy;

            return privacy.Trim().ToLower() switch
            {
                PrivatePrivacy => PrivatePrivacy,
                HiddenPrivacy => HiddenPrivacy,
                _ => PublicPrivacy
            };
        }

        private static string NormalizePermissionOrDefault(string? permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                return AnyonePermission;

            return permission.Trim().ToLower() switch
            {
                AdminModPermission or "admin_and_moderator" or "admins_and_moderators" or "moderators" => AdminModPermission,
                AdminOnlyPermission or "admin" or "admins" => AdminOnlyPermission,
                _ => AnyonePermission
            };
        }

        private static string NormalizeRoleForWrite(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                throw new BadRequestException(ErrorCodes.GROUP.ROLE_INVALID, "Role must be admin, moderator, or member.");

            return role.Trim().ToLower() switch
            {
                AdminRole => AdminRole,
                ModeratorRole => ModeratorRole,
                MemberRole => MemberRole,
                _ => throw new BadRequestException(ErrorCodes.GROUP.ROLE_INVALID, "Role must be admin, moderator, or member.")
            };
        }

        private static string NormalizeRoleOrDefault(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return MemberRole;

            return role.Trim().ToLower() switch
            {
                AdminRole => AdminRole,
                ModeratorRole => ModeratorRole,
                _ => MemberRole
            };
        }

        private static string NormalizeMemberStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return ActiveStatus;

            return status.Trim().ToLower() switch
            {
                ActiveStatus => ActiveStatus,
                PendingStatus => PendingStatus,
                AcceptedStatus => AcceptedStatus,
                DeclinedStatus => DeclinedStatus,
                LeftStatus => LeftStatus,
                KickedStatus => KickedStatus,
                BlockedStatus => BlockedStatus,
                _ => status.Trim().ToLower()
            };
        }

        private static bool IsAdmin(string? role)
        {
            return string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetRolePriority(string? role)
        {
            if (role == AdminRole) return 0;
            if (role == ModeratorRole) return 1;
            return 2;
        }
    }
}
