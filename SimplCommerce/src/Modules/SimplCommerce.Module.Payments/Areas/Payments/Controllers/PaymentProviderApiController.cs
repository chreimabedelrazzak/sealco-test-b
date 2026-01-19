using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Payments.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;


namespace SimplCommerce.Module.Payments.Areas.Payments.Controllers
{
    [Area("Payments")]
    [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/payments-providers")]
    public class PaymentProviderApiController : Controller
    {
        private readonly IRepositoryWithTypedId<PaymentProvider, string> _paymentProviderRepository;

        public PaymentProviderApiController(IRepositoryWithTypedId<PaymentProvider, string> paymentProviderRepositor)
        {
            _paymentProviderRepository = paymentProviderRepositor;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var providers = await _paymentProviderRepository.Query()
                .Where(x => x.IsEnabled) // ✅ Only show what is active
                .Select(x => new
                {
                    x.Id,           // e.g., "CashOnDelivery"
                    x.Name,         // e.g., "Cash on Delivery"
                    x.LandingViewComponentName // ✅ e.g., "CoD" or "Stripe" - Use this to switch components in React
                }).ToListAsync();

            return Ok(providers); // Use Ok() for standard API consistency
        }

        [HttpPost("{id}/enable")]
        public async Task<IActionResult> Enable(string id)
        {
            var provider = await _paymentProviderRepository.Query().FirstOrDefaultAsync(x => x.Id == id);
            provider.IsEnabled = true;
            await _paymentProviderRepository.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("{id}/disable")]
        public async Task<IActionResult> Disable(string id)
        {
            var provider = await _paymentProviderRepository.Query().FirstOrDefaultAsync(x => x.Id == id);
            provider.IsEnabled = false;
            await _paymentProviderRepository.SaveChangesAsync();
            return NoContent();
        }
    }
}
