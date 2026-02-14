namespace Kpett.ChatApp.Contants
{
    public static class ErrorCodes
    {
        public static class USER
        {
            public const string NOT_FOUND = "USER.NOT_FOUND";
            public const string ALREADY_EXISTS = "USER.ALREADY_EXISTS";
            public const string INACTIVE = "USER.INACTIVE";
        }

        public static class AUTH
        {
            public const string INVALID_CREDENTIALS = "AUTH.INVALID_CREDENTIALS";
            public const string UNAUTHORIZED = "AUTH.UNAUTHORIZED";
            public const string FORBIDDEN = "AUTH.FORBIDDEN";
        }

        public static class FRIEND
        {
            public const string SELF_REFERENCE = "FRIEND.SELF_REFERENCE";
            public const string SENDER_NOT_FOUND = "FRIEND.SENDER_NOT_FOUND";
            public const string RECEIVER_NOT_FOUND = "FRIEND.RECEIVER_NOT_FOUND";
            public const string BLOCKED_RELATIONSHIP = "FRIEND.BLOCKED_RELATIONSHIP";
            public const string ALREADY_FRIENDS = "FRIEND.ALREADY_FRIENDS";
            public const string FRIEND_REQUEST_PENDING = "FRIEND.FRIEND_REQUEST_PENDING";
            public const string REQUEST_NOT_FOUND_OR_PROCESSED = "FRIEND.REQUEST_NOT_FOUND_OR_PROCESSED";
            public const string FRIEND_REQUEST_NOT_FOUND = "FRIEND.FRIEND_REQUEST_NOT_FOUND";
        }

        public static class CONVERSATION
        {
            public const string NOT_FOUND = "CONVERSATION.NOT_FOUND";
            public const string USER_NOT_IN_CONVERSATION = "CONVERSATION.USER_NOT_IN_CONVERSATION";
            public const string INVALID_MESSAGE = "CONVERSATION.INVALID_MESSAGE";
        }

        public static class POST
        {
            public const string NOT_FOUND = "POST.NOT_FOUND";
            public const string USER_NOT_AUTHORIZED = "POST.USER_NOT_AUTHORIZED";
            public const string PARENT_POST_NOT_FOUND = "POST.PARENT_POST_NOT_FOUND";
        }

        public static class COMMENT
        {
            public const string NOT_FOUND = "COMMENT.NOT_FOUND";
            public const string USER_NOT_AUTHORIZED = "COMMENT.USER_NOT_AUTHORIZED";
            public const string PARENT_COMMENT_NOT_FOUND = "COMMENT.PARENT_COMMENT_NOT_FOUND";
        }

        public static class SERVER
        {
            public const string SYSTEM_ERROR = "SERVER.SYSTEM_ERROR";
            public const string DATABASE_ERROR = "SERVER.DATABASE_ERROR";
        }

        public static class VALIDATION
        {
            public const string REQUIRED = "VAL.REQUIRED";
        }
    }
}
