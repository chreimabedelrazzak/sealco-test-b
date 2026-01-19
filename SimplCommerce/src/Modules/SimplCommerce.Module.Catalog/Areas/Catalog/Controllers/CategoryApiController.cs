using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Infrastructure.Web.SmartTable;
using SimplCommerce.Module.Catalog.Areas.Catalog.ViewModels;
using SimplCommerce.Module.Catalog.Models;
using SimplCommerce.Module.Catalog.Services;
using SimplCommerce.Module.Core.Models;
using SimplCommerce.Module.Core.Services;


namespace SimplCommerce.Module.Catalog.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [Authorize] // Require JWT token for all actions
    [Route("api/categories")]
    public class CategoryApiController : Controller
    {
        private readonly IRepository<Category> _categoryRepository;
        private readonly IRepository<Product> _productRepository;
        private readonly IRepository<ProductCategory> _productCategoryRepository;
        private readonly IRepository<CategoryBanner> _categoryBannerRepository;
        private readonly ICategoryService _categoryService;
        private readonly IMediaService _mediaService;

        public CategoryApiController(
            IRepository<Category> categoryRepository,
            IRepository<ProductCategory> productCategoryRepository,
            IRepository<CategoryBanner> categoryBannerRepository,
            IRepository<Product> productRepository,
            ICategoryService categoryService,
            IMediaService mediaService)
        {
            _categoryRepository = categoryRepository;
            _productCategoryRepository = productCategoryRepository;
            _categoryBannerRepository = categoryBannerRepository;
            _productRepository = productRepository;
            _categoryService = categoryService;
            _mediaService = mediaService;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            // We query the Repository directly to control exactly what is returned
            var categories = await _categoryRepository.Query()
                .Where(x => !x.IsDeleted)
                .Select(x => new CategoryListItem // Map directly to your updated ViewModel
                {
                    Id = x.Id,
                    Name = x.Name,       // Raw name from DB (no '>>')
                    Slug = x.Slug,       // This will now be populated
                    DisplayOrder = x.DisplayOrder,
                    IncludeInMenu = x.IncludeInMenu,
                    IsPublished = x.IsPublished,
                    ParentId = x.ParentId
                })
                .OrderBy(x => x.DisplayOrder)
                .ToListAsync();

            return Json(categories);
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(long id)
        {
            var category = await _categoryRepository.Query()
                .Include(x => x.ThumbnailImage)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (category == null)
                return NotFound();

            var model = new CategoryForm
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                MetaTitle = category.MetaTitle,
                MetaKeywords = category.MetaKeywords,
                MetaDescription = category.MetaDescription,
                DisplayOrder = category.DisplayOrder,
                Description = category.Description,
                ParentId = category.ParentId,
                IncludeInMenu = category.IncludeInMenu,
                IsPublished = category.IsPublished,
                ThumbnailImageUrl = _mediaService.GetThumbnailUrl(category.ThumbnailImage),
            };

            return Json(model);
        }


        [AllowAnonymous]
        [HttpGet("by-slug")]
        public async Task<IActionResult> GetBySlug([FromQuery] string slug)
        {
            if (string.IsNullOrEmpty(slug))
                return BadRequest("Slug is required");

            // 1. FIRST: Fetch the category. You cannot use 'category.Id' until this is done.
            var category = await _categoryRepository.Query()
                .Include(x => x.ThumbnailImage)
                .FirstOrDefaultAsync(x => x.Slug == slug && !x.IsDeleted);

            if (category == null)
                return NotFound();

            var now = DateTime.UtcNow;

            // 2. NOW you can fetch banners using category.Id
            var banners = await _categoryBannerRepository.Query()
            .Include(cb => cb.Banner)
                .ThenInclude(b => b.ThumbnailMedia)
            .Where(cb => cb.CategoryId == category.Id && cb.Banner.IsActive)
            .Select(cb => cb.Banner)
            .Where(b => (b.StartDate == null || b.StartDate <= now) && 
                        (b.EndDate == null || b.EndDate >= now))
            .OrderBy(b => b.Position)
            .Select(b => new 
            {
                b.Id,
                b.TitleEn,
                b.SubtitleEn,
                b.DescriptionEn,
                b.TitleAr,
                b.SubtitleAr,
                b.DescriptionAr,
                b.LinkUrl,
                // Pass the specific size string here
                ThumbnailImageUrl = b.ThumbnailMedia != null ? _mediaService.GetThumbnailUrl(b.ThumbnailMedia, "1384x492xi") : null
            })
            .ToListAsync();
            // 3. Fetch children data
            var childrenData = await _categoryRepository.Query()
                .Include(x => x.ThumbnailImage)
                .Where(x => x.ParentId == category.Id && !x.IsDeleted && x.IsPublished)
                .OrderBy(x => x.DisplayOrder)
                .ToListAsync();

            var children = childrenData.Select(x => new 
            {
                x.Id,
                x.Name,
                x.Slug,
                ThumbnailImageUrl = _mediaService.GetThumbnailUrl(x.ThumbnailImage, "160x107xi")
            }).ToList();

            // 4. Construct the response
            return Json(new
            {
                Id = category.Id,
                category.Name,
                category.Slug,
                category.Description,
                category.MetaTitle,
                category.MetaKeywords,
                category.MetaDescription,
                category.DisplayOrder,
                category.ParentId,
                category.IncludeInMenu,
                category.IsPublished,
                ThumbnailImageUrl = _mediaService.GetThumbnailUrl(category.ThumbnailImage, "160x107xi"),
                SubCategories = children,
                Banners = banners 
            });
        }

        [HttpPost]
        public async Task<IActionResult> Post(CategoryForm model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = new Category
            {
                Name = model.Name,
                Slug = model.Slug,
                MetaTitle = model.MetaTitle,
                MetaKeywords = model.MetaKeywords,
                MetaDescription = model.MetaDescription,
                DisplayOrder = model.DisplayOrder,
                Description = model.Description,
                ParentId = model.ParentId,
                IncludeInMenu = model.IncludeInMenu,
                IsPublished = model.IsPublished
            };

            await SaveCategoryImage(category, model);
            await _categoryService.Create(category);

            return CreatedAtAction(nameof(Get), new { id = category.Id }, null);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(long id, CategoryForm model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var category = await _categoryRepository.Query().FirstOrDefaultAsync(x => x.Id == id);
            if (category == null)
                return NotFound();

            category.Name = model.Name;
            category.Slug = model.Slug;
            category.MetaTitle = model.MetaTitle;
            category.MetaKeywords = model.MetaKeywords;
            category.MetaDescription = model.MetaDescription;
            category.Description = model.Description;
            category.DisplayOrder = model.DisplayOrder;
            category.ParentId = model.ParentId;
            category.IncludeInMenu = model.IncludeInMenu;
            category.IsPublished = model.IsPublished;

            if (category.ParentId.HasValue && await HaveCircularNesting(category.Id, category.ParentId.Value))
            {
                ModelState.AddModelError("ParentId", "Parent category cannot be itself children");
                return BadRequest(ModelState);
            }

            await SaveCategoryImage(category, model);
            await _categoryService.Update(category);

            return Accepted();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var category = _categoryRepository.Query()
                .Include(x => x.Children)
                .FirstOrDefault(x => x.Id == id);

            if (category == null)
                return NotFound();

            if (category.Children.Any(x => !x.IsDeleted))
                return BadRequest(new { Error = "Please make sure this category contains no children" });

            await _categoryService.Delete(category);
            return NoContent();
        }

[AllowAnonymous]
[HttpGet("{id}/products")]
public async Task<IActionResult> GetProducts(
    long id, 
    [FromQuery] string categories = "", 
    [FromQuery] string attributes = "", 
    [FromQuery] decimal? minPrice = null, 
    [FromQuery] decimal? maxPrice = null, 
    int page = 1, 
    int pageSize = 12)
{
    // 1. Get Category IDs once (Recursive in-memory logic)
    var allCategoryIds = GetCategoryAndChildrenIds(id);

    // 2. Fetch Filters and Price Limits based on the whole category scope
    // We execute this FIRST to avoid DbContext concurrency issues
    var baseCategoryQuery = _productRepository.Query()
        .AsNoTracking()
        .Where(p => p.Categories.Any(c => allCategoryIds.Contains(c.CategoryId)) && p.IsPublished && !p.IsDeleted);

    var availableFiltersData = await baseCategoryQuery
        .SelectMany(p => p.AttributeValues)
        .Select(av => new { Name = av.Attribute.Name, av.Value })
        .Distinct()
        .ToListAsync();

    var minCategoryPrice = await baseCategoryQuery.MinAsync(p => (decimal?)p.Price) ?? 0;
    var maxCategoryPrice = await baseCategoryQuery.MaxAsync(p => (decimal?)p.Price) ?? 0;

    // 3. Build the Main Query for Products
    var query = _productRepository.Query()
        .AsNoTracking() 
        .Where(p => p.IsPublished && !p.IsDeleted && p.IsVisibleIndividually);

    // Apply Price Filtering
    if (minPrice.HasValue)
        query = query.Where(p => p.Price >= minPrice.Value);
    
    if (maxPrice.HasValue)
        query = query.Where(p => p.Price <= maxPrice.Value);

    // Apply Category Filters (either specific selection or whole parent branch)
    if (!string.IsNullOrEmpty(categories))
    {
        var selectedCatIds = categories.Split(',').Select(long.Parse).ToList();
        query = query.Where(p => p.Categories.Any(c => selectedCatIds.Contains(c.CategoryId)));
    }
    else
    {
        query = query.Where(p => p.Categories.Any(c => allCategoryIds.Contains(c.CategoryId)));
    }

    // Apply Dynamic Attribute Filters
    if (!string.IsNullOrEmpty(attributes))
    {
        var groups = attributes.Split('|', StringSplitOptions.RemoveEmptyEntries);
        foreach (var group in groups)
        {
            var parts = group.Split(':');
            if (parts.Length == 2)
            {
                var attrName = parts[0];
                var attrValues = parts[1].Split(',');
                query = query.Where(p => p.AttributeValues.Any(av => 
                    av.Attribute.Name == attrName && attrValues.Contains(av.Value)));
            }
        }
    }

    // 4. Get Totals and Paginated Data
    var totalItems = await query.CountAsync();
    
    var productData = await query
        .OrderBy(p => p.DisplayOrder)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(p => new {
            p.Id,
            ProductId = p.Id,
            p.Name,
            Description = p.Description,           // Use the property from productData
            ShortDescription = p.ShortDescription,
            p.Slug,
            p.Price,
            p.OldPrice,
            p.StockQuantity,
            p.ThumbnailImage,
            // Projecting Medias directly to avoid N+1 queries
            Medias = p.Medias.OrderBy(m => m.DisplayOrder).Select(m => m.Media).ToList(),
            Attributes = p.AttributeValues.Select(av => new { 
                AttributeName = av.Attribute.Name, 
                Value = av.Value 
            }).ToList()
        })
        .ToListAsync();

    // 5. Transform for Response
    var items = productData.Select(p => new {
        p.Id,
        ProductId = p.Id,
        ProductName = p.Name,
        Description = p.Description,           // Use the property from productData
        ShortDescription = p.ShortDescription,
        p.Slug,
        p.Price,
        p.OldPrice,
        p.StockQuantity,
        ThumbnailImageUrl = _mediaService.GetThumbnailUrl(p.ThumbnailImage, "320x320xi"),
        Imgs = new {
            Thumbnails = p.Medias.Select(m => _mediaService.GetThumbnailUrl(m, "320x320xi")).ToList(),
            Previews = p.Medias.Select(m => _mediaService.GetThumbnailUrl(m, "500x500xi")).ToList(),
            FullSize = p.Medias.Select(m => _mediaService.GetThumbnailUrl(m, "1500x1500xi")).ToList()
        },
        p.Attributes
    }).ToList();

    var groupedFilters = availableFiltersData
        .GroupBy(x => x.Name)
        .Select(g => new {
            Name = g.Key,
            Values = g.Select(v => v.Value).Distinct().OrderBy(x => x).ToList()
        }).ToList();

    return Ok(new { 
        page, 
        pageSize, 
        totalItems, 
        items,
        availableFilters = groupedFilters,
        priceRange = new { min = minCategoryPrice, max = maxCategoryPrice } 
    });
}

        // Helper Method for Category Recursion
        private List<long> GetCategoryAndChildrenIds(long categoryId)
{
    // 1. Fetch ALL categories into memory ONCE
    // We only select ID and ParentId to keep it extremely lightweight
    var allCategories = _categoryRepository.Query()
        .AsNoTracking()
        .Select(x => new { x.Id, x.ParentId })
        .ToList();

    var resultIds = new List<long>();
    
    // 2. Start the recursive search in memory (no more DB hits)
    Traverse(categoryId, allCategories, resultIds);
    
    return resultIds.Distinct().ToList();
}

private void Traverse(long parentId, IEnumerable<dynamic> allCategories, List<long> resultIds)
{
    resultIds.Add(parentId);
    
    // Find children in the local list
    var children = allCategories.Where(x => x.ParentId == parentId).Select(x => (long)x.Id);
    
    foreach (var childId in children)
    {
        Traverse(childId, allCategories, resultIds);
    }
}

        // Helper method to find all child IDs recursively
        // private List<long> GetCategoryAndChildrenIds(long categoryId)
        // {
        //     var ids = new List<long> { categoryId };
        //     var childrenIds = _categoryRepository.Query()
        //         .Where(x => x.ParentId == categoryId && !x.IsDeleted)
        //         .Select(x => x.Id)
        //         .ToList();

        //     foreach (var childId in childrenIds)
        //     {
        //         ids.AddRange(GetCategoryAndChildrenIds(childId)); // Recursive call for sub-sub-categories
        //     }

        //     return ids.Distinct().ToList();
        // }




        [HttpPut("update-product/{id}")]
        public async Task<IActionResult> UpdateProduct(long id, [FromBody] ProductCategoryForm model)
        {
            var productCategory = await _productCategoryRepository.Query().FirstOrDefaultAsync(x => x.Id == id);
            if (productCategory == null)
                return NotFound();

            productCategory.IsFeaturedProduct = model.IsFeaturedProduct;
            productCategory.DisplayOrder = model.DisplayOrder;

            await _productCategoryRepository.SaveChangesAsync();
            return Accepted();
        }

        #region Helpers

        private async Task SaveCategoryImage(Category category, CategoryForm model)
        {
            if (model.ThumbnailImage != null)
            {
                var fileName = await SaveFile(model.ThumbnailImage);

                if (category.ThumbnailImage != null)
                    category.ThumbnailImage.FileName = fileName;
                else
                    category.ThumbnailImage = new Media { FileName = fileName };
            }
        }

        private async Task<string> SaveFile(IFormFile file)
        {
            var originalFileName = ContentDispositionHeaderValue
                .Parse(file.ContentDisposition)
                .FileName
                .Value
                .Trim('"');

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}";
            await _mediaService.SaveMediaAsync(file.OpenReadStream(), fileName, file.ContentType);
            return fileName;
        }

        private async Task<bool> HaveCircularNesting(long childId, long parentId)
        {
            var category = await _categoryRepository.Query().FirstOrDefaultAsync(x => x.Id == parentId);
            var parentCategoryId = category.ParentId;

            while (parentCategoryId.HasValue)
            {
                if (parentCategoryId.Value == childId)
                    return true;

                var parentCategory = await _categoryRepository.Query().FirstAsync(x => x.Id == parentCategoryId);
                parentCategoryId = parentCategory.ParentId;
            }

            return false;
        }

        

        #endregion
    }
}
