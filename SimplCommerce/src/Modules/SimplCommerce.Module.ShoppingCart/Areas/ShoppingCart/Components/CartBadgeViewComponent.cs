using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SimplCommerce.Infrastructure.Web;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Core.Services;
using SimplCommerce.Module.ShoppingCart.Areas.ShoppingCart.ViewModels;
using SimplCommerce.Module.ShoppingCart.Services;

namespace SimplCommerce.Module.ShoppingCart.Areas.ShoppingCart.Components
{
    public class CartBadgeViewComponent : ViewComponent
    {
        private ICartService _cartService;
        private IWorkContext _workContext;
        private ICurrencyService _currencyService;

        public CartBadgeViewComponent(ICartService cartService, IWorkContext workContext, ICurrencyService currencyService)
        {
            _cartService = cartService;
            _workContext = workContext;
            _currencyService = currencyService;
        }

       public async Task<IViewComponentResult> InvokeAsync()
        {
            var currentUser = await _workContext.GetCurrentUser();
            
            // 1. If no user is found, return a count of 0 immediately
            if (currentUser == null)
            {
                return View(this.GetViewPath(), 0);
            }

            // 2. Since we know currentUser isn't null, we can safely access .Id
            var cart = await _cartService.GetCartDetails(currentUser.Id);
            
            if (cart == null)
            {
                cart = new CartVm(_currencyService);
            }
            
            return View(this.GetViewPath(), cart.Items.Count);
        }
    }
}
