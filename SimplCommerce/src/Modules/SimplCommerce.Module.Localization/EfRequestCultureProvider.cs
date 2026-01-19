// using System.Threading.Tasks;
// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Localization;
// using Microsoft.Extensions.DependencyInjection;
// using SimplCommerce.Module.Core.Extensions;

// namespace SimplCommerce.Module.Localization
// {
//     public class EfRequestCultureProvider : RequestCultureProvider
//     {
//         public override async Task<ProviderCultureResult> DetermineProviderCultureResult(HttpContext httpContext)
//         {
//             var workContext = httpContext.RequestServices.GetRequiredService<IWorkContext>();
//             var user = await workContext.GetCurrentUser();
//             var culture = user.Culture;

//             if (culture == null)
//             {
//                 return await Task.FromResult((ProviderCultureResult)null);
//             }

//             var providerResultCulture = new ProviderCultureResult(culture);

//             return await Task.FromResult(providerResultCulture);
//         }
//     }
// }

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.DependencyInjection;
using SimplCommerce.Module.Core.Extensions;

namespace SimplCommerce.Module.Localization
{
    public class EfRequestCultureProvider : RequestCultureProvider
    {
        public override async Task<ProviderCultureResult> DetermineProviderCultureResult(HttpContext httpContext)
        {
            var workContext = httpContext.RequestServices.GetRequiredService<IWorkContext>();
            var user = await workContext.GetCurrentUser();

            // ✅ FIX: Check if user is null before accessing properties
            // This happens now because we stopped auto-creating guest users for every request.
            if (user == null || string.IsNullOrEmpty(user.Culture))
            {
                // Returning null allows the middleware to move to the next provider 
                // (like CookieRequestCultureProvider or AcceptLanguageHeaderRequestCultureProvider)
                return null;
            }

            var providerResultCulture = new ProviderCultureResult(user.Culture);

            return providerResultCulture;
        }
    }
}