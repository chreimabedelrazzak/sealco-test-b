using System;

namespace SimplCommerce.Module.Catalog.Areas.Catalog.Models
{
    public class BannerCreateDto
    {
        public string TitleEn { get; set; }
        public string SubtitleEn { get; set; }
        public string DescriptionEn { get; set; }

        public string TitleAr { get; set; }
        public string SubtitleAr { get; set; }
        public string DescriptionAr { get; set; }

        // Reference to the uploaded media ID instead of Base64 strings
        public long? ThumbnailMediaId { get; set; }

        public string LinkUrl { get; set; }

        public string PageCode { get; set; }  // HOME, CATEGORY, etc.
        public string TypeCode { get; set; }  // SLIDER, CTA, etc.

        public int Position { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}