using Kpett.ChatApp.DTOs.Request;
using Kpett.ChatApp.DTOs.Response;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PostController : ControllerBase
    {
        private readonly AppDbContext _dbcontext;
        private readonly IPostFeedService _postFeedService;
        public PostController(AppDbContext dbcontext , IPostFeedService postFeedService)
        {
            _dbcontext = dbcontext;
            _postFeedService = postFeedService;
        }

        [HttpPost("PostFeed")]
        public async Task<IActionResult> PostFeed([FromBody] PostMediaRequest postMedia, CancellationToken cancel)
        {
            try
            {
                await _postFeedService.PostFeed(postMedia, cancel);
                return Ok(new GeneralResponse
                {

                    StatusCode = StatusCodes.Status200OK,
                    Message = "post create successfully",
                    Return = true

                });
            }
            catch (Exception ex)
            {
                return BadRequest(new GeneralResponse
                {
                    Message = ex.Message,
                    ErorrCode = StatusCodes.Status400BadRequest,
                    Return = false
                });
            }

        }   
    }
}
