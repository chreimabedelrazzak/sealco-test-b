using System;
using System.ComponentModel.DataAnnotations.Schema;
using SimplCommerce.Infrastructure.Models;
using SimplCommerce.Module.Core.Models; // Ensure this is included for the Media class

namespace SimplCommerce.Module.Catalog.Areas.Catalog.Models
{
    public class Banner : EntityBase
    {
        public string TitleEn { get; set; }
        public string SubtitleEn { get; set; }
        public string DescriptionEn { get; set; }

        public string TitleAr { get; set; }
        public string SubtitleAr { get; set; }
        public string DescriptionAr { get; set; }

        public string LinkUrl { get; set; }

        // New Media Reference
        public long? ThumbnailMediaId { get; set; }
        public virtual Media ThumbnailMedia { get; set; }

        [ForeignKey("BannerPageType")]
        public long BannerPageTypeId { get; set; }
        public virtual BannerPageType BannerPageType { get; set; }

        [ForeignKey("BannerType")]
        public long BannerTypeId { get; set; }
        public virtual BannerType BannerType { get; set; }

        public int Position { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }
    }

    public class BannerPageType : EntityBase
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }

    public class BannerType : EntityBase
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }
}