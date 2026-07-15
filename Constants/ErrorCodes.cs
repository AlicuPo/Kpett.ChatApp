namespace Kpett.ChatApp.Constants
{
    public static class ErrorCodes
    {
        public static class USER
        {
            public const string NOT_FOUND = "USER.NOT_FOUND";
            public const string ALREADY_EXISTS = "USER.ALREADY_EXISTS";
            public const string ALREADY_EXISTS_BY_EMAIL = "USER.ALREADY_EXISTS_BY_EMAIL";
            public const string ALREADY_EXISTS_BY_USERNAME = "USER.ALREADY_EXISTS_BY_USERNAME";
            public const string INACTIVE = "USER.INACTIVE";
            public const string USERNAME_TAKEN = "USER.USERNAME_TAKEN";
        }

        public static class AUTH
        {
            public const string INVALID_CREDENTIALS = "AUTH.INVALID_CREDENTIALS";
            public const string UNAUTHORIZED = "AUTH.UNAUTHORIZED";
            public const string FORBIDDEN = "AUTH.FORBIDDEN";
            public const string ACCESS_TOKEN_INVALID = "AUTH.ACCESS_TOKEN_INVALID";
            public const string REFRESH_TOKEN_INVALID = "AUTH.REFRESH_TOKEN_INVALID";
            public const string PASSWORD_RESET_OTP_INVALID = "AUTH.PASSWORD_RESET_OTP_INVALID";
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
            public const string REQUEST_ALREADY_SENT = "FRIEND.REQUEST_ALREADY_SENT";
            public const string FRIENDSHIP_NOT_FOUND = "FRIEND.FRIENDSHIP_NOT_FOUND";
        }

        public static class FOLLOW
        {
            public const string SELF_REFERENCE = "FOLLOW.SELF_REFERENCE";
            public const string ALREADY_FOLLOWING = "FOLLOW.ALREADY_FOLLOWING";
            public const string FOLLOW_NOT_FOUND = "FOLLOW.FOLLOW_NOT_FOUND";
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
            public const string COMMENTS_DISABLED = "POST.COMMENTS_DISABLED";
        }

        public static class MEDIA
        {
            public const string FILE_EMPTY = "MEDIA.FILE_EMPTY";
            public const string FILE_SIZE_EXCEEDS_LIMIT = "MEDIA.FILE_SIZE_EXCEEDS_LIMIT";
            public const string INVALID_FILE_EXTENSION = "MEDIA.INVALID_FILE_EXTENSION";

            public const string TOO_MANY_FILES = "MEDIA.TOO_MANY_FILES";
            public const string ALL_FILES_FAILED = "MEDIA.ALL_FILES_FAILED";

            public const string UPLOAD_FAILED = "MEDIA.UPLOAD_FAILED";

            public const string PUBLIC_ID_REQUIRED = "MEDIA.PUBLIC_ID_REQUIRED";
            public const string NOT_FOUND = "MEDIA.NOT_FOUND";
            public const string DELETE_FAILED = "MEDIA.DELETE_FAILED";
        }

        public static class COMMENT
        {
            public const string NOT_FOUND = "COMMENT.NOT_FOUND";
            public const string USER_NOT_AUTHORIZED = "COMMENT.USER_NOT_AUTHORIZED";
            public const string PARENT_COMMENT_NOT_FOUND = "COMMENT.PARENT_COMMENT_NOT_FOUND";
        }

        public static class GROUP
        {
            public const string USER_ID_REQUIRED = "GROUP_USER_ID_REQUIRED";
            public const string NAME_REQUIRED = "GROUP_NAME_REQUIRED";
            public const string NOT_FOUND = "GROUP_NOT_FOUND";
            public const string NOT_A_MEMBER = "NOT_A_MEMBER";
            public const string NOT_ADMIN = "NOT_ADMIN";
            public const string NOT_OWNER = "NOT_OWNER";
            public const string PRIVACY_INVALID = "GROUP_PRIVACY_INVALID";
            public const string PERMISSION_INVALID = "GROUP_PERMISSION_INVALID";
            public const string LANGUAGE_INVALID = "GROUP_LANGUAGE_INVALID";
            public const string RULE_INVALID = "GROUP_RULE_INVALID";
            public const string MEMBER_NOT_FOUND = "GROUP_MEMBER_NOT_FOUND";
            public const string ALREADY_MEMBER = "GROUP_ALREADY_MEMBER";
            public const string JOIN_REQUEST_NOT_FOUND = "GROUP_JOIN_REQUEST_NOT_FOUND";
            public const string JOIN_REQUEST_PENDING = "GROUP_JOIN_REQUEST_PENDING";
            public const string MEMBER_BLOCKED = "GROUP_MEMBER_BLOCKED";
            public const string INVITE_INVALID = "GROUP_INVITE_INVALID";
            public const string FRIEND_REQUIRED = "GROUP_FRIEND_REQUIRED";
            public const string ROLE_INVALID = "GROUP_ROLE_INVALID";
            public const string OWNER_ACTION_INVALID = "GROUP_OWNER_ACTION_INVALID";
            public const string SELF_ACTION_INVALID = "GROUP_SELF_ACTION_INVALID";
        }

        public static class SERVER
        {
            public const string SYSTEM_ERROR = "SERVER.SYSTEM_ERROR";
            public const string DATABASE_ERROR = "SERVER.DATABASE_ERROR";
        }

        public static class VALIDATION
        {
            public const string REQUIRED = "VAL.REQUIRED";
            public const string INVALID = "VAL.INVALID";
        }

    }
}

