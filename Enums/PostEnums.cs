using System.ComponentModel;

namespace Kpett.ChatApp.Enums
{
    public class PostEnums
    {

    }
    public enum PostPrivacy
    {

        [Description("Public")]
        Public,
        [Description("Friends")]
        Friends,
        [Description("Private")]
        Private
    }

    public enum FeedSourceType
    {
        Friend = 0,
        Group = 1,
        Follow = 2
    }

    public enum PostReactionType
    {
        [Description("Like")]
        Like = 0,
        [Description("Love")]
        Love = 1,
        [Description("Haha")]
        Haha = 2,
        [Description("Wow")]
        Wow = 3,
        [Description("Sad")]
        Sad = 4,
        [Description("Angry")]
        Angry = 5
    }
    public enum PostMediaType
    {
        [Description("Image")]
        Image = 0,
        [Description("Video")]
        Video = 1,
        [Description("GIF")]
        GIF = 2,
    }

}
