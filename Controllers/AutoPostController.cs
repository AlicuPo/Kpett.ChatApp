using Kpett.ChatApp.DTOs.Response.Shared;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/autopost")]
    [ApiController]
    public class AutoPostController : ControllerBase
    {
        private readonly IAutoPostService _autoPostService;
        private readonly IOptions<AutoPostOptions> _options;
        private readonly ILogger<AutoPostController> _logger;

        public AutoPostController(IAutoPostService autoPostService, IOptions<AutoPostOptions> options, ILogger<AutoPostController> logger)
        {
            _autoPostService = autoPostService;
            _options = options;
            _logger = logger;
        }

        /// <summary>
        /// Xem cấu hình AutoPost hiện tại (không cần auth để FE debug, nếu muốn bảo vệ thì thêm [Authorize]).
        /// </summary>
        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            var o = _options.Value;
            return Ok(new GeneralResponse<AutoPostOptions>
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = "AutoPost config",
                Data = o
            });
        }

        /// <summary>
        /// Trigger thủ công - chỉ SuperAdmin. Đổi người đăng = truyền BotUserId trong config hoặc dùng Bot mặc định.
        /// </summary>
        [HttpPost("trigger")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Trigger(CancellationToken cancel)
        {
            _logger.LogInformation("SuperAdmin triggered AutoPost manually");
            var count = await _autoPostService.FetchAndPostOnceAsync(cancel);
            return Ok(new GeneralResponse<object>
            {
                IsSuccess = true,
                StatusCode = 200,
                Message = $"AutoPost completed, created {count} posts",
                Data = new { created = count, botUserId = _options.Value.BotUserId, botUsername = _options.Value.BotUsername }
            });
        }
    }
}
