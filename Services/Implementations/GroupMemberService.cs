using Kpett.ChatApp.Data;
using Kpett.ChatApp.Constants;
using Kpett.ChatApp.DTOs.Request.Group;
using Kpett.ChatApp.DTOs.Response.Group;
using Kpett.ChatApp.Events.Group;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using static Kpett.ChatApp.Constants.GroupConstants;

namespace Kpett.ChatApp.Services.Implementations
{
    /// <summary>
    /// Service quản lý thao tác thành viên nhóm: tham gia, rời, mời, duyệt, kick, block, phân quyền.
    /// </summary>
    public class GroupMemberService : GroupServiceBase, IGroupMemberService
    {
        private readonly IMediator _mediator;

        /// <summary>
        /// Khởi tạo service với database context.
        /// </summary>
        public GroupMemberService(AppDbContext dbContext, IMediator mediator) : base(dbContext)
        {
            _mediator = mediator;
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
            var sentInvitations = new List<GroupInvitation>();
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

                sentInvitations.Add(invitation);
            }

            await _dbContext.SaveChangesAsync(cancel);

            foreach (var inv in sentInvitations)
            {
                await _mediator.Publish(new GroupInvitationSentEvent
                {
                    InvitationId = inv.Id,
                    GroupId = group.Id,
                    GroupName = group.Name,
                    InviterId = userId,
                    InviteeId = inv.InviteeUserId
                }, cancel);
            }

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

    }
}
