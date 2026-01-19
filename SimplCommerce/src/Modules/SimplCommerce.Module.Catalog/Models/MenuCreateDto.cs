using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SimplCommerce.Module.Catalog.Areas.Catalog.Models;

public class MenuCreateDto
{
    [Required]
    public string Name { get; set; }

    [Required]
    [StringLength(50)]
    public string Code { get; set; }

    [Required]
    public long MenuTypeId { get; set; }

    public bool IsActive { get; set; } = true;

    public List<MenuItemDto> Items { get; set; } = new List<MenuItemDto>();
}
