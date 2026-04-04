SELECT
    COUNT(*) AS NullPairCount
FROM [KpettChatApp].[FriendRequests]
WHERE [UserLowId] IS NULL
   OR [UserHighId] IS NULL;

SELECT
    [UserLowId],
    [UserHighId],
    COUNT(*) AS PairCount
FROM [KpettChatApp].[FriendRequests]
GROUP BY [UserLowId], [UserHighId]
HAVING COUNT(*) > 1;

SELECT
    [Id],
    [SenderId],
    [ReceiverId],
    [UserLowId],
    [UserHighId],
    [Status]
FROM [KpettChatApp].[FriendRequests]
WHERE [UserLowId] <> CASE
        WHEN [SenderId] < [ReceiverId] THEN [SenderId]
        ELSE [ReceiverId]
    END
   OR [UserHighId] <> CASE
        WHEN [SenderId] < [ReceiverId] THEN [ReceiverId]
        ELSE [SenderId]
    END;
