using System;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SimplCommerce.Module.Core.Models;
using SimplCommerce.Module.Core.Areas.Core.ViewModels.Account;
using SimplCommerce.Module.Core.Areas.Core.ViewModels; // Added this
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SimplCommerce.Infrastructure.Data; // For IRepository
using Microsoft.EntityFrameworkCore; // For Include and ToListAsync

namespace SimplCommerce.Module.Core.Areas.Core.Controllers
{
    [Area("Core")]
    [Route("api/account")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class AccountApiController : ControllerBase
    {
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly IRepository<UserAddress> _userAddressRepository; 
    private readonly IRepository<Address> _addressRepository; 
    private readonly IRepositoryWithTypedId<Country, string> _countryRepository;
    private readonly IRepository<StateOrProvince> _stateOrProvinceRepository;
    private readonly IRepository<District> _districtRepository; // Added this field

    public AccountApiController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IConfiguration configuration,
        IRepository<UserAddress> userAddressRepository, 
        IRepository<Address> addressRepository,
        IRepositoryWithTypedId<Country, string> countryRepository, // Added this parameter
        IRepository<StateOrProvince> stateOrProvinceRepository,
        IRepository<District> districtRepository) // Removed underscores from parameter names
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _userAddressRepository = userAddressRepository;
        _addressRepository = addressRepository;
        _countryRepository = countryRepository; // Now matches parameter
        _stateOrProvinceRepository = stateOrProvinceRepository;
        _districtRepository = districtRepository;
    }

        [HttpPost("login")]
        [AllowAnonymous] // Login must be accessible without a token
        public async Task<IActionResult> Login([FromBody] LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return BadRequest(new { success = false, message = "Invalid login attempt" });

            // ✅ Use CheckPasswordSignInAsync instead of PasswordSignInAsync
            // This validates credentials without attempting to set an MVC Identity Cookie
            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
            
            if (!result.Succeeded)
                return BadRequest(new { success = false, message = "Invalid login attempt" });

            var token = await GenerateJwtToken(user);
            return Ok(new 
            { 
                success = true, 
                token, 
                fullName = user.FullName,
                id = user.Id
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // For JWT, the frontend "logs out" by deleting the token.
            // On the server, we simply clean up any persistent session data.
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            if (!string.IsNullOrEmpty(userId))
            {
                var currentUser = await _userManager.FindByIdAsync(userId);
                if (currentUser != null)
                {
                    // Clear refresh tokens if your implementation uses them
                    currentUser.RefreshToken = null; 
                    currentUser.RefreshTokenExpiryTime = null;
                    await _userManager.UpdateAsync(currentUser);
                }
            }

            return Ok(new { success = true, message = "Logged out successfully" });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = new User
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                UserGuid = Guid.NewGuid(),
                // Ensure a culture is set to avoid fallback to WorkContext defaults
                Culture = _configuration.GetValue<string>("Global.DefaultCultureUI") ?? "en-US"
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(new { success = false, errors = result.Errors });

            await _userManager.AddToRoleAsync(user, "customer");

            var token = await GenerateJwtToken(user);
            return Ok(new { success = true, token, id = user.Id, fullName = user.FullName  });
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // Security best practice: don't reveal if the user exists
                return Ok(new { success = true });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // In a decoupled setup, this URL should point to your Frontend React/Next.js URL
            var resetUrl = $"{_configuration["App:FrontendUrl"]}/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";

            await SendEmailAsync(user.Email, "Reset Password", $"Click here to reset your password: {resetUrl}");

            return Ok(new { success = true });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ApiResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return BadRequest(new { message = "Invalid request" });

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);

            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors });

            return Ok(new { success = true });
        }

        [HttpPost("change-fullname")]
        public async Task<IActionResult> ChangeFullName([FromBody] ChangeFullNameViewModel model)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.FullName))
            {
                return BadRequest(new { success = false, message = "Full name is required." });
            }

            // ✅ Get the User ID from the JWT Claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found." });
            }

            // ✅ Update the FullName
            user.FullName = model.FullName;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new { success = false, errors = result.Errors });
            }

            // ✅ Optional: If your frontend uses the name from the token, 
            // you might want to return a fresh token here.
            // For now, we return the updated name.
            return Ok(new 
            { 
                success = true, 
                message = "Full name updated successfully", 
                fullName = user.FullName 
            });
        }

        private async Task<string> GenerateJwtToken(User user)
        {
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), 
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? "")
            };

            var roleClaims = roles.Select(role => new Claim(ClaimTypes.Role, role));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                _configuration["Jwt:Issuer"],
                _configuration["Jwt:Audience"],
                claims: claims.Concat(roleClaims),
                expires: DateTime.UtcNow.AddHours(2),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task SendEmailAsync(string email, string subject, string message)
        {
            var smtpHost = _configuration["Smtp:Host"];
            var smtpPort = int.Parse(_configuration["Smtp:Port"] ?? "587");
            var smtpUser = _configuration["Smtp:User"];
            var smtpPass = _configuration["Smtp:Password"];
            
            using var client = new System.Net.Mail.SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new System.Net.NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };
            
            var mailMessage = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress(smtpUser, "SimplCommerce"),
                Subject = subject,
                Body = message,
                IsBodyHtml = true
            };
            
            mailMessage.To.Add(email);
            await client.SendMailAsync(mailMessage);
        }

        // Inside AccountApiController.cs

// GET: api/account/addresses
[HttpGet("addresses")]
public async Task<IActionResult> GetAddresses()
{
    var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
    if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out long userId))
        return Unauthorized();

    var userAddresses = await _userAddressRepository.Query()
        .Include(x => x.Address).ThenInclude(x => x.StateOrProvince)
        .Include(x => x.Address).ThenInclude(x => x.Country)
        .Include(x => x.Address).ThenInclude(x => x.District)
        .Where(x => x.UserId == userId)
        .ToListAsync();

    // SimplCommerce uses AddressType: 1 for Shipping, 2 for Billing (usually)
    // Or it might be an Enum like AddressType.Shipping
    var shipping = userAddresses.FirstOrDefault(x => x.AddressType == AddressType.Shipping);
    var billing = userAddresses.FirstOrDefault(x => x.AddressType == AddressType.Billing);

    return Ok(new AccountTaxAndShippingRequestVm
    {
        NewShippingAddress = shipping == null ? null : MapToShippingVm(shipping),
        NewBillingAddress = billing == null ? null : MapToShippingVm(billing),
        ExistingShippingAddressId = shipping?.AddressId ?? 0
    });
}

[HttpPost("addresses")]
public async Task<IActionResult> SaveAddresses([FromBody] AccountTaxAndShippingRequestVm model)
{
    var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
    if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out long userId))
        return Unauthorized();

    long? shippingAddressId = null;
    long? billingAddressId = null;

    if (model.NewShippingAddress != null)
    {
        shippingAddressId = await UpdateOrCreateAddress(userId, model.NewShippingAddress, isShipping: true);
    }

    if (model.NewBillingAddress != null)
    {
        billingAddressId = await UpdateOrCreateAddress(userId, model.NewBillingAddress, isShipping: false);
    }

    return Ok(new 
    { 
        success = true, 
        message = "Addresses updated successfully",
        shippingAddressId, // Returns the ID to frontend
        billingAddressId   // Returns the ID to frontend
    });
}

[HttpGet("countries/{countryId}/states-provinces")]
public async Task<IActionResult> GetStatesOrProvinces(string countryId)
{
    // Fetch states based on the country string ID
    var states = await _stateOrProvinceRepository.Query()
        .Where(x => x.CountryId == countryId)
        .OrderBy(x => x.Name)
        .Select(x => new 
        {
            Value = x.Id.ToString(),
            Text = x.Name
        })
        .ToListAsync();

    return Ok(states);
}

[HttpGet("states-provinces/{stateId}/districts")]
public async Task<IActionResult> GetDistricts(long stateId)
{
    // Fetch districts based on the state long ID
    var districts = await _districtRepository.Query()
        .Where(x => x.StateOrProvinceId == stateId)
        .OrderBy(x => x.Name)
        .Select(x => new 
        {
            Id = x.Id,
            Name = x.Name
        })
        .ToListAsync();

    return Ok(districts);
}

private async Task<long> UpdateOrCreateAddress(long userId, AccountShippingAddressVm vm, bool isShipping)
{
    var typeToFind = isShipping ? AddressType.Shipping : AddressType.Billing;

    var userAddress = await _userAddressRepository.Query()
        .Include(x => x.Address)
        .FirstOrDefaultAsync(x => x.UserId == userId && x.AddressType == typeToFind);

    if (userAddress == null)
    {
        var address = new Address
        {
            ContactName = vm.ContactName,
            Phone = vm.Phone,
            AddressLine1 = vm.AddressLine1,
            AddressLine2 = vm.AddressLine2,
            City = vm.City,
            ZipCode = vm.ZipCode,
            DistrictId = vm.DistrictId,
            StateOrProvinceId = vm.StateOrProvinceId,
            CountryId = vm.CountryId
        };

        userAddress = new UserAddress
        {
            Address = address,
            UserId = userId,
            AddressType = typeToFind
        };
        _userAddressRepository.Add(userAddress);
    }
    else
    {
        userAddress.Address.ContactName = vm.ContactName;
        userAddress.Address.Phone = vm.Phone;
        userAddress.Address.AddressLine1 = vm.AddressLine1;
        userAddress.Address.AddressLine2 = vm.AddressLine2;
        userAddress.Address.City = vm.City;
        userAddress.Address.ZipCode = vm.ZipCode;
        userAddress.Address.DistrictId = vm.DistrictId;
        userAddress.Address.StateOrProvinceId = vm.StateOrProvinceId;
        userAddress.Address.CountryId = vm.CountryId;
    }

    await _userAddressRepository.SaveChangesAsync();
    
    // Return the AddressId (The actual ID of the Address entity)
    return userAddress.AddressId; 
}

        private AccountShippingAddressVm MapToShippingVm(UserAddress userAddr)
        {
            var addr = userAddr.Address;
            return new AccountShippingAddressVm
            {
                UserAddressId = userAddr.Id,
                ContactName = addr.ContactName,
                Phone = addr.Phone,
                AddressLine1 = addr.AddressLine1,
                AddressLine2 = addr.AddressLine2,
                DistrictId = addr.DistrictId,
                DistrictName = addr.District?.Name,
                City = addr.City,
                ZipCode = addr.ZipCode,
                StateOrProvinceId = addr.StateOrProvinceId,
                StateOrProvinceName = addr.StateOrProvince?.Name,
                CountryId = addr.CountryId,
                CountryName = addr.Country?.Name
            };
        }
    }
    public class AccountTaxAndShippingRequestVm
    {
        public AccountShippingAddressVm NewShippingAddress { get; set; }
        public AccountShippingAddressVm NewBillingAddress { get; set; }
        public long? ExistingShippingAddressId { get; set; }
    }

    public class AccountShippingAddressVm
    {
        public long? UserAddressId { get; set; }
        public string ContactName { get; set; }
        public string Phone { get; set; }
        public string AddressLine1 { get; set; }
        public string AddressLine2 { get; set; }
        public long? DistrictId { get; set; }
        public string DistrictName { get; set; }
        public string ZipCode { get; set; }
        public long StateOrProvinceId { get; set; }
        public string StateOrProvinceName { get; set; }
        public string City { get; set; }
        public string CountryId { get; set; }
        public string CountryName { get; set; }
    }
}

