using Microsoft.AspNetCore.Mvc;
using KappaTracker.Services;

namespace KappaTracker.Web.Controllers
{
    [ApiController]
    [Route("kappa/api/icon")]
    public class IconController : ControllerBase
    {
        private readonly IconCacheService _iconCache;

        public IconController(IconCacheService iconCache)
        {
            _iconCache = iconCache;
        }

        [HttpGet("{templateId}")]
        public async Task<IActionResult> GetIcon(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId) || templateId.Length != 24)
                return BadRequest();

            var bytes = await _iconCache.GetIconAsync(templateId);
            if (bytes is null || bytes.Length == 0)
                return NotFound();

            // Icons are immutable per template id - let the browser keep them.
            Response.Headers.CacheControl = "public, max-age=2592000, immutable";
            return File(bytes, "image/webp");
        }
    }
}
