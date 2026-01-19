using Microsoft.AspNetCore.Mvc;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Catalog.Models;
using SimplCommerce.Module.Catalog.Areas.Catalog.Models;
using System;
using System.Linq;
using System.Collections.Generic;

namespace SimplCommerce.Module.Catalog.Areas.Catalog.Controllers
{
    [Area("Catalog")]
    [ApiController]
    [Route("api/menu")]
    public class MenuController : ControllerBase
    {
        private readonly IRepository<Menu> _menuRepository;
        private readonly IRepository<MenuItem> _menuItemRepository;

        public MenuController(
            IRepository<Menu> menuRepository,
            IRepository<MenuItem> menuItemRepository)
        {
            _menuRepository = menuRepository;
            _menuItemRepository = menuItemRepository;
        }

        // ======================================================
        // GET api/menu/type/{menuTypeId}
        // menuTypeId = -1 => ALL MENUS
        // ======================================================
        [HttpGet("type/{menuTypeId:long}")]
        public IActionResult GetMenuByType(long menuTypeId)
        {
            var query = _menuRepository
                .Query()
                .Where(m => m.IsActive);

            if (menuTypeId != -1)
                query = query.Where(m => m.MenuTypeId == menuTypeId);

            var menus = query
                .Select(m => new MenuDto
                {
                    Id = m.Id,
                    Name = m.Name,
                    MenuTypeId = m.MenuTypeId,
                    IsActive = m.IsActive,
                    Items = m.MenuItems
                        .Where(mi => mi.IsActive)
                        .OrderBy(mi => mi.Position)
                        .Select(mi => new MenuItemDto
                        {
                            Id = mi.Id,
                            ParentId = mi.ParentId,
                            MenuItemTypeId = mi.MenuItemTypeId,
                            EntityId = mi.EntityId,
                            TitleEn = mi.TitleEn,
                            TitleAr = mi.TitleAr,
                            Url = mi.Url,
                            Position = mi.Position,
                            IsActive = mi.IsActive,
                            IsDeleted = false
                        })
                        .ToList()
                })
                .ToList();

            if (!menus.Any())
                return NotFound();

            // If requesting a single menu type, return one object
            if (menuTypeId != -1)
                return Ok(menus.First());

            return Ok(menus);
        }

        // ======================================================
        // POST api/menu
        // CREATE MENU (flat structure)
        // ======================================================
        [HttpPost]
        public IActionResult CreateMenu([FromBody] MenuCreateDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var menu = new Menu
            {
                Name = dto.Name,
                Code = dto.Code,
                MenuTypeId = dto.MenuTypeId,
                IsActive = dto.IsActive,
                CreatedOn = DateTime.UtcNow
            };

            _menuRepository.Add(menu);
            _menuRepository.SaveChanges();

            foreach (var itemDto in dto.Items)
            {
                if (itemDto.IsDeleted)
                    continue;

                var item = new MenuItem
                {
                    MenuId = menu.Id,
                    ParentId = itemDto.ParentId,
                    MenuItemTypeId = itemDto.MenuItemTypeId,
                    EntityId = itemDto.EntityId,
                    TitleEn = itemDto.TitleEn,
                    TitleAr = itemDto.TitleAr,
                    Url = itemDto.Url,
                    Position = itemDto.Position,
                    IsActive = itemDto.IsActive,
                    CreatedOn = DateTime.UtcNow
                };

                _menuItemRepository.Add(item);
            }

            _menuRepository.SaveChanges();
            return Ok(new { menu.Id });
        }

        // ======================================================
        // PUT api/menu/{menuId}
        // UPDATE MENU
        // SAME STRUCTURE AS GET RESPONSE
        // ======================================================
        [HttpPut("{menuId:long}")]
        public IActionResult UpdateMenu(long menuId, [FromBody] MenuDto dto)
        {
            var menu = _menuRepository.Query().FirstOrDefault(m => m.Id == menuId);
            if (menu == null)
                return NotFound();

            menu.Name = dto.Name;
            menu.IsActive = dto.IsActive;
            menu.ModifiedOn = DateTime.UtcNow;

            var existingItems = _menuItemRepository
                .Query()
                .Where(mi => mi.MenuId == menuId)
                .ToList();

            foreach (var itemDto in dto.Items ?? new List<MenuItemDto>())
            {
                // =========================
                // DELETE
                // =========================
                if (itemDto.IsDeleted && itemDto.Id > 0)
                {
                    var toDelete = existingItems.FirstOrDefault(x => x.Id == itemDto.Id);
                    if (toDelete != null)
                        _menuItemRepository.Remove(toDelete);

                    continue;
                }

                MenuItem item;

                // =========================
                // UPDATE EXISTING
                // =========================
                if (itemDto.Id > 0)
                {
                    item = existingItems.FirstOrDefault(x => x.Id == itemDto.Id);
                    if (item == null)
                        continue;
                }
                // =========================
                // ADD NEW (THIS WAS FAILING)
                // =========================
                else
                {
                    item = new MenuItem
                    {
                        MenuId = menuId,
                        CreatedOn = DateTime.UtcNow
                    };

                    _menuItemRepository.Add(item);
                }

                // =========================
                // SAFE PARENT ASSIGNMENT
                // =========================
                if (itemDto.ParentId.HasValue &&
                    !existingItems.Any(x => x.Id == itemDto.ParentId))
                {
                    item.ParentId = null; // prevent FK crash
                }
                else
                {
                    item.ParentId = itemDto.ParentId;
                }

                item.MenuItemTypeId = itemDto.MenuItemTypeId;
                item.EntityId = itemDto.EntityId;
                item.TitleEn = itemDto.TitleEn;
                item.TitleAr = itemDto.TitleAr;
                item.Url = itemDto.Url;
                item.Position = itemDto.Position;
                item.IsActive = itemDto.IsActive;
                item.ModifiedOn = DateTime.UtcNow;
            }

            // ✅ SAVE BOTH
            _menuItemRepository.SaveChanges();
            _menuRepository.SaveChanges();

            return Ok(new { menu.Id });
        }


    }
}
