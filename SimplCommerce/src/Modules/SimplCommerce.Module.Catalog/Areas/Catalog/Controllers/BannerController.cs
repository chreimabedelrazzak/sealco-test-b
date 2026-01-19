using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimplCommerce.Module.Catalog.Areas.Catalog.Models;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Core.Services; // Added for IMediaService if needed

namespace SimplCommerce.Module.Catalog.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Route("api/banner")]
    [ApiController]
    public class BannerController : ControllerBase
    {
        private readonly IRepository<Banner> _bannerRepository;
        private readonly IRepository<BannerPageType> _bannerPageTypeRepository;
        private readonly IRepository<BannerType> _bannerTypeRepository;
        private readonly IMediaService _mediaService; // Useful for getting URLs

        public BannerController(
            IRepository<Banner> bannerRepository,
            IRepository<BannerPageType> bannerPageTypeRepository,
            IRepository<BannerType> bannerTypeRepository,
            IMediaService mediaService)
        {
            _bannerRepository = bannerRepository;
            _bannerPageTypeRepository = bannerPageTypeRepository;
            _bannerTypeRepository = bannerTypeRepository;
            _mediaService = mediaService;
        }

        // GET api/banner?pageCode=HOME&typeCode=SLIDER
        [HttpGet]
        public async Task<IActionResult> GetBanners([FromQuery] string pageCode, [FromQuery] string typeCode)
        {
            if (string.IsNullOrEmpty(pageCode) || string.IsNullOrEmpty(typeCode))
                return BadRequest("pageCode and typeCode are required.");

            var now = DateTime.UtcNow;

            var pageType = await _bannerPageTypeRepository.Query()
                .FirstOrDefaultAsync(p => p.Code == pageCode);

            var type = await _bannerTypeRepository.Query()
                .FirstOrDefaultAsync(t => t.Code == typeCode);

            if (pageType == null || type == null)
                return BadRequest("Invalid pageCode or typeCode.");

            var banners = await _bannerRepository.Query()
                .Include(x => x.ThumbnailMedia) // Include the Media table
                .Where(b => b.IsActive &&
                            (b.StartDate == null || b.StartDate <= now) &&
                            (b.EndDate == null || b.EndDate >= now) &&
                            b.BannerPageTypeId == pageType.Id &&
                            b.BannerTypeId == type.Id)
                .OrderBy(b => b.Position)
                .Select(b => new
                {
                    b.Id,
                    b.TitleEn,
                    b.SubtitleEn,
                    b.DescriptionEn,
                    b.TitleAr,
                    b.SubtitleAr,
                    b.DescriptionAr,
                    // Return the Media URL instead of Base64 bytes
                    ImageUrl = b.ThumbnailMedia != null ? _mediaService.GetThumbnailUrl(b.ThumbnailMedia) : null,
                    b.LinkUrl,
                    b.Position
                })
                .ToListAsync();

            return Ok(banners);
        }

        // POST api/banner
        [HttpPost]
        public async Task<IActionResult> CreateBanner([FromBody] BannerCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var pageType = await _bannerPageTypeRepository.Query()
                .FirstOrDefaultAsync(p => p.Code == dto.PageCode);
            var type = await _bannerTypeRepository.Query()
                .FirstOrDefaultAsync(t => t.Code == dto.TypeCode);

            if (pageType == null)
                return BadRequest($"BannerPageType with code '{dto.PageCode}' not found.");
            if (type == null)
                return BadRequest($"BannerType with code '{dto.TypeCode}' not found.");

            var banner = new Banner
            {
                TitleEn = dto.TitleEn,
                SubtitleEn = dto.SubtitleEn,
                DescriptionEn = dto.DescriptionEn,
                TitleAr = dto.TitleAr,
                SubtitleAr = dto.SubtitleAr,
                DescriptionAr = dto.DescriptionAr,
                
                // Map the Media ID from the DTO
                ThumbnailMediaId = dto.ThumbnailMediaId,
                BannerPageTypeId = pageType.Id,
                BannerTypeId = type.Id,

                LinkUrl = dto.LinkUrl,
                Position = dto.Position,
                IsActive = dto.IsActive,
                StartDate = dto.StartDate,
                EndDate = dto.EndDate,
                CreatedOn = DateTime.UtcNow
            };

            _bannerRepository.Add(banner);
            await _bannerRepository.SaveChangesAsync();

            return Ok(new { banner.Id });
        }
    }
}