using System.ComponentModel;

namespace Kpett.ChatApp.Enums
{
    public class GroupEnums
    {
    }
    public enum GroupPrivacy
    {
        [Description("public")]
        Public = 0,
        [Description("private")]
        Private = 1,
        [Description("hidden")]
        Hidden = 2
    }

    public enum GroupMemberRole
    {
        Member = 0,
        Moderator = 1,
        Admin = 2
    }
    public enum GroupSortBy
    {
        Relevance,
        NewestCreated,
        MostMembers,
        MostActive
    }

}
