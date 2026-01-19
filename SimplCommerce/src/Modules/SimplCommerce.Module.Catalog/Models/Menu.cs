using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SimplCommerce.Infrastructure.Models;
using System.Collections.Generic;

namespace SimplCommerce.Module.Catalog.Models
{
    public class MenuType : EntityBase
    {
        [Required]
        [StringLength(50)]
        public string Code { get; set; }   // HEADER, FOOTER

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedOn { get; set; }
    }

    public class Menu : EntityBase
    {
        [Required]
        public long MenuTypeId { get; set; }
        public virtual MenuType MenuType { get; set; }

        [Required]
        [StringLength(50)]
        public string Code { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedOn { get; set; }

        public virtual ICollection<MenuItem> MenuItems { get; set; }
    }

    public class MenuItemType : EntityBase
    {
        [Required]
        [StringLength(50)]
        public string Code { get; set; }   // CUSTOM, CATEGORY, PAGE

        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedOn { get; set; }
    }

    public class MenuItem : EntityBase
    {
        public long MenuId { get; set; }
        public virtual Menu Menu { get; set; }

        public long? ParentId { get; set; }
        public virtual MenuItem Parent { get; set; }

        public long MenuItemTypeId { get; set; }
        public virtual MenuItemType MenuItemType { get; set; }

        public int Position { get; set; } = 0;

        [StringLength(150)]
        public string TitleEn { get; set; }
        [StringLength(150)]
        public string TitleAr { get; set; }

        public string Url { get; set; }

        public long? EntityId { get; set; } // points to Category / Page / etc.

        public bool IsActive { get; set; } = true;
        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? ModifiedOn { get; set; }

        public virtual ICollection<MenuItem> Children { get; set; }
    }
}
