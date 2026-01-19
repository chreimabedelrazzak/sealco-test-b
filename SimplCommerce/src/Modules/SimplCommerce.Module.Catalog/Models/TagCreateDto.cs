using System;
using System.ComponentModel.DataAnnotations;

namespace SimplCommerce.Module.Catalog.Areas.Catalog.Models
{
    /// <summary>
    /// DTO for creating a new ProductTag.
    /// Clients only provide the necessary fields.
    /// </summary>
    public class TagCreateDto
    {
        [Required(ErrorMessage = "The TitleEn field is required.")]
        [StringLength(200, ErrorMessage = "TitleEn cannot be longer than 200 characters.")]
        public string TitleEn { get; set; }

        [StringLength(200, ErrorMessage = "TitleAr cannot be longer than 200 characters.")]
        public string TitleAr { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedOn { get; set; }
    }
}
