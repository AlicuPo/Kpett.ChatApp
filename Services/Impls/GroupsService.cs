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
    /// Service quản lý nhóm: CRUD nhóm, cài đặt, tìm kiếm (uỷ quyền thao tác thành viên cho <see cref="IGroupMemberService"/>).
    /// </summary>
    public class GroupsService : IGroupsService
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
        private const string DefaultLanguage = "vi";

        private readonly AppDbContext _dbContext;
        private readonly IGroupMemberService _groupMemberService;

        /// <summary>
        /// Khởi tạo service với database context và service thành viên nhóm.
        /// </summary>
        public GroupsService(AppDbContext dbContext, IGroupMemberService groupMemberService)
        {
            _dbContext = dbContext;
            _groupMemberService = groupMemberService;
        }

        /// <inheritdoc />
        public async Task<CreateGroupResponse> CreateGroupAsync(string userId, CreateGroupRequest request, CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            if (string.IsNullOrWhiteSpace(request.Name))
                throw new BadRequestException(ErrorCodes.GROUP.NAME_REQUIRED, "Group name is required.");

            var privacy = NormalizePrivacyForWrite(request.type, allowMissing: true);
            var now = DateTime.UtcNow;

            var newGroup = new Group
            {
                Id = Guid.NewGuid().ToString(),
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Type = privacy,
                AvatarUrl = request.AvatarUrl,
                CoverImageUrl = request.CoverImageUrl,
                PostApproval = false,
                MemberApproval = privacy is PrivatePrivacy or HiddenPrivacy,
                WhoCanPost = AnyonePermission,
                WhoCanInvite = AnyonePermission,
                Language = NormalizeLanguageForWrite(request.Language, allowMissing: true),
                CreatedAt = now,
                CreatedByUserId = userId,
                OwnerUserId = userId,
                Status = ActiveStatus
            };

            _dbContext.Groups.Add(newGroup);

            _dbContext.GroupMembers.Add(new GroupMember
            {
                Id = Guid.NewGuid().ToString(),
                GroupId = newGroup.Id,
                UserId = userId,
                Role = AdminRole,
                Status = ActiveStatus,
                JoinedAt = now,
                CreatedAt = now,
                CreatedByUserId = userId
            });

            var rules = BuildRuleEntitiesFromTitles(newGroup.Id, request.Rules);
            if (rules.Count > 0)
                _dbContext.GroupRules.AddRange(rules);

            await _dbContext.SaveChangesAsync(cancel);

            return new CreateGroupResponse
            {
                Id = newGroup.Id,
                Name = newGroup.Name,
                Slug = null,
                CreatedAt = newGroup.CreatedAt
            };
        }

        /// <inheritdoc />
        public async Task<GroupDetailResponse> UpdateGroupAsync(
            string userId,
            string groupId,
            UpdateGroupRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureGroupAdminAsync(group, userId, cancel);

            if (!string.IsNullOrWhiteSpace(request.Name))
                group.Name = request.Name.Trim();

            if (request.Description != null)
                group.Description = request.Description.Trim();

            if (!string.IsNullOrWhiteSpace(request.AvatarUrl))
                group.AvatarUrl = request.AvatarUrl.Trim();

            if (!string.IsNullOrWhiteSpace(request.CoverImageUrl))
                group.CoverImageUrl = request.CoverImageUrl.Trim();

            if (request.Privacy.HasValue)
                group.Type = MapPrivacy(request.Privacy.Value);

            if (request.Language != null)
                group.Language = NormalizeLanguageForWrite(request.Language, allowMissing: false);

            if (request.Rules != null)
                await ReplaceRulesAsync(group.Id, BuildRuleEntitiesFromTitles(group.Id, request.Rules), cancel);

            TouchGroup(group, userId);

            await _dbContext.SaveChangesAsync(cancel);

            return await BuildGroupDetailResponseAsync(group, userId, cancel);
        }

        /// <inheritdoc />
        public async Task DeleteGroupAsync(
            string userId,
            string groupId,
            DeleteGroupRequest? request = null,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await GetActiveGroupAsync(groupId, cancel);

            if (group.OwnerUserId != userId)
            {
                var member = await _dbContext.GroupMembers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m =>
                        m.GroupId == groupId &&
                        m.UserId == userId &&
                        m.Status == ActiveStatus,
                        cancel);

                if (!IsAdmin(member?.Role))
                    throw new ForbiddenException(ErrorCodes.GROUP.NOT_OWNER, "Only the group owner or an admin can delete this group.");
            }

            group.Status = DeletedStatus;
            TouchGroup(group, userId);

            await _dbContext.SaveChangesAsync(cancel);
        }

        /// <inheritdoc />
        public async Task<GroupDetailResponse> GetGroupByIdAsync(
            string userId,
            string groupId,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            var group = await GetActiveGroupAsync(groupId, cancel);
            return await BuildGroupDetailResponseAsync(group, userId, cancel);
        }

        /// <inheritdoc />
        public async Task<GroupDetailResponse> GetGroupBySlugAsync(
            string userId,
            string slug,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await _dbContext.Groups
                .FirstOrDefaultAsync(g => g.Id == slug && g.Status != DeletedStatus, cancel)
                ?? throw new NotFoundException(ErrorCodes.GROUP.NOT_FOUND, "Group not found.");

            return await BuildGroupDetailResponseAsync(group, userId, cancel);
        }

        /// <inheritdoc />
        public async Task<SearchGroupResponse> SearchGroupsAsync(
            string userId,
            SearchGroupRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var query = _dbContext.Groups
                .AsNoTracking()
                .Where(g => g.Status != DeletedStatus);

            if (!string.IsNullOrWhiteSpace(request.Keyword))
            {
                var keyword = request.Keyword.Trim().ToLower();
                query = query.Where(g =>
                    (g.Name != null && g.Name.ToLower().Contains(keyword)) ||
                    (g.Description != null && g.Description.ToLower().Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                var privacy = NormalizePrivacyForWrite(request.Type, allowMissing: false);
                query = query.Where(g => g.Type == privacy);
            }

            if (!string.IsNullOrWhiteSpace(request.Language))
            {
                var language = NormalizeLanguageForWrite(request.Language, allowMissing: false);
                query = query.Where(g => g.Language == language);
            }

            var total = await query.CountAsync(cancel);

            var groups = await query
                .OrderByDescending(g => g.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancel);

            var groupIds = groups.Select(g => g.Id).ToList();
            var memberMap = await _dbContext.GroupMembers
                .AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId) && m.Status == ActiveStatus)
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancel);

            var myMemberships = await _dbContext.GroupMembers
                .AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId) && m.UserId == userId && m.Status == ActiveStatus)
                .Select(m => m.GroupId)
                .ToHashSetAsync(cancel);

            var items = groups.Select(g => new GroupSummary
            {
                Id = g.Id,
                Name = g.Name,
                Slug = g.Id,
                AvatarUrl = g.AvatarUrl,
                Privacy = ParsePrivacy(g.Type),
                MemberCount = memberMap.TryGetValue(g.Id, out var count) ? count : 0,
                IsMember = myMemberships.Contains(g.Id)
            }).ToList();

            return new SearchGroupResponse
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<MyGroupsResponse> GetMyGroupsAsync(
            string userId,
            MyGroupsRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var memberQuery = _dbContext.GroupMembers
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.Status == ActiveStatus);

            if (!string.IsNullOrWhiteSpace(request.FilterByRole))
                memberQuery = memberQuery.Where(m => m.Role == request.FilterByRole.ToLower());

            var total = await memberQuery.CountAsync(cancel);

            var memberships = await memberQuery
                .OrderByDescending(m => m.JoinedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancel);

            var groupIds = memberships.Select(m => m.GroupId).ToList();
            var groups = await _dbContext.Groups
                .AsNoTracking()
                .Where(g => groupIds.Contains(g.Id) && g.Status != DeletedStatus)
                .ToListAsync(cancel);

            var groupDict = groups.ToDictionary(g => g.Id);
            var memberMap = await _dbContext.GroupMembers
                .AsNoTracking()
                .Where(m => groupIds.Contains(m.GroupId) && m.Status == ActiveStatus)
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count, cancel);

            var items = memberships
                .Where(m => groupDict.ContainsKey(m.GroupId))
                .Select(m =>
                {
                    var group = groupDict[m.GroupId];
                    return new MyGroupItem
                    {
                        Id = group.Id,
                        Name = group.Name,
                        Slug = group.Id,
                        AvatarUrl = group.AvatarUrl,
                        MyRole = ParseMemberRole(m.Role),
                        MemberCount = memberMap.TryGetValue(group.Id, out var count) ? count : 0,
                        UnreadPostCount = 0,
                        JoinedAt = m.JoinedAt ?? m.CreatedAt
                    };
                }).ToList();

            return new MyGroupsResponse
            {
                Items = items,
                TotalCount = total
            };
        }

        /// <inheritdoc />
        public async Task<GroupSettingsResponse> GetGroupSettingsAsync(
            string userId,
            string groupId,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureGroupAdminAsync(group, userId, cancel);

            return await BuildGroupSettingsResponseAsync(group, cancel);
        }

        /// <inheritdoc />
        public async Task<GroupSettingsResponse> UpdateGroupSettingsAsync(
            string userId,
            string groupId,
            UpdateGroupSettingsRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureGroupAdminAsync(group, userId, cancel);

            if (request.Privacy != null)
                group.Type = NormalizePrivacyForWrite(request.Privacy, allowMissing: false);

            if (request.WhoCanPost != null)
                group.WhoCanPost = NormalizePermissionForWrite(request.WhoCanPost);

            if (request.WhoCanInvite != null)
                group.WhoCanInvite = NormalizePermissionForWrite(request.WhoCanInvite);

            if (request.PostApproval.HasValue)
                group.PostApproval = request.PostApproval.Value;

            if (request.MemberApproval.HasValue)
                group.MemberApproval = request.MemberApproval.Value;

            if (request.Language != null)
                group.Language = NormalizeLanguageForWrite(request.Language, allowMissing: false);

            if (request.Rules != null)
                await ReplaceRulesAsync(group.Id, BuildRuleEntities(group.Id, request.Rules), cancel);

            TouchGroup(group, userId);

            await _dbContext.SaveChangesAsync(cancel);

            return await BuildGroupSettingsResponseAsync(group, cancel);
        }

        /// <inheritdoc />
        public async Task<GroupSettingsResponse> UpdateGroupRulesAsync(
            string userId,
            string groupId,
            UpdateGroupRulesRequest request,
            CancellationToken cancel = default)
        {
            EnsureUserId(userId);
            EnsureRequest(request);

            var group = await GetActiveGroupAsync(groupId, cancel);
            await EnsureGroupAdminAsync(group, userId, cancel);

            await ReplaceRulesAsync(group.Id, BuildRuleEntities(group.Id, request.Rules), cancel);
            TouchGroup(group, userId);

            await _dbContext.SaveChangesAsync(cancel);

            return await BuildGroupSettingsResponseAsync(group, cancel);
        }

        /// <inheritdoc />
        public Task<GroupMembershipActionResponse> JoinGroupAsync(string userId, string groupId, CancellationToken cancel = default)
            => _groupMemberService.JoinGroupAsync(userId, groupId, cancel);

        /// <inheritdoc />
        public Task<GroupMembershipActionResponse> LeaveGroupAsync(string userId, string groupId, CancellationToken cancel = default)
            => _groupMemberService.LeaveGroupAsync(userId, groupId, cancel);

        /// <inheritdoc />
        public Task<GroupInviteMembersResponse> InviteMembersAsync(string userId, string groupId, InviteGroupMembersRequest request, CancellationToken cancel = default)
            => _groupMemberService.InviteMembersAsync(userId, groupId, request, cancel);

        /// <inheritdoc />
        public Task<GroupMemberResponse> AcceptJoinRequestAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default)
            => _groupMemberService.AcceptJoinRequestAsync(userId, groupId, targetUserId, cancel);

        /// <inheritdoc />
        public Task<GroupMembershipActionResponse> DeclineJoinRequestAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default)
            => _groupMemberService.DeclineJoinRequestAsync(userId, groupId, targetUserId, cancel);

        /// <inheritdoc />
        public Task<GroupMemberListResponse> GetGroupMembersAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default)
            => _groupMemberService.GetGroupMembersAsync(userId, groupId, request, cancel);

        /// <inheritdoc />
        public Task<GroupMemberListResponse> GetPendingJoinRequestsAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default)
            => _groupMemberService.GetPendingJoinRequestsAsync(userId, groupId, request, cancel);

        /// <inheritdoc />
        public Task<GroupMembershipActionResponse> KickMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default)
            => _groupMemberService.KickMemberAsync(userId, groupId, targetUserId, cancel);

        /// <inheritdoc />
        public Task<GroupMembershipActionResponse> BlockMemberAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default)
            => _groupMemberService.BlockMemberAsync(userId, groupId, targetUserId, cancel);

        /// <inheritdoc />
        public Task<GroupMemberResponse> UpdateMemberRoleAsync(string userId, string groupId, string targetUserId, UpdateGroupMemberRoleRequest request, CancellationToken cancel = default)
            => _groupMemberService.UpdateMemberRoleAsync(userId, groupId, targetUserId, request, cancel);

        /// <inheritdoc />
        public Task<GroupMemberResponse> RevokeMemberRoleAsync(string userId, string groupId, string targetUserId, CancellationToken cancel = default)
            => _groupMemberService.RevokeMemberRoleAsync(userId, groupId, targetUserId, cancel);

        /// <inheritdoc />
        public Task<GroupMemberListResponse> GetAdminsAndModeratorsAsync(string userId, string groupId, GroupMemberListRequest request, CancellationToken cancel = default)
            => _groupMemberService.GetAdminsAndModeratorsAsync(userId, groupId, request, cancel);

        /// <inheritdoc />
        public async Task<Group> GetByIdAsync(string id)
            => await _dbContext.Groups.FindAsync(id)
               ?? throw new NotFoundException(ErrorCodes.GROUP.NOT_FOUND, "Group not found.");

        /// <inheritdoc />
        public async Task<Group> GetBySlugAsync(string slug)
            => await _dbContext.Groups.FirstOrDefaultAsync(g => g.Id == slug)
               ?? throw new NotFoundException(ErrorCodes.GROUP.NOT_FOUND, "Group not found.");

        /// <inheritdoc />
        public async Task<Group> CreateAsync(Group group)
        {
            _dbContext.Groups.Add(group);
            await _dbContext.SaveChangesAsync();
            return group;
        }

        /// <inheritdoc />
        public async Task<Group> UpdateAsync(Group group)
        {
            _dbContext.Groups.Update(group);
            await _dbContext.SaveChangesAsync();
            return group;
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string id)
        {
            var group = await _dbContext.Groups.FindAsync(id)
                        ?? throw new NotFoundException(ErrorCodes.GROUP.NOT_FOUND, "Group not found.");

            _dbContext.Groups.Remove(group);
            await _dbContext.SaveChangesAsync();
        }

        /// <inheritdoc />
        public async Task<(List<Group> Items, int TotalCount)> SearchAsync(
            string? keyword,
            GroupPrivacy? privacy,
            string? language,
            GroupSortBy sortBy,
            int page,
            int pageSize)
        {
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
            var query = _dbContext.Groups.AsNoTracking().Where(g => g.Status != DeletedStatus);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(g =>
                    (g.Name != null && g.Name.ToLower().Contains(kw)) ||
                    (g.Description != null && g.Description.ToLower().Contains(kw)));
            }

            if (privacy.HasValue)
                query = query.Where(g => g.Type == MapPrivacy(privacy.Value));

            if (!string.IsNullOrWhiteSpace(language))
            {
                var normalizedLanguage = NormalizeLanguageForWrite(language, allowMissing: false);
                query = query.Where(g => g.Language == normalizedLanguage);
            }

            var total = await query.CountAsync();

            query = sortBy switch
            {
                GroupSortBy.NewestCreated => query.OrderByDescending(g => g.CreatedAt),
                _ => query.OrderByDescending(g => g.CreatedAt)
            };

            var items = await query
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync();

            return (items, total);
        }

        /// <inheritdoc />
        public async Task<(List<Group> Items, int TotalCount)> GetByMemberAsync(
            string userId,
            GroupMemberRole? filterByRole,
            int page,
            int pageSize)
        {
            var normalizedPage = Math.Max(1, page);
            var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
            var memberQuery = _dbContext.GroupMembers
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.Status == ActiveStatus);

            if (filterByRole.HasValue)
                memberQuery = memberQuery.Where(m => m.Role == filterByRole.Value.ToString().ToLower());

            var total = await memberQuery.CountAsync();
            var memberships = await memberQuery
                .OrderByDescending(m => m.JoinedAt)
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync();

            var groupIds = memberships.Select(m => m.GroupId).ToList();
            var groups = await _dbContext.Groups
                .AsNoTracking()
                .Where(g => groupIds.Contains(g.Id) && g.Status != DeletedStatus)
                .ToListAsync();

            return (groups, total);
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string id)
            => await _dbContext.Groups.AnyAsync(g => g.Id == id);

        /// <inheritdoc />
        public async Task<bool> SlugExistsAsync(string slug)
            => await _dbContext.Groups.AnyAsync(g => g.Id == slug);

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

        private async Task EnsureGroupAdminAsync(Group group, string userId, CancellationToken cancel)
        {
            var member = await _dbContext.GroupMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.GroupId == group.Id &&
                    m.UserId == userId &&
                    m.Status == ActiveStatus,
                    cancel)
                ?? throw new ForbiddenException(ErrorCodes.GROUP.NOT_A_MEMBER, "You are not a member of this group.");

            if (!IsAdmin(member.Role) && group.OwnerUserId != userId)
                throw new ForbiddenException(ErrorCodes.GROUP.NOT_ADMIN, "Only group admins can update group settings.");
        }

        private async Task<GroupDetailResponse> BuildGroupDetailResponseAsync(
            Group group,
            string userId,
            CancellationToken cancel)
        {
            var memberCount = await _dbContext.GroupMembers
                .AsNoTracking()
                .CountAsync(m => m.GroupId == group.Id && m.Status == ActiveStatus, cancel);

            var myMembership = await _dbContext.GroupMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.GroupId == group.Id && m.UserId == userId && m.Status == ActiveStatus, cancel);

            return new GroupDetailResponse
            {
                Id = group.Id,
                Name = group.Name ?? string.Empty,
                AvatarUrl = group.AvatarUrl,
                CoverImageUrl = group.CoverImageUrl,
                Description = group.Description,
                Type = NormalizePrivacyOrDefault(group.Type),
                Language = NormalizeLanguageOrDefault(group.Language),
                WhoCanPost = NormalizePermissionOrDefault(group.WhoCanPost),
                WhoCanInvite = NormalizePermissionOrDefault(group.WhoCanInvite),
                PostApproval = group.PostApproval,
                MemberApproval = group.MemberApproval,
                Rules = await GetRuleResponsesAsync(group.Id, cancel),
                CreatedAt = group.CreatedAt,
                CreatedByUserId = group.CreatedByUserId,
                UpdatedAt = group.UpdatedAt,
                IsMember = myMembership != null,
                MyRole = myMembership?.Role,
                MemberCount = memberCount
            };
        }

        private async Task<GroupSettingsResponse> BuildGroupSettingsResponseAsync(
            Group group,
            CancellationToken cancel)
        {
            return new GroupSettingsResponse
            {
                GroupId = group.Id,
                Privacy = NormalizePrivacyOrDefault(group.Type),
                WhoCanPost = NormalizePermissionOrDefault(group.WhoCanPost),
                WhoCanInvite = NormalizePermissionOrDefault(group.WhoCanInvite),
                PostApproval = group.PostApproval,
                MemberApproval = group.MemberApproval,
                Language = NormalizeLanguageOrDefault(group.Language),
                Rules = await GetRuleResponsesAsync(group.Id, cancel),
                UpdatedAt = group.UpdatedAt
            };
        }

        private async Task<List<GroupRuleResponse>> GetRuleResponsesAsync(string groupId, CancellationToken cancel)
        {
            return await _dbContext.GroupRules
                .AsNoTracking()
                .Where(r => r.GroupId == groupId)
                .OrderBy(r => r.Order)
                .ThenBy(r => r.Id)
                .Select(r => new GroupRuleResponse
                {
                    Id = r.Id,
                    Title = r.Title ?? string.Empty,
                    Description = r.Description,
                    Order = r.Order
                })
                .ToListAsync(cancel);
        }

        private async Task ReplaceRulesAsync(string groupId, List<GroupRule> rules, CancellationToken cancel)
        {
            var existingRules = await _dbContext.GroupRules
                .Where(r => r.GroupId == groupId)
                .ToListAsync(cancel);

            if (existingRules.Count > 0)
                _dbContext.GroupRules.RemoveRange(existingRules);

            if (rules.Count > 0)
                _dbContext.GroupRules.AddRange(rules);
        }

        private static List<GroupRule> BuildRuleEntitiesFromTitles(string groupId, IEnumerable<string>? rules)
        {
            if (rules == null)
                return new List<GroupRule>();

            return rules
                .Select((rule, index) => new { Rule = rule?.Trim(), Index = index })
                .Where(x => !string.IsNullOrWhiteSpace(x.Rule))
                .Select(x => new GroupRule
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = groupId,
                    Title = x.Rule,
                    Description = null,
                    Order = x.Index + 1
                })
                .ToList();
        }

        private static List<GroupRule> BuildRuleEntities(string groupId, IReadOnlyList<UpsertGroupRuleRequest> rules)
        {
            var result = new List<GroupRule>();

            for (var index = 0; index < rules.Count; index++)
            {
                var rule = rules[index];
                var title = rule.Title?.Trim();

                if (string.IsNullOrWhiteSpace(title))
                    throw new BadRequestException(ErrorCodes.GROUP.RULE_INVALID, "Rule title is required.");

                result.Add(new GroupRule
                {
                    Id = Guid.NewGuid().ToString(),
                    GroupId = groupId,
                    Title = title,
                    Description = string.IsNullOrWhiteSpace(rule.Description) ? null : rule.Description.Trim(),
                    Order = rule.Order.HasValue && rule.Order.Value > 0 ? rule.Order.Value : index + 1
                });
            }

            return result;
        }

        private static void TouchGroup(Group group, string userId)
        {
            group.UpdatedAt = DateTime.UtcNow;
            group.UpdatedByUserId = userId;
        }

        private static string NormalizePrivacyForWrite(string? privacy, bool allowMissing)
        {
            if (string.IsNullOrWhiteSpace(privacy))
            {
                if (allowMissing)
                    return PublicPrivacy;

                throw new BadRequestException(ErrorCodes.GROUP.PRIVACY_INVALID, "Privacy must be public, private, or hidden.");
            }

            return privacy.Trim().ToLower() switch
            {
                "0" or PublicPrivacy => PublicPrivacy,
                "1" or PrivatePrivacy => PrivatePrivacy,
                "2" or HiddenPrivacy => HiddenPrivacy,
                _ => throw new BadRequestException(ErrorCodes.GROUP.PRIVACY_INVALID, "Privacy must be public, private, or hidden.")
            };
        }

        private static string NormalizePrivacyOrDefault(string? privacy)
        {
            if (string.IsNullOrWhiteSpace(privacy))
                return PublicPrivacy;

            return privacy.Trim().ToLower() switch
            {
                "0" or PublicPrivacy => PublicPrivacy,
                "1" or PrivatePrivacy => PrivatePrivacy,
                "2" or HiddenPrivacy => HiddenPrivacy,
                _ => PublicPrivacy
            };
        }

        private static string NormalizePermissionForWrite(string permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                throw new BadRequestException(ErrorCodes.GROUP.PERMISSION_INVALID, "Permission must be anyone, admin_mod, or admin_only.");

            return permission.Trim().ToLower() switch
            {
                AnyonePermission or "all" or "all_members" or "members" => AnyonePermission,
                AdminModPermission or "admin_and_moderator" or "admins_and_moderators" or "moderators" => AdminModPermission,
                AdminOnlyPermission or "admin" or "admins" => AdminOnlyPermission,
                _ => throw new BadRequestException(ErrorCodes.GROUP.PERMISSION_INVALID, "Permission must be anyone, admin_mod, or admin_only.")
            };
        }

        private static string NormalizePermissionOrDefault(string? permission)
        {
            if (string.IsNullOrWhiteSpace(permission))
                return AnyonePermission;

            return permission.Trim().ToLower() switch
            {
                AnyonePermission or "all" or "all_members" or "members" => AnyonePermission,
                AdminModPermission or "admin_and_moderator" or "admins_and_moderators" or "moderators" => AdminModPermission,
                AdminOnlyPermission or "admin" or "admins" => AdminOnlyPermission,
                _ => AnyonePermission
            };
        }

        private static string NormalizeLanguageForWrite(string? language, bool allowMissing)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                if (allowMissing)
                    return DefaultLanguage;

                throw new BadRequestException(ErrorCodes.GROUP.LANGUAGE_INVALID, "Language is required.");
            }

            var normalized = language.Trim().ToLower();
            if (normalized.Length is < 2 or > 20)
                throw new BadRequestException(ErrorCodes.GROUP.LANGUAGE_INVALID, "Language must be 2 to 20 characters.");

            return normalized;
        }

        private static string NormalizeLanguageOrDefault(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
                return DefaultLanguage;

            return language.Trim().ToLower();
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

        private static string MapPrivacy(GroupPrivacy privacy) => privacy switch
        {
            GroupPrivacy.Private => PrivatePrivacy,
            GroupPrivacy.Hidden => HiddenPrivacy,
            _ => PublicPrivacy
        };

        private static GroupPrivacy ParsePrivacy(string? type) => NormalizePrivacyOrDefault(type) switch
        {
            PrivatePrivacy => GroupPrivacy.Private,
            HiddenPrivacy => GroupPrivacy.Hidden,
            _ => GroupPrivacy.Public
        };

        private static GroupMemberRole ParseMemberRole(string? role) => role?.ToLower() switch
        {
            AdminRole => GroupMemberRole.Admin,
            ModeratorRole => GroupMemberRole.Moderator,
            _ => GroupMemberRole.Member
        };

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
