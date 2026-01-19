using SimplCommerce.Infrastructure.Models;

namespace SimplCommerce.Module.Catalog.Models // Put it in the core Models folder
{
    public class CategoryBanner : EntityBase
    {
        public long CategoryId { get; set; }
        
        // This was likely erroring because it couldn't find Category
        public virtual Category Category { get; set; }

        public long BannerId { get; set; }
        
        // This is in the same namespace or Catalog.Areas.Catalog.Models
        public virtual SimplCommerce.Module.Catalog.Areas.Catalog.Models.Banner Banner { get; set; }
    }
}