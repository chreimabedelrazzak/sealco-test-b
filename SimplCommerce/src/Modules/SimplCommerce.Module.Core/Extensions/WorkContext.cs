using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Infrastructure;
using SimplCommerce.Module.Core.Models;
using Microsoft.Extensions.Configuration;

namespace SimplCommerce.Module.Core.Extensions
{
    public class WorkContext : IWorkContext
    {
        private const string UserGuidCookiesName = "SimplUserGuid";
        private const long GuestRoleId = 3;

        private User _currentUser;
        private UserManager<User> _userManager;
        private HttpContext _httpContext;
        private IRepository<User> _userRepository;
        private readonly IConfiguration _configuration;

        public WorkContext(UserManager<User> userManager,
                           IHttpContextAccessor contextAccessor,
                           IRepository<User> userRepository,
                           IConfiguration configuration)
        {
            _userManager = userManager;
            _httpContext = contextAccessor.HttpContext;
            _userRepository = userRepository;
            _configuration = configuration;
        }

        public string GetCurrentHostName() => _httpContext.Request.Host.Value;

        public async Task<User> GetCurrentUser()
        {
            if (_currentUser != null)
            {
                return _currentUser;
            }

            // 1. Try to get user from the JWT/Identity context
            var contextUser = _httpContext.User;
            if (contextUser.Identity.IsAuthenticated)
            {
                _currentUser = await _userManager.GetUserAsync(contextUser);
            }

            if (_currentUser != null)
            {
                return _currentUser;
            }

            // 2. Optional: Only check for existing Guests if your business logic requires it.
            // We remove the "Auto-Create" section at the bottom to stop the database bloat.
            var userGuid = GetUserGuidFromCookies();
            if (userGuid.HasValue)
            {
                _currentUser = _userRepository.Query()
                    .Include(x => x.Roles)
                    .FirstOrDefault(x => x.UserGuid == userGuid);
            }

            // 3. Return the user if found (Real or existing Guest), 
            // otherwise return null. Do NOT create a new guest here.
            return _currentUser; 
        }

        private Guid? GetUserGuidFromCookies()
        {
            if (_httpContext.Request.Cookies.ContainsKey(UserGuidCookiesName))
            {
                return Guid.Parse(_httpContext.Request.Cookies[UserGuidCookiesName]);
            }

            return null;
        }

        private void SetUserGuidCookies()
        {
            _httpContext.Response.Cookies.Append(UserGuidCookiesName, _currentUser.UserGuid.ToString(), new CookieOptions
            {
                Expires = DateTime.UtcNow.AddYears(5),
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Strict
            });
        }
    }
}
