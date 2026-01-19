using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.ShoppingCart.Areas.ShoppingCart.ViewModels;
using SimplCommerce.Module.ShoppingCart.Models;
using SimplCommerce.Module.ShoppingCart.Services;

namespace SimplCommerce.Module.ShoppingCart.Areas.ShoppingCart.Controllers
{
    [Area("ShoppingCart")]
    [ApiController]
    [Route("api")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class CartApiController : ControllerBase
    {
        private readonly IRepository<CartItem> _cartItemRepository;
        private readonly ICartService _cartService;
        private readonly IWorkContext _workContext;

        public CartApiController(
            IRepository<CartItem> cartItemRepository,
            ICartService cartService,
            IWorkContext workContext)
        {
            _cartItemRepository = cartItemRepository;
            _cartService = cartService;
            _workContext = workContext;
        }

        [HttpGet("customers/{customerId}/cart")]
        public async Task<IActionResult> List(long customerId)
        {
            var cart = await _cartService.GetCartDetails(customerId);
            return Ok(cart);
        }

        [HttpPost("customers/{customerId}/add-cart-item")]
        public async Task<IActionResult> AddToCart(
            long customerId,
            [FromBody] AddToCartModel model)
        {
            var result = await _cartService.AddToCart(
                customerId,
                model.ProductId,
                model.Quantity);

            if (result.Success)
    {
                // Fetch the created/updated item to get its Database ID (itemId)
                var cartItem = await _cartItemRepository.Query()
                    .FirstOrDefaultAsync(x => x.ProductId == model.ProductId && x.CustomerId == customerId);

                return Ok(new
                {
                    Success = true,
                    ItemId = cartItem?.Id, // This is what your frontend calls 'itemId'
                    ProductId = model.ProductId
                });
            }

            return BadRequest(new
            {
                ErrorCode = result.ErrorCode,
                Message = result.ErrorMessage
            });
        }

        [HttpPut("carts/items/{itemId}")]
        public async Task<IActionResult> UpdateQuantity(
            long itemId,
            [FromBody] CartQuantityUpdate model)
        {
            var currentUser = await _workContext.GetCurrentUser();

            var cartItem = await _cartItemRepository.Query()
                .FirstOrDefaultAsync(x =>
                    x.Id == itemId &&
                    x.CustomerId == currentUser.Id);

            if (cartItem == null)
            {
                return NotFound();
            }

            cartItem.Quantity = model.Quantity;
            cartItem.LatestUpdatedOn = DateTimeOffset.Now;

            await _cartItemRepository.SaveChangesAsync();
            return Accepted();
        }

        [HttpDelete("carts/items/{itemId}")]
        public async Task<IActionResult> Remove(long itemId)
        {
            var currentUser = await _workContext.GetCurrentUser();

            var cartItem = await _cartItemRepository.Query()
                .FirstOrDefaultAsync(x =>
                    x.Id == itemId &&
                    x.CustomerId == currentUser.Id);

            if (cartItem == null)
            {
                return NotFound();
            }

            _cartItemRepository.Remove(cartItem);
            await _cartItemRepository.SaveChangesAsync();

            return NoContent();
        }

        // DELETE: /api/customers/{customerId}/cart-items/{itemId}
        [HttpDelete("customers/{customerId}/cart-items/{itemId}")]
        public async Task<IActionResult> RemoveForCustomer(long customerId, long itemId)
        {
            // 1. Find the item and ensure it belongs to the specified customer
            var cartItem = await _cartItemRepository.Query()
                .FirstOrDefaultAsync(x => x.Id == itemId && x.CustomerId == customerId);

            if (cartItem == null)
            {
                return NotFound(new { Message = $"Cart item {itemId} not found for customer {customerId}" });
            }

            // 2. Perform removal
            _cartItemRepository.Remove(cartItem);
            await _cartItemRepository.SaveChangesAsync();

            return NoContent();
        }
    }
}
