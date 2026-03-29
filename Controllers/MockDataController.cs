using Kpett.ChatApp.Models;
using Kpett.ChatApp.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kpett.ChatApp.Controllers
{
    [Route("api/[controller]")]
    public class MockDataController : Controller
    {

        private readonly AppDbContext _context;

        public MockDataController(IPostService postFeedService, AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("map-mock-data")]
        public async Task<IActionResult> MapMockData()
        {
            // Lấy danh sách ID của 1000 Users (Chỉ lấy ID để tiết kiệm RAM)
            var userIds = await _context.Users.Select(u => u.Id).ToListAsync();

            if (!userIds.Any()) return BadRequest("Không có user nào trong DB.");

            // Lấy toàn bộ Posts
            var posts = await _context.Posts.ToListAsync();
            var random = new Random();

            // Lặp qua 33.000 bài post và gán ngẫu nhiên 1 User ID
            foreach (var post in posts)
            {
                var randomUserIndex = random.Next(userIds.Count);
                post.CreatedByUserId = userIds[randomUserIndex];
            }

            // Lưu thay đổi xuống DB
            await _context.SaveChangesAsync();

            return Ok($"Đã map thành công {posts.Count} posts.");
        }

        private readonly List<string> _realImages = new List<string>
        {
            "https://images.unsplash.com/photo-1517849845537-4d257902454a?w=1080&q=80", // Chó cưng
            "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=1080&q=80", // Chân dung cô gái
            "https://images.unsplash.com/photo-1506744626753-eda8151a747b?w=1080&q=80", // Phong cảnh núi
            "https://images.unsplash.com/photo-1543852786-1cf6624b9987?w=1080&q=80", // Mèo cưng
            "https://images.unsplash.com/photo-1499364615650-ec38552f4f34?w=1080&q=80", // Đời sống âm nhạc
            "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=1080&q=80", // Chân dung nam
            "https://images.unsplash.com/photo-1472214103451-9374bd1c798e?w=1080&q=80", // Phong cảnh đồi cỏ
            "https://images.unsplash.com/photo-1558981403-c5f9899a28bc?w=1080&q=80", // Xe cộ / Đường phố
            "https://images.unsplash.com/photo-1511367461989-f85a21fda167?w=1080&q=80", // Nghệ thuật / Trừu tượng
            "https://images.unsplash.com/photo-1524504388940-b1c1722653e1?w=1080&q=80", // Chân dung thời trang
            "https://images.unsplash.com/photo-1555685812-4b943f1cb0eb?w=1080&q=80", // Đồ ăn / Cafe
            "https://images.unsplash.com/photo-1504595403659-9088ce801e29?w=1080&q=80", // Động vật hoang dã
            "https://images.unsplash.com/photo-1522202176988-66273c2fd55f?w=1080&q=80", // Nhóm bạn bè
            "https://images.unsplash.com/photo-1488161628813-04466f872528?w=1080&q=80", // Quán cafe / Lập trình
            "https://images.unsplash.com/photo-1516483638261-f4085ee6b633?w=1080&q=80"  // Biển / Du lịch
        };

        // 2. Chuẩn bị sẵn kho dữ liệu VIDEO THẬT (Video MP4 nhẹ, stream mượt mà)
        private readonly List<string> _realVideos = new List<string>
        {
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerBlazes.mp4",
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ForBiggerJoyrides.mp4",
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4",
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4",
            "https://commondatastorage.googleapis.com/gtv-videos-bucket/sample/TearsOfSteel.mp4"
        };


        [HttpPost("generate-real-post-media")]
        public async Task<IActionResult> GenerateRealPostMedia([FromQuery] int numberOfPosts = 30000)
        {
            // Lấy ngẫu nhiên các PostId từ database
            var postIds = await _context.Posts
                .OrderBy(p => Guid.NewGuid())
                .Select(p => p.Id)
                .Take(numberOfPosts)
                .ToListAsync();

            if (!postIds.Any())
            {
                return BadRequest("Không tìm thấy bài post nào trong Database. Vui lòng tạo Post trước.");
            }

            var postMedias = new List<PostMedia>();
            var random = new Random();

            foreach (var postId in postIds)
            {
                var mediaId = Guid.NewGuid().ToString();

                // Quyết định loại Media: 80% là Ảnh, 20% là Video
                int mediaTypeChance = random.Next(1, 11);

                if (mediaTypeChance <= 8)
                {
                    // Lấy ngẫu nhiên 1 đường link ảnh thật từ danh sách
                    var randomImageUrl = _realImages[random.Next(_realImages.Count)];

                    postMedias.Add(new PostMedia
                    {
                        Id = mediaId,
                        PostId = postId,
                        MediaType = "Image",
                        MediaUrl = randomImageUrl,
                        ThumbnailUrl = "", // Ảnh không cần thumbnail
                        Width = 1080,
                        Height = 1080, // Tỷ lệ vuông 1:1 phổ biến trên mạng xã hội
                        Duration = 0,
                        SortOrder = 1
                    });
                }
                else
                {
                    // Lấy ngẫu nhiên 1 đường link video thật từ danh sách
                    var randomVideoUrl = _realVideos[random.Next(_realVideos.Count)];

                    // Lấy ngẫu nhiên 1 ảnh làm Thumbnail cho video
                    var randomThumbnailUrl = _realImages[random.Next(_realImages.Count)];

                    postMedias.Add(new PostMedia
                    {
                        Id = mediaId,
                        PostId = postId,
                        MediaType = "Video",
                        MediaUrl = randomVideoUrl,
                        ThumbnailUrl = randomThumbnailUrl,
                        Width = 1280,
                        Height = 720, // Tỷ lệ ngang 16:9 chuẩn video
                        Duration = random.Next(15, 61), // Thời lượng ngẫu nhiên
                        SortOrder = 1
                    });
                }
            }

            try
            {
                // Lưu hàng loạt vào CSDL để tối ưu tốc độ
                await _context.PostMedia.AddRangeAsync(postMedias);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Message = $"Đã tạo thành công dữ liệu ảnh/video thật cho {postIds.Count} bài posts.",
                    TotalImages = postMedias.Count(m => m.MediaType == "Image"),
                    TotalVideos = postMedias.Count(m => m.MediaType == "Video")
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi ghi dữ liệu: {ex.Message}");
            }
        }
    }
}
