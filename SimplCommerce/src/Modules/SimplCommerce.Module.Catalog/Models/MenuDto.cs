using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SimplCommerce.Module.Catalog.Areas.Catalog.Models
{
    public class MenuDto
    {
        public long Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; }

        public long MenuTypeId { get; set; }

        public bool IsActive { get; set; }

        public List<MenuItemDto> Items { get; set; } = new();
    }
}
