using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Catalog.Models;
using SimplCommerce.Module.Catalog.Areas.Catalog.Models;

namespace SimplCommerce.Module.Catalog.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Route("api/tag")]
    [ApiController]
    public class TagController : ControllerBase
    {
        private readonly IRepository<Product> _productRepository;
        private readonly IRepository<ProductTag> _tagRepository;
        private readonly IRepository<ProductTagMapping> _tagMappingRepository;

        public TagController(
            IRepository<Product> productRepository,
            IRepository<ProductTag> tagRepository,
            IRepository<ProductTagMapping> tagMappingRepository)
        {
            _productRepository = productRepository;
            _tagRepository = tagRepository;
            _tagMappingRepository = tagMappingRepository;
        }

        // GET api/tag/products/{tagId}
        [HttpGet("products/{tagId}")]
        public IActionResult GetProductsByTagId(long tagId)
        {
            var productIds = _tagMappingRepository
                .Query()
                .Where(x => x.ProductTagId == tagId)
                .Select(x => x.ProductId)
                .ToList();

            if (!productIds.Any())
                return Ok(Array.Empty<Product>());

            var products = _productRepository
                .Query()
                .Where(p => productIds.Contains(p.Id))
                .ToList();

            return Ok(products);
        }

        // POST api/tag
        [HttpPost]
        public IActionResult CreateTag([FromBody] TagCreateDto dto)
        {
            if (dto == null)
                return BadRequest("Tag data is required.");

            var tag = new ProductTag
            {
                TitleEn = dto.TitleEn,
                TitleAr = dto.TitleAr,
                IsActive = dto.IsActive,
                ProductTagTypeId = 2,      // always 2
                ProductTagType = null,      // prevent EF from inserting a new ProductTagType
                CreatedOn = dto.CreatedOn,
                UpdatedOn = dto.UpdatedOn
            };

            _tagRepository.Add(tag);
            _tagRepository.SaveChanges();

            return Ok(new { tag.Id });
        }




        // POST api/tag/mapping
        [HttpPost("mapping")]
        public IActionResult AddProductTagMapping([FromBody] ProductTagMapping mapping)
        {
            if (mapping == null || mapping.ProductId <= 0 || mapping.ProductTagId <= 0)
                return BadRequest("ProductId and ProductTagId are required.");

            var exists = _tagMappingRepository
                .Query()
                .Any(x => x.ProductId == mapping.ProductId &&
                          x.ProductTagId == mapping.ProductTagId);

            if (exists)
                return Conflict("Tag already assigned to product.");

            mapping.CreatedOn = DateTime.UtcNow;

            _tagMappingRepository.Add(mapping);
            _tagMappingRepository.SaveChanges();

            return Ok(new
            {
                mapping.ProductId,
                mapping.ProductTagId
            });
        }

        // DELETE api/tag/mapping?productId=1&productTagId=2
        [HttpDelete("mapping")]
        public IActionResult RemoveProductTagMapping(long productId, long productTagId)
        {
            var mapping = _tagMappingRepository
                .Query()
                .FirstOrDefault(x => x.ProductId == productId &&
                                     x.ProductTagId == productTagId);

            if (mapping == null)
                return NotFound("Mapping not found.");

            _tagMappingRepository.Remove(mapping);
            _tagMappingRepository.SaveChanges();

            return Ok(new
            {
                productId,
                productTagId
            });
        }

        // DELETE api/tag/{id}
        [HttpDelete("{id:long}")]
        public IActionResult DeleteTag(long id)
        {
            // Find the tag by id
            var tag = _tagRepository.Query().FirstOrDefault(t => t.Id == id);

            if (tag == null)
                return NotFound($"ProductTag with Id {id} not found.");

            // Remove the tag
            _tagRepository.Remove(tag);
            _tagRepository.SaveChanges();

            return Ok(new { message = $"ProductTag with Id {id} deleted successfully." });
        }

    }
}
