using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SimplCommerce.Module.Catalog.Areas.Catalog.Models
{
    public class MenuItemDto
    {
        public long? Id { get; set; }  // null for new item
        public long? ParentId { get; set; }

        [Required]
        public long MenuItemTypeId { get; set; }

        public long? EntityId { get; set; }  // For category/page types

        [StringLength(150)]
        public string TitleEn { get; set; }
        [StringLength(150)]
        public string TitleAr { get; set; }

        public string Url { get; set; }
        public int Position { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        public bool IsDeleted { get; set; } = false; // For deletion in UpdateMenu
    }
}
