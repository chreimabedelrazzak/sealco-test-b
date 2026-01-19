using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Catalog.Models;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Core.Services;
using SimplCommerce.Module.WishList.Areas.WishList.ViewModels;
using SimplCommerce.Module.WishList.Models;

namespace SimplCommerce.Module.WishList.Areas.WishList.Controllers
{
    [Area("WishList")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/wishlist")]
    public class CustomerWishListApiController : Controller
    {
        private readonly IRepository<Models.WishList> _wishListRepository;
        private readonly IRepository<WishListItem> _wishListItemRepository;
        private readonly IRepository<Product> _productRepository;
        private readonly IMediaService _mediaService;

        public CustomerWishListApiController(
            IRepository<Models.WishList> wishListRepository,
            IRepository<WishListItem> wishListItemRepository,
            IRepository<Product> productRepository,
            IMediaService mediaService)
        {
            _wishListRepository = wishListRepository;
            _wishListItemRepository = wishListItemRepository;
            _productRepository = productRepository;
            _mediaService = mediaService;
        }

        // 1. GET: Retrieve products in wishlist for a specific user
        [HttpGet("{userId}")]
        public async Task<IActionResult> GetWishlistByUserId(long userId)
        {
            var wishList = await _wishListRepository.Query()
                .Include(x => x.Items).ThenInclude(x => x.Product).ThenInclude(x => x.ThumbnailImage)
                .SingleOrDefaultAsync(x => x.UserId == userId);

            if (wishList == null)
            {
                // Returning an empty list directly to avoid WishListVm type mismatch errors
                return Json(new Array[] { }); 
            }

            var wishlistItems = wishList.Items.Select(x => new WishListItemVm
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductName = x.Product.Name,
                Price = x.Product.Price,
                OldPrice = x.Product.OldPrice ?? 0m,
                ProductImage = _mediaService.GetThumbnailUrl(x.Product.ThumbnailImage),
                Quantity = x.Quantity
            }).ToList();

            return Json(wishlistItems);
        }

        // 2. POST: Add product to wishlist for a specific user via route ID
        [HttpPost("add-item/{userId}")]
        public async Task<IActionResult> AddItemByUserId(long userId, [FromBody] AddToWishList model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var product = await _productRepository.Query().AnyAsync(x => x.Id == model.ProductId);
            if (!product)
            {
                return NotFound(new { Message = "Product not found" });
            }
            
            var wishList = await _wishListRepository.Query()
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.UserId == userId);

            if (wishList == null)
            {
                wishList = new Models.WishList { UserId = userId };
                _wishListRepository.Add(wishList);
                await _wishListRepository.SaveChangesAsync();
            }

            var existingItem = wishList.Items.FirstOrDefault(x => x.ProductId == model.ProductId);
            if (existingItem != null)
            {
                return BadRequest(new { Message = "Product already exists in your wishlist" });
            }

            var wishListItem = new WishListItem
            {
                WishListId = wishList.Id,
                ProductId = model.ProductId,
                Quantity = model.Quantity
            };

            _wishListItemRepository.Add(wishListItem);
            wishList.LatestUpdatedOn = DateTimeOffset.Now;
            await _wishListRepository.SaveChangesAsync();

            return Ok(new { Message = "Product added successfully" });
        }

        // 3. DELETE: Remove product from wishlist for a specific user
        [HttpDelete("remove-item/{userId}/{productId}")]
        public async Task<IActionResult> RemoveItemByProductId(long userId, long productId)
        {
            // Find the wishlist belonging to this user
            var wishList = await _wishListRepository.Query()
                .Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.UserId == userId);

            if (wishList == null)
            {
                return NotFound(new { Message = "Wishlist not found" });
            }

            // Find the specific item in that wishlist
            var itemToRemove = wishList.Items.FirstOrDefault(x => x.ProductId == productId);

            if (itemToRemove == null)
            {
                return NotFound(new { Message = "Product not found in your wishlist" });
            }

            // Remove the item
            _wishListItemRepository.Remove(itemToRemove);
            wishList.LatestUpdatedOn = DateTimeOffset.Now;
            
            await _wishListItemRepository.SaveChangesAsync();

            return Ok(new { Message = "Product removed successfully" });
        }
    }
}