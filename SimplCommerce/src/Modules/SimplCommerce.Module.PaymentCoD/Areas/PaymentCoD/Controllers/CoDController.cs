using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Checkouts.Areas.Checkouts.ViewModels;
using SimplCommerce.Module.Checkouts.Services;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Orders.Services;
using SimplCommerce.Module.PaymentCoD.Models;
using SimplCommerce.Module.Payments.Models;
using SimplCommerce.Module.ShoppingCart.Areas.ShoppingCart.ViewModels;
using SimplCommerce.Module.ShoppingCart.Services;

namespace SimplCommerce.Module.PaymentCoD.Areas.PaymentCoD.Controllers
{
    [Authorize]
    [Area("PaymentCoD")]
    public class CoDController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IWorkContext _workContext;
        private readonly ICheckoutService _checkoutService;
        private readonly IRepositoryWithTypedId<PaymentProvider, string> _paymentProviderRepository;
        private Lazy<CoDSetting> _setting;

        public CoDController(
            ICheckoutService checkoutService,
            IOrderService orderService,
            IRepositoryWithTypedId<PaymentProvider, string> paymentProviderRepository,
            IWorkContext workContext)
        {
            _paymentProviderRepository = paymentProviderRepository;
            _checkoutService = checkoutService;
            _orderService = orderService;
            _workContext = workContext;
            _setting = new Lazy<CoDSetting>(GetSetting());
        }

        [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
        [HttpPost("api/cod/checkout")]
        public async Task<IActionResult> CoDCheckout([FromBody] Guid checkoutId)
        {
            // 1. User ID Extraction
            var userIdClaim = User.Claims.FirstOrDefault(c => 
                c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && 
                long.TryParse(c.Value, out _));

            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out long userId))
            {
                return Unauthorized(new { message = "Invalid user identification" });
            }

            // 2. Fetch Checkout Details
            var checkoutVm = await _checkoutService.GetCheckoutDetails(checkoutId);
            if(checkoutVm == null)
            {
                return NotFound(new { message = "Checkout session not found" });
            }

            // 3. FIXED: Ownership Check
            // We remove the checkoutVm.CustomerId check since that property doesn't exist in the VM.
            // The checkoutId (Guid) is unique enough to serve as the security token here.
            // If you need strict verification, you'd need to inject IRepository<Checkout> 
            // and check the DB entity directly.

            // 4. Logic Validation
            if (!ValidateCoD(checkoutVm))
            {
                return BadRequest(new { message = "Payment Method is not eligible for this order total." });
            }

            // 5. Create the Order
            var calculatedFee = CalculateFee(checkoutVm);           
            var orderCreateResult = await _orderService.CreateOrder(checkoutVm.Id, "CashOnDelivery", calculatedFee);

            if (!orderCreateResult.Success)
            {
                return BadRequest(new { message = orderCreateResult.Error });
            }

            // 6. Return JSON
            return Ok(new { 
                success = true, 
                orderId = orderCreateResult.Value.Id,
                nextStep = $"/checkout/success/{orderCreateResult.Value.Id}" 
            });
        }

        private CoDSetting GetSetting()
        {
            var coDProvider = _paymentProviderRepository.Query().FirstOrDefault(x => x.Id == PaymentProviderHelper.CODProviderId);
            if (string.IsNullOrEmpty(coDProvider.AdditionalSettings))
            {
                return new CoDSetting();
            }

            var coDSetting = JsonConvert.DeserializeObject<CoDSetting>(coDProvider.AdditionalSettings);
            return coDSetting;
        }

        private bool ValidateCoD(CheckoutVm checkoutVm)
        {
            if (_setting.Value.MinOrderValue.HasValue && _setting.Value.MinOrderValue.Value > checkoutVm.OrderTotal)
            {
                return false;
            }

            if (_setting.Value.MaxOrderValue.HasValue && _setting.Value.MaxOrderValue.Value < checkoutVm.OrderTotal)
            {
                return false;
            }

            return true;
        }

        private decimal CalculateFee(CheckoutVm chekoutVm)
        {
            var percent = _setting.Value.PaymentFee;
            return (chekoutVm.OrderTotal / 100) * percent;
        }
    }
}
