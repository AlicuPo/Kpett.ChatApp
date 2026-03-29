using System.ComponentModel;

namespace Kpett.ChatApp.Enums
{
    public enum MediaType
    {
        [Description("Image")]
        Image = 0,

        [Description("Video")]
        Video = 1,

        [Description("Document")]
        Document = 2,

        [Description("Unknown")]
        Unknown = 3
    }
}
