using System.ComponentModel;

namespace Kpett.ChatApp.Enums
{
    public enum UserEnums
    {
        [Description("Online")]
        Online = 1,

        [Description("Offline")]
        Offline = 2,

        [Description("Busy")]
        Busy = 4,

        [Description("Away")]
        Away = 5
    }
    public enum UserGenderEnums
    {
        [Description("Male")]
        Online = 1,

        [Description("Female")]
        Offline = 2,

        [Description("Other")]
        Busy = 4,

        [Description("unKnow")]
        Away = 5
    }
}
