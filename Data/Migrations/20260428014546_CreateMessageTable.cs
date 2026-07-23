using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kpett.ChatApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateMessageTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[KpettChatApp].[Messages]', N'U') IS NOT NULL
                   AND EXISTS (
                       SELECT 1
                       FROM sys.columns
                       WHERE object_id = OBJECT_ID(N'[KpettChatApp].[Messages]', N'U')
                         AND name = N'Id'
                         AND system_type_id = TYPE_ID(N'bigint')
                   )
                BEGIN
                    IF OBJECT_ID(N'[KpettChatApp].[Messages_Legacy_20260428014546]', N'U') IS NOT NULL
                    BEGIN
                        DROP TABLE [KpettChatApp].[Messages_Legacy_20260428014546];
                    END;

                    EXEC sp_rename N'KpettChatApp.Messages', N'Messages_Legacy_20260428014546';

                    IF OBJECT_ID(N'[KpettChatApp].[PK_Messages]', N'PK') IS NOT NULL
                    BEGIN
                        ALTER TABLE [KpettChatApp].[Messages_Legacy_20260428014546]
                        DROP CONSTRAINT [PK_Messages];
                    END;
                END;

                IF OBJECT_ID(N'[KpettChatApp].[Messages]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [KpettChatApp].[Messages] (
                        [Id] nvarchar(450) NOT NULL,
                        [ConversationId] nvarchar(450) NOT NULL,
                        [SenderId] nvarchar(450) NOT NULL,
                        [ReplyToMessageId] nvarchar(450) NULL,
                        [Content] nvarchar(max) NULL,
                        [Type] nvarchar(50) NOT NULL,
                        [ClientMessageId] nvarchar(450) NULL,
                        [Metadata] nvarchar(max) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        [IsDeleted] bit NOT NULL,
                        CONSTRAINT [PK_Messages] PRIMARY KEY ([Id])
                    );
                END;

                IF OBJECT_ID(N'[KpettChatApp].[Messages_Legacy_20260428014546]', N'U') IS NOT NULL
                BEGIN
                    INSERT INTO [KpettChatApp].[Messages] (
                        [Id],
                        [ConversationId],
                        [SenderId],
                        [ReplyToMessageId],
                        [Content],
                        [Type],
                        [ClientMessageId],
                        [Metadata],
                        [CreatedAt],
                        [UpdatedAt],
                        [IsDeleted]
                    )
                    SELECT
                        CONVERT(nvarchar(450), legacy.[Id]),
                        legacy.[ConversationId],
                        legacy.[SenderId],
                        NULL,
                        details.[Content],
                        LEFT(COALESCE(legacy.[Type], N'Text'), 50),
                        legacy.[ClientMessageId],
                        legacy.[Metadata],
                        legacy.[CreatedAt],
                        NULL,
                        COALESCE(legacy.[IsDeleted], 0)
                    FROM [KpettChatApp].[Messages_Legacy_20260428014546] AS legacy
                    LEFT JOIN [KpettChatApp].[MessageDetails] AS details
                        ON details.[MessageId] = legacy.[Id]
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM [KpettChatApp].[Messages] AS currentMessages
                        WHERE currentMessages.[Id] = CONVERT(nvarchar(450), legacy.[Id])
                    );

                    DROP TABLE [KpettChatApp].[Messages_Legacy_20260428014546];
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[KpettChatApp].[Messages]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [KpettChatApp].[Messages];
                END;
                """);
        }
    }
}
