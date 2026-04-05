namespace Kpett.ChatApp.Contants
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

        public static class SERVER
        {
            public const string SYSTEM_ERROR = "SERVER.SYSTEM_ERROR";
            public const string DATABASE_ERROR = "SERVER.DATABASE_ERROR";
        }

        public static class VALIDATION
        {
            public const string REQUIRED = "VAL.REQUIRED";
        }

        public static class CLOUDINARY
        {
            public const string MISSING_HEADER = "CLOUDINARY.MISSING_HEADER";
            public const string INVALID_SIGNATURE = "CLOUDINARY.INVALID_SIGNATURE";
        }
    }
}
