using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Core.Models;
using SimplCommerce.Module.Core.Services;
using SimplCommerce.Module.Orders.Areas.Orders.ViewModels;
using SimplCommerce.Module.Orders.Events;
using SimplCommerce.Module.Orders.Models;
using SimplCommerce.Module.Checkouts.Areas.Checkouts.ViewModels;

namespace SimplCommerce.Module.Orders.Areas.Orders.Controllers
{
    [Area("Orders")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/customer/orders")]
    public class CustomerOrderApiController : Controller
    {
        private readonly IRepository<Order> _orderRepository;
        private readonly IWorkContext _workContext;
        private readonly ICurrencyService _currencyService;
        private readonly IMediator _mediator; // Added this

        public CustomerOrderApiController(
            IRepository<Order> orderRepository, 
            IWorkContext workContext, 
            ICurrencyService currencyService,
            IMediator mediator) // Injected this
        {
            _orderRepository = orderRepository;
            _workContext = workContext;
            _currencyService = currencyService;
            _mediator = mediator;
        }

        [HttpGet("order-confirmation/{id}")]
        public async Task<IActionResult> GetOrderConfirmation(long id)
         {
            var order = _orderRepository
                .Query()
                .Include(x => x.ShippingAddress).ThenInclude(x => x.District)
                .Include(x => x.ShippingAddress).ThenInclude(x => x.StateOrProvince)
                .Include(x => x.ShippingAddress).ThenInclude(x => x.Country)
                .Include(x => x.OrderItems).ThenInclude(x => x.Product).ThenInclude(x => x.ThumbnailImage)
                .Include(x => x.OrderItems).ThenInclude(x => x.Product).ThenInclude(x => x.OptionCombinations).ThenInclude(x => x.Option)
                .Include(x => x.Customer)
                .FirstOrDefault(x => x.Id == id);

            if (order == null)
            {
                return NotFound();
            }

            var currentUser = await _workContext.GetCurrentUser();
            // if (!User.IsInRole("admin") && order.VendorId != currentUser.VendorId)
            // {
            //     return BadRequest(new { error = "You don't have permission to manage this order" });
            // }

            var model = new OrderDetailVm(_currencyService)
            {
                Id = order.Id,
                IsMasterOrder = order.IsMasterOrder,
                CreatedOn = order.CreatedOn,
                OrderStatus = (int)order.OrderStatus,
                OrderStatusString = order.OrderStatus.ToString(),
                CustomerId = order.CustomerId,
                CustomerName = order.Customer.FullName,
                CustomerEmail = order.Customer.Email,
                ShippingMethod = order.ShippingMethod,
                PaymentMethod = order.PaymentMethod,
                PaymentFeeAmount = order.PaymentFeeAmount,
                Subtotal = order.SubTotal,
                DiscountAmount = order.DiscountAmount,
                SubTotalWithDiscount = order.SubTotalWithDiscount,
                TaxAmount = order.TaxAmount,
                ShippingAmount = order.ShippingFeeAmount,
                OrderTotal = order.OrderTotal,
                OrderNote = order.OrderNote,
                ShippingAddress = new ShippingAddressVm
                {
                    AddressLine1 = order.ShippingAddress.AddressLine1,
                    City = order.ShippingAddress.City,
                    ZipCode = order.ShippingAddress.ZipCode,
                    ContactName = order.ShippingAddress.ContactName,
                    DistrictName = order.ShippingAddress.District?.Name,
                    StateOrProvinceName = order.ShippingAddress.StateOrProvince.Name,
                    Phone = order.ShippingAddress.Phone
                },
                OrderItems = order.OrderItems.Select(x => new OrderItemVm(_currencyService)
                {
                    Id = x.Id,
                    ProductId = x.Product.Id,
                    ProductName = x.Product.Name,
                    ProductPrice = x.ProductPrice,
                    Quantity = x.Quantity,
                    DiscountAmount = x.DiscountAmount,
                    TaxAmount = x.TaxAmount,
                    TaxPercent = x.TaxPercent,
                    VariationOptions = OrderItemVm.GetVariationOption(x.Product)
                }).ToList()
            };

            if (order.IsMasterOrder)
            {
                model.SubOrderIds = _orderRepository.Query().Where(x => x.ParentId == order.Id).Select(x => x.Id).ToList();
            }

            await _mediator.Publish(new OrderDetailGot { OrderDetailVm = model });

            return Json(model);
        }

        [HttpGet("users/{userId}")]
        public async Task<IActionResult> GetUserOrderHistory(long userId)
        {
            // 1. Identification: Get the currently authenticated user from the JWT/WorkContext
            var currentUser = await _workContext.GetCurrentUser();

            // 2. Security: Ensure the user is only requesting their own data
            // This prevents "User A" from accessing "User B's" order history
            // if (userId != currentUser.Id)
            // {
            //     return BadRequest(new { error = "You do not have permission to view these orders." });
            // }

            // 3. Query: Fetch orders belonging to this user, including necessary details for a list view
            var orders = await _orderRepository
                .Query()
                .Include(x => x.OrderItems).ThenInclude(x => x.Product)
                .Where(x => x.CustomerId == userId)
                .OrderByDescending(x => x.CreatedOn)
                .ToListAsync();

            // 4. Mapping: Convert to ViewModels using the injected _currencyService
            var model = orders.Select(order => new OrderDetailVm(_currencyService)
            {
                Id = order.Id,
                CreatedOn = order.CreatedOn,
                OrderStatus = (int)order.OrderStatus,
                OrderStatusString = order.OrderStatus.ToString(),
                OrderTotal = order.OrderTotal,
                Subtotal = order.SubTotal,
                ShippingAmount = order.ShippingFeeAmount,
                TaxAmount = order.TaxAmount,
                // Map a summary of items
                OrderItems = order.OrderItems.Select(x => new OrderItemVm(_currencyService)
                {
                    Id = x.Id,
                    ProductId = x.Product.Id,
                    ProductName = x.Product.Name,
                    ProductPrice = x.ProductPrice,
                    Quantity = x.Quantity
                }).ToList()
            }).ToList();

            return Json(model);
        }
    }

}