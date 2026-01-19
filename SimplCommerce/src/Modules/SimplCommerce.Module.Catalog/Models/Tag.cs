using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SimplCommerce.Infrastructure.Models;

namespace SimplCommerce.Module.Catalog.Models
{
    public class ProductTagType : EntityBase
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; }         // e.g., "System" or "Manual"

        [StringLength(500)]
        public string Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedOn { get; set; }
    }

    // Product tag
    public class ProductTag : EntityBase
    {
        [Required]
        [StringLength(200)]
        public string TitleEn { get; set; }

        [StringLength(200)]
        public string TitleAr { get; set; }

        [Required]
        [ForeignKey("ProductTagType")]
        public long ProductTagTypeId { get; set; }
        public virtual ProductTagType ProductTagType { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedOn { get; set; }

    }

    // Mapping table for Product <-> Tag
    public class ProductTagMapping : EntityBase
    {
        public long ProductId { get; set; }

        public long ProductTagId { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedOn { get; set; }
    }
}
