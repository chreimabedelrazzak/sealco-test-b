using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Newtonsoft.Json;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Checkouts.Areas.Checkouts.ViewModels;
using SimplCommerce.Module.Checkouts.Models;
using SimplCommerce.Module.Checkouts.Services;
using SimplCommerce.Module.Core.Extensions;
using SimplCommerce.Module.Core.Models;
using SimplCommerce.Module.ShoppingCart.Models;

namespace SimplCommerce.Module.Checkouts.Areas.Checkouts.Controllers
{
    [Area("Checkouts")]
    [Route("checkout")]
    [Authorize(AuthenticationSchemes = Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)]
    public class CheckoutController : Controller
    {
        private readonly IRepositoryWithTypedId<Country, string> _countryRepository;
        private readonly IRepository<StateOrProvince> _stateOrProvinceRepository;
        private readonly IRepository<UserAddress> _userAddressRepository;
        private readonly IRepository<District> _districtRepository;
        private readonly ICheckoutService _checkoutService;
        private readonly IRepository<CartItem> _cartItemRepository;
        private readonly IWorkContext _workContext;
        private readonly IRepositoryWithTypedId<Checkout, Guid> _checkoutRepository;
        private readonly UserManager<User> _userManager;
        

        public CheckoutController(
            IRepository<StateOrProvince> stateOrProvinceRepository,
            IRepositoryWithTypedId<Country, string> countryRepository,
            IRepository<UserAddress> userAddressRepository,
            IRepository<District> districtRepository,
            ICheckoutService checkout,
            IRepository<CartItem> cartItemRepository,
            IWorkContext workContext,
            IRepositoryWithTypedId<Checkout, Guid> checkoutRepository,
            UserManager<User> userManager)
        {
            _stateOrProvinceRepository = stateOrProvinceRepository;
            _countryRepository = countryRepository;
            _districtRepository = districtRepository;
            _userAddressRepository = userAddressRepository;
            _checkoutService = checkout;
            _cartItemRepository = cartItemRepository;
            _workContext = workContext;
            _checkoutRepository = checkoutRepository;
            _userManager = userManager;
        }

        //TODO: Consider to allow customer select a subset of products in cart and pass to this endpoint
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CheckoutFormVm checkoutFormVm)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized(new { message = "User is not authenticated via JWT" });
            }

            // ✅ FIX: Find all 'nameidentifier' claims and pick the one that is a valid number
            var userIdClaim = User.Claims.FirstOrDefault(c => 
                c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && 
                long.TryParse(c.Value, out _));

            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out long userId))
            {
                return Unauthorized(new { 
                    message = "Invalid user identification in token",
                    debug_info = "The system found an email instead of a numeric ID in the nameidentifier claim."
                });
            }

            // Now proceed with fetching the user and creating the checkout
            var currentUser = await _userManager.FindByIdAsync(userId.ToString());
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not found in database" });
            }

            var cartItems = await _cartItemRepository.Query()
                .Where(x => x.CustomerId == userId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                return BadRequest(new { message = "Your cart is empty" });
            }

            var cartItemToCheckouts = cartItems.Select(x => new CartItemToCheckoutVm
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity
            }).ToList();

            var checkout = await _checkoutService.Create(
                userId, 
                userId, 
                cartItemToCheckouts, 
                checkoutFormVm?.CouponCode
            );

            return Ok(new { checkoutId = checkout.Id }); 
        }

        [HttpGet("{checkoutId}/shipping")]
        public async Task<IActionResult> Shipping(Guid checkoutId)
        {
            // 1. Robust User ID Extraction from JWT Claims
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value 
                            ?? User.FindFirst("sub")?.Value;

            // Handle the double 'nameidentifier' issue found earlier
            if (string.IsNullOrEmpty(userIdString) || !long.TryParse(userIdString, out _))
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => 
                    c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && 
                    long.TryParse(c.Value, out _));
                userIdString = userIdClaim?.Value;
            }

            if (string.IsNullOrEmpty(userIdString) || !long.TryParse(userIdString, out long userId))
            {
                return Unauthorized(new { message = "Invalid user identification in token" });
            }

            // 2. Fetch the full User object from DB
            var currentUser = await _userManager.FindByIdAsync(userId.ToString());
            if (currentUser == null)
            {
                return Unauthorized(new { message = "User not found in database" });
            }

            // 3. Retrieve the Checkout and verify ownership
            var checkout = await _checkoutRepository.Query().FirstOrDefaultAsync(x => x.Id == checkoutId);
            if (checkout == null)
            {
                return NotFound();
            }

            // Use CustomerId (the long ID) for verification instead of object comparison
            if (checkout.CustomerId != currentUser.Id)
            {
                return Forbid();
            }

            // 4. Prepare the View Model
            var model = new DeliveryInformationVm();
            model.CheckoutId = checkoutId;

            // Populate form (addresses, countries, etc.)
            PopulateShippingForm(model, currentUser);

            // 5. Return JSON for API compatibility
            return Ok(model);
        }

        [AllowAnonymous] // Or [Authorize] depending on your needs
        [HttpGet("api/states/{stateId}/districts")]
        public async Task<IActionResult> GetDistricts(long stateId)
        {
            var districts = await _districtRepository.Query()
                .Where(x => x.StateOrProvinceId == stateId)
                .OrderBy(x => x.Name)
                .Select(x => new {
                    Id = x.Id,
                    Name = x.Name
                }).ToListAsync();

            return Ok(districts);
        }

        [HttpPost("{checkoutId}/shipping")]
        public async Task<IActionResult> Shipping(Guid checkoutId, [FromBody] DeliveryInformationVm model)
        {
            var userId = GetUserIdFromClaims(); 
            
            var checkout = await _checkoutRepository.Query().FirstOrDefaultAsync(x => x.Id == checkoutId);
            if (checkout == null) return NotFound();
            if (checkout.CustomerId != userId) return Forbid();

            // 2. Validation Check (Added null check for NewBillingAddressForm to prevent crash)
            if ((!model.NewAddressForm.IsValid() && model.ShippingAddressId == 0) ||
                (!model.UseShippingAddressAsBillingAddress && model.BillingAddressId == 0 && (model.NewBillingAddressForm == null || !model.NewBillingAddressForm.IsValid())))
            {
                return BadRequest(new { message = "Please provide a valid address", model });
            }

            // 3. Update the database fields
            // ✅ FIX: Populate the specific ShippingMethod column
            checkout.ShippingMethod = model.ShippingMethod; 

            // Save the full blob for the OrderService to read address details later
            checkout.ShippingData = JsonConvert.SerializeObject(model);
            
            await _checkoutRepository.SaveChangesAsync();

            return Ok(new { 
                success = true, 
                nextStep = $"/checkout/{checkoutId}/payment" 
            });
        }

        [HttpPost("{checkoutId}/update-tax-and-shipping-prices")]
        public async Task<IActionResult> UpdateTaxAndShippingPrices(Guid checkoutId, [FromBody] TaxAndShippingPriceRequestVm model)
        {
            // 1. Get User ID from JWT (using the numeric filtering we established)
            var userIdClaim = User.Claims.FirstOrDefault(c => 
                c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && 
                long.TryParse(c.Value, out _));

            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out long userId))
            {
                return Unauthorized();
            }

            // 2. Find Checkout
            var checkout = await _checkoutRepository.Query().FirstOrDefaultAsync(x => x.Id == checkoutId);
            if(checkout == null)
            {
                return NotFound();
            }

            // 3. Security Check (using ID instead of object comparison)
            if (checkout.CustomerId != userId)
            {
                return Forbid();
            }

            // 4. Update the prices via the service
            var orderTaxAndShippingPrice = await _checkoutService.UpdateTaxAndShippingPrices(checkoutId, model);

            // 5. Return JSON (already returns Ok, which is perfect for React)
            return Ok(orderTaxAndShippingPrice);
        }


        [HttpGet("success/{orderId}")]
        public IActionResult Success(long orderId)
        {
            // Simple response for React to handle navigation
            return Ok(new { orderId = orderId });
        }

        [HttpGet("error/{orderId}")]
        public IActionResult Error(long orderId)
        {
            return Ok(new { orderId = orderId, message = "Payment failed" });
        }

        private long GetUserIdFromClaims()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => 
                c.Type == System.Security.Claims.ClaimTypes.NameIdentifier && 
                long.TryParse(c.Value, out _));

            return (userIdClaim != null && long.TryParse(userIdClaim.Value, out long userId)) ? userId : 0;
        }

        private void PopulateShippingForm(DeliveryInformationVm model, User currentUser)
        {
            model.ExistingShippingAddresses = _userAddressRepository.Query()
                .Where(x => x.AddressType == AddressType.Shipping && x.UserId == currentUser.Id)
                .Select(x => new ShippingAddressVm {
                    UserAddressId = x.Id,
                    ContactName = x.Address.ContactName,
                    Phone = x.Address.Phone,
                    AddressLine1 = x.Address.AddressLine1,
                    AddressLine2 = x.Address.AddressLine2,
                    City = x.Address.City,
                    ZipCode = x.Address.ZipCode,
                    DistrictId = x.Address.District.Id,
                    DistrictName = x.Address.District.Name,
                    StateOrProvinceId = x.Address.StateOrProvince.Id,
                    StateOrProvinceName = x.Address.StateOrProvince.Name,
                    CountryId = x.Address.Country.Id,
                    CountryName = x.Address.Country.Name,
                    IsCityEnabled = x.Address.Country.IsCityEnabled,
                    IsZipCodeEnabled = x.Address.Country.IsZipCodeEnabled,
                    IsDistrictEnabled = x.Address.Country.IsDistrictEnabled
                }).ToList();
            
            model.ExistingBillingAddresses = _userAddressRepository.Query()
                .Where(x => x.AddressType == AddressType.Billing && x.UserId == currentUser.Id)
                .Select(x => new BillingAddressVm {
                    UserAddressId = x.Id,
                    ContactName = x.Address.ContactName,
                    Phone = x.Address.Phone,
                    AddressLine1 = x.Address.AddressLine1,
                    AddressLine2 = x.Address.AddressLine2,
                    City = x.Address.City,
                    ZipCode = x.Address.ZipCode,
                    DistrictId = x.Address.District.Id,
                    DistrictName = x.Address.District.Name,
                    StateOrProvinceId = x.Address.StateOrProvince.Id,
                    StateOrProvinceName = x.Address.StateOrProvince.Name,
                    CountryId = x.Address.Country.Id,
                    CountryName = x.Address.Country.Name,
                    IsCityEnabled = x.Address.Country.IsCityEnabled,
                    IsZipCodeEnabled = x.Address.Country.IsZipCodeEnabled,
                    IsDistrictEnabled = x.Address.Country.IsDistrictEnabled
                }).ToList();

            model.ShippingAddressId = currentUser.DefaultShippingAddressId ?? 0;
            model.UseShippingAddressAsBillingAddress = true;
            model.NewAddressForm.ShipableContries = _countryRepository.Query()
                .Where(x => x.IsShippingEnabled).OrderBy(x => x.Name)
                .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() }).ToList();

            if (model.NewAddressForm.ShipableContries.Count == 1)
            {
                var countryId = model.NewAddressForm.ShipableContries.First().Value;
                model.NewAddressForm.StateOrProvinces = _stateOrProvinceRepository.Query()
                    .Where(x => x.CountryId == countryId).OrderBy(x => x.Name)
                    .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() }).ToList();
            }
            if (model.NewAddressForm.StateOrProvinces != null && model.NewAddressForm.StateOrProvinces.Count == 1)
            {
                var stateId = long.Parse(model.NewAddressForm.StateOrProvinces.First().Value);
                model.NewAddressForm.Districts = _districtRepository.Query()
                    .Where(x => x.StateOrProvinceId == stateId)
                    .OrderBy(x => x.Name)
                    .Select(x => new SelectListItem 
                    { 
                        Text = x.Name, 
                        Value = x.Id.ToString() 
                    }).ToList();
            }
        }
    }
}