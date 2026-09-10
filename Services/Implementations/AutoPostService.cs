using System.Text.RegularExpressions;
using System.Xml.Linq;
using Kpett.ChatApp.Data;
using Kpett.ChatApp.DTOs.Request.Post;
using Kpett.ChatApp.Enums;
using Kpett.ChatApp.Extensions;
using Kpett.ChatApp.Helpers;
using Kpett.ChatApp.Models;
using Kpett.ChatApp.Options;
using Kpett.ChatApp.Services.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kpett.ChatApp.Services.Implementations
{
    public class AutoPostService : IAutoPostService
    {
        private readonly AppDbContext _db;
        private readonly IPostService _postService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<AutoPostOptions> _options;
        private readonly ILogger<AutoPostService> _logger;

        public AutoPostService(
            AppDbContext db,
            IPostService postService,
            IHttpClientFactory httpClientFactory,
            IOptions<AutoPostOptions> options,
            ILogger<AutoPostService> logger)
        {
            _db = db;
            _postService = postService;
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
        }

        public Task<int> FetchAndPostAsync(CancellationToken cancel = default)
            => FetchAndPostOnceAsync(cancel);

        public async Task<int> FetchAndPostOnceAsync(CancellationToken cancel = default)
        {
            var opts = _options.Value;

            if (!opts.Enabled)
            {
                _logger.LogInformation("AutoPost disabled (AutoPost:Enabled=false), skip");
                return 0;
            }

            var botUserId = await ResolveBotUserIdAsync(opts, cancel);
            if (string.IsNullOrWhiteSpace(botUserId))
            {
                _logger.LogWarning("AutoPost: không tìm thấy bot user (BotUserId={BotUserId}, BotUsername={BotUsername}, BotEmail={BotEmail})", opts.BotUserId, opts.BotUsername, opts.BotEmail);
                return 0;
            }

            if (opts.RssFeeds == null || opts.RssFeeds.Count == 0)
            {
                _logger.LogWarning("AutoPost: RssFeeds rỗng");
                return 0;
            }

            var client = _httpClientFactory.CreateClient("AutoPost");
            var allItems = new List<RssItem>();

            foreach (var feedUrl in opts.RssFeeds)
            {
                try
                {
                    var items = await FetchRssAsync(client, feedUrl, cancel);
                    allItems.AddRange(items);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AutoPost: lỗi fetch RSS {FeedUrl}", feedUrl);
                }
            }

            if (allItems.Count == 0)
            {
                _logger.LogInformation("AutoPost: không có item nào từ RSS");
                return 0;
            }

            // Mới nhất trước, deduplicate theo Link
            var distinct = allItems
                .Where(x => !string.IsNullOrWhiteSpace(x.Link))
                .GroupBy(x => x.Link!, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.PubDate).First())
                .OrderByDescending(x => x.PubDate)
                .Take(opts.MaxPostsPerRun * 3) // lấy dư để trừ trùng
                .ToList();

            var created = 0;
            foreach (var item in distinct)
            {
                if (created >= opts.MaxPostsPerRun) break;
                if (cancel.IsCancellationRequested) break;

                // Dedupe: nếu đã có post chứa link này thì skip
                var link = item.Link!;
                var exists = await _db.Posts.AsNoTracking()
                    .AnyAsync(p => !p.IsDeleted && p.Content != null && p.Content.Contains(link), cancel);
                if (exists)
                {
                    _logger.LogInformation("AutoPost: skip trùng link {Link}", link);
                    continue;
                }

                var content = BuildContent(item);
                var media = BuildMedia(item);

                var req = new PostRequest
                {
                    Content = content,
                    Privacy = PostPrivacy.Public.GetDescription(),
                    Type = PostType.Post.GetDescription(),
                    AllowComments = true,
                    IsNsfw = false,
                    Media = media
                };

                try
                {
                    await _postService.CreatePostAsync(botUserId, req, cancel);
                    created++;
                    _logger.LogInformation("AutoPost: đã tạo post từ {Link} bởi {BotUserId}", link, botUserId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AutoPost: lỗi tạo post cho {Link}", link);
                }
            }

            _logger.LogInformation("AutoPost: hoàn tất, tạo {Count} bài", created);
            return created;
        }

        private async Task<string?> ResolveBotUserIdAsync(AutoPostOptions opts, CancellationToken cancel)
        {
            // 1. Ưu tiên BotUserId trực tiếp
            if (!string.IsNullOrWhiteSpace(opts.BotUserId))
            {
                var exists = await _db.Users.AsNoTracking().AnyAsync(u => u.Id == opts.BotUserId, cancel);
                if (exists) return opts.BotUserId;
                _logger.LogWarning("AutoPost: BotUserId {Id} không tồn tại, thử fallback", opts.BotUserId);
            }

            // 2. Fallback username
            if (!string.IsNullOrWhiteSpace(opts.BotUsername))
            {
                var byUsername = await _db.Users.AsNoTracking()
                    .Where(u => u.Username == opts.BotUsername)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync(cancel);
                if (!string.IsNullOrWhiteSpace(byUsername)) return byUsername;
            }

            // 3. Fallback email
            if (!string.IsNullOrWhiteSpace(opts.BotEmail))
            {
                var byEmail = await _db.Users.AsNoTracking()
                    .Where(u => u.Email == opts.BotEmail)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync(cancel);
                if (!string.IsNullOrWhiteSpace(byEmail)) return byEmail;
            }

            // 4. Cuối cùng: lấy SuperAdmin hoặc user đầu tiên active
            var fallback = await _db.Users.AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.CreatedAt)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(cancel);
            return fallback;
        }

        private static string BuildContent(RssItem item)
        {
            // Tiêu đề + mô tả ngắn + nguồn
            var title = CleanHtml(item.Title);
            var desc = CleanHtml(Truncate(item.Description, 300));
            var link = item.Link ?? "";

            // Format thân thiện với Kpet social
            if (!string.IsNullOrWhiteSpace(desc))
                return $"🐾 {title}\n\n{desc}\n\n🔗 Nguồn: {link}";
            return $"🐾 {title}\n\n🔗 Nguồn: {link}";
        }

        private static List<MediaRequest>? BuildMedia(RssItem item)
        {
            if (string.IsNullOrWhiteSpace(item.ImageUrl)) return null;
            var decodedUrl = System.Net.WebUtility.HtmlDecode(item.ImageUrl.Trim());
            // Chỉ nhận ảnh http/https
            if (!decodedUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;

            return new List<MediaRequest>
            {
                new MediaRequest
                {
                    PublicId = Guid.NewGuid().ToString(),
                    Url = decodedUrl,
                    Type = MediaType.Image.GetDescription()
                }
            };
        }

        private static async Task<List<RssItem>> FetchRssAsync(HttpClient client, string feedUrl, CancellationToken cancel)
        {
            using var resp = await client.GetAsync(feedUrl, cancel);
            resp.EnsureSuccessStatusCode();
            var xml = await resp.Content.ReadAsStringAsync(cancel);

            var doc = XDocument.Parse(xml);
            var items = new List<RssItem>();

            // RSS 2.0: <rss><channel><item>
            var rssItems = doc.Descendants("item");
            // Atom: <feed><entry>
            var atomEntries = doc.Descendants().Where(e => e.Name.LocalName == "entry");

            foreach (var el in rssItems)
            {
                var title = el.Element("title")?.Value?.Trim();
                var link = el.Element("link")?.Value?.Trim();
                // Atom link có thể là <link href="">
                if (string.IsNullOrWhiteSpace(link))
                    link = el.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value?.Trim();

                var desc = el.Element("description")?.Value ?? el.Descendants().FirstOrDefault(e => e.Name.LocalName == "summary")?.Value;
                var pubDateStr = el.Element("pubDate")?.Value ?? el.Element("published")?.Value ?? el.Descendants().FirstOrDefault(e => e.Name.LocalName == "published")?.Value;
                DateTime.TryParse(pubDateStr, out var pubDate);

                var imageUrl = ExtractImageUrl(el);

                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(link))
                    items.Add(new RssItem { Title = title, Link = link, Description = desc, PubDate = pubDate == default ? DateTime.UtcNow : pubDate, ImageUrl = imageUrl });
            }

            foreach (var el in atomEntries)
            {
                var title = el.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value?.Trim();
                var link = el.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Attribute("href")?.Value?.Trim()
                           ?? el.Elements().FirstOrDefault(e => e.Name.LocalName == "link")?.Value?.Trim();
                var desc = el.Elements().FirstOrDefault(e => e.Name.LocalName == "summary")?.Value
                           ?? el.Elements().FirstOrDefault(e => e.Name.LocalName == "content")?.Value;
                var pubDateStr = el.Elements().FirstOrDefault(e => e.Name.LocalName == "published")?.Value
                                 ?? el.Elements().FirstOrDefault(e => e.Name.LocalName == "updated")?.Value;
                DateTime.TryParse(pubDateStr, out var pubDate);
                var imageUrl = ExtractImageUrl(el);

                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(link))
                    items.Add(new RssItem { Title = title, Link = link, Description = desc, PubDate = pubDate == default ? DateTime.UtcNow : pubDate, ImageUrl = imageUrl });
            }

            return items;
        }

        private static string? ExtractImageUrl(XElement itemEl)
        {
            // 1. <enclosure url="" type="image/...">
            var enclosure = itemEl.Elements("enclosure").FirstOrDefault(e => (e.Attribute("type")?.Value ?? "").StartsWith("image", StringComparison.OrdinalIgnoreCase));
            var url = enclosure?.Attribute("url")?.Value;
            if (!string.IsNullOrWhiteSpace(url)) return url.Trim();

            // 2. <media:content> / <media:thumbnail> (namespace bất kỳ)
            var mediaContent = itemEl.Descendants().FirstOrDefault(e => e.Name.LocalName == "content" && e.Attribute("url") != null);
            url = mediaContent?.Attribute("url")?.Value;
            if (!string.IsNullOrWhiteSpace(url) && IsImageUrl(url)) return url.Trim();

            var thumb = itemEl.Descendants().FirstOrDefault(e => e.Name.LocalName == "thumbnail" && e.Attribute("url") != null);
            url = thumb?.Attribute("url")?.Value;
            if (!string.IsNullOrWhiteSpace(url)) return url.Trim();

            // 3. Trong description có <img src="">
            var desc = itemEl.Element("description")?.Value ?? "";
            if (!string.IsNullOrWhiteSpace(desc))
            {
                var m = Regex.Match(desc, "<img[^>]+src=[\"'](?<src>[^\"']+)[\"']", RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups["src"].Value.Trim();
            }

            return null;
        }

        private static bool IsImageUrl(string url)
        {
            var lower = url.ToLowerInvariant();
            return lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".png") || lower.EndsWith(".webp") || lower.EndsWith(".gif") || lower.Contains("image");
        }

        private static string? CleanHtml(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var s = Regex.Replace(input, "<[^>]+>", "");
            s = System.Net.WebUtility.HtmlDecode(s);
            return s.Trim();
        }

        private static string? Truncate(string? s, int max)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            s = CleanHtml(s) ?? s;
            if (s.Length <= max) return s;
            return s.Substring(0, max).Trim() + "...";
        }

        private class RssItem
        {
            public string? Title { get; set; }
            public string? Link { get; set; }
            public string? Description { get; set; }
            public DateTime PubDate { get; set; }
            public string? ImageUrl { get; set; }
        }
    }
}
