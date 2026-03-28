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
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Kpett.ChatApp.Services.Interfaces
{
    public interface IPostService
    {
        // Post operations
        Task<string> CreatePostAsync(string userId, PostRequest postRequest, CancellationToken cancel);
        Task<PostResponseDTO> GetPostAsync(string postId, string? currentUserId, CancellationToken cancel);
        Task<PaginatedData<PostFeedResponse>> GetFeedAsync(string currentUserId, string? cursor, int limit = 10);
        Task<List<PostResponseDTO>> GetUserPostsAsync(string userId, SearchRequest request, CancellationToken cancel = default);
        Task<PostResponseDTO> UpdatePostAsync(string postId, string userId, string content, string privacy, CancellationToken cancel);
        Task DeletePostAsync(string postId, string userId, CancellationToken cancel);

        // Reaction operations
        Task<PostReactionDTO> AddReactionAsync(string postId, string userId, byte reactionType, CancellationToken cancel);
        Task RemoveReactionAsync(string postId, string userId, CancellationToken cancel);
        Task<List<PostReactionDTO>> GetPostReactionsAsync(string postId, CancellationToken cancel);

        // Comment operations
        Task<CommentDTO> AddCommentAsync(string postId, string userId, string content, string? parentCommentId, CancellationToken cancel);
        Task<List<CommentDTO>> GetCommentsAsync(string postId, CancellationToken cancel);
        Task<CommentDTO> UpdateCommentAsync(string commentId, string userId, string content, CancellationToken cancel);
        Task DeleteCommentAsync(string commentId, string userId, CancellationToken cancel);
    }
}
