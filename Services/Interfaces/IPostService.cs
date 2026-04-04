using Azure.Core;
using CloudinaryDotNet;
using Kpett.ChatApp.Contants;
using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.DTOs.Request.Shared;
using Kpett.ChatApp.DTOs.Response.Post;
using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Exceptions;
using Kpett.ChatApp.Helper;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Receive;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IPostService
    {
        // Post operations
        Task<PostFeedResponse> CreatePostAsync(string userId, PostRequest postRequest, CancellationToken cancel);
        Task<PostFeedResponse> UpdatePostAsync(string postId, string userId, PostRequest postRequest, CancellationToken cancel);
        Task<PostFeedResponse> GetPostByIdAsync(string postId, string? currentUserId, CancellationToken cancel);
        Task<PaginatedData<PostFeedResponse>> GetFeedAsync(string currentUserId, string? cursor = null, int limit = 10, CancellationToken cancel = default);
        Task<PaginatedData<PostThumbnailResponse>> GetPostsByUserIdAsync(string userId, string currentUerId, SearchRequest request, CursorPaginationRequest cursorPagination, CancellationToken cancel = default);
        Task<PostResponseDTO> UpdatePostAsync(string postId, string userId, string content, string privacy, CancellationToken cancel);
        Task DeletePostAsync(string postId, string userId, CancellationToken cancel);

        // Media operations
        Task DeleteMedia(string publicId, [FromQuery] string resourceType);

        // Reaction operations
        Task<PostReactionDTO> AddReactionAsync(string postId, string userId, byte reactionType, CancellationToken cancel);
        Task RemoveReactionAsync(string postId, string userId, CancellationToken cancel);
        Task<List<PostReactionDTO>> GetPostReactionsAsync(string postId, CancellationToken cancel);

        // Comment operations
        Task<CommentDTO> AddCommentAsync(string postId, string userId, string content, string? parentCommentId, CancellationToken cancel);
        Task<CommentsPageDTO> GetCommentsAsync(string postId, string? parentCommentId, string currentUserId, string? cursor, int limit, CancellationToken cancel);
        Task<CommentDTO> UpdateCommentAsync(string commentId, string userId, string content, CancellationToken cancel);
        Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel);
    }
}
