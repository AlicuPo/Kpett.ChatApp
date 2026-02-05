using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Channels;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MessageController : ControllerBase
    {
        private readonly IMessage _message;
        private readonly IToken _Itoken;
        public MessageController(IMessage message, IToken token)
        {
            _message = message;
            _Itoken = token;

        }

        [HttpPost("GetMessages")]
        [Authorize]
        public async Task<IActionResult> GetMessages(string conversationId, string currentUserId, long? cursorMessageId, int pageSize, CancellationToken cancel)
        {
            try
            {
                var result = await _message.GetMessagesAsync(conversationId, currentUserId, cursorMessageId, pageSize, cancel);

                return Ok(new
                {
                    StatusCode = StatusCodes.Status200OK,
                    Messages = result.Messages,
                    OldestMessageId = result.OldestMessageId,
                    HasMore = result.HasMore,
                    //UnreadCount = result.UnreadCount
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = ex.Message,
                    ErrorCode = StatusCodes.Status400BadRequest
                });
            }
        }



        [HttpPost("MarkAsRead/{id}")]
        [Authorize]
        public async Task<IActionResult> MarkAsRead(string id, [FromBody] ReadMessageRequest request, CancellationToken cancel)
        {
            try
            {
                await _message.MarkAsRead(id, request, cancel);
                return Ok(new
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Messages marked as read successfully."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest
                });
            }
        }

        [HttpPost("SendMessage")]
        [Authorize]
        public async Task<IActionResult> SendMessageAsync( string conversationId, [FromBody] DTOs.Request.SendMessageRequest request, CancellationToken cancel)
        {
            try
            {
                var userClaims = _Itoken.GetUserClaims();
                var senderId = userClaims?.UserId ?? string.Empty;
                var message = await _message.SendMessageAsync(conversationId, senderId, request, cancel);
                return Ok(new
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = message
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest
                });
            }
        }
        [HttpPost("GetMessagesAsync")]
        [Authorize]
        public async Task<IActionResult> GetMessagesAsync(string conversationId, [FromBody] long userId, [FromQuery] int lastReadMessageIdsearch, CancellationToken cancel)
        {
            try
            {
                var messages = await _message.GetMessagesAsync(conversationId, userId, lastReadMessageIdsearch, cancel);
                return Ok(new
                {
                    StatusCode = StatusCodes.Status200OK,
                    Messages = messages
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest
                });
            }
        }

        [HttpPost]
        [Route("MarkAsReadAsync/{conversationId}")]
        [Authorize]
        public async Task<IActionResult> MarkAsReadAsync(string conversationId, [FromBody] long request, CancellationToken cancel)
        {
            try
            {
                var userClaims = _Itoken.GetUserClaims();
                var userId = userClaims?.UserId ?? string.Empty;
                await _message.MarkAsReadAsync(conversationId, userId, request, cancel);
                return Ok(new
                {
                    StatusCode = StatusCodes.Status200OK,
                    Message = "Messages marked as read successfully."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest
                });
            }
        }
    }
}
