﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MVCCoreVue.Data;
using MVCCoreVue.Data.Attributes;
using MVCCoreVue.Extensions;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace MVCCoreVue.Controllers
{
    [Authorize]
    [Route("api/[controller]/{dataType}/[action]")]
    public class DataController : Controller
    {
        private readonly ILogger<AccountController> _logger;
        private readonly ApplicationDbContext _context;

        public DataController(ILogger<AccountController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Add(string dataType, [FromBody]JObject item)
        {
            if (!TryResolveObject(dataType, item, out object obj, out Type type))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            IRepository repository = GetRepository(type);
            try
            {
                var newItem = await repository.AddAsync(obj);
                return Json(newItem);
            }
            catch
            {
                return Json(new { error = "Item could not be saved." });
            }
        }

        [HttpPost("{id}/{childProp}")]
        public async Task<IActionResult> AddChild(string dataType, string id, string childProp)
        {
            if (!TryGetRepository(dataType, out IRepository repository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (!Guid.TryParse(id, out Guid guid))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (string.IsNullOrEmpty(childProp))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            object item = null;
            try
            {
                item = await repository.FindItemAsync(guid);
            }
            catch
            {
                return Json(new { error = "Item could not be accessed." });
            }
            if (item == null)
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/404" });
            }
            var childTypeInfo = item.GetType().GetTypeInfo();
            var pInfo = childTypeInfo.GetProperty(childProp.ToInitialCaps());
            if (pInfo == null)
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            var idPInfo = childTypeInfo.GetProperty($"{childProp.ToInitialCaps()}Id");
            if (idPInfo == null)
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (!TryGetRepository(pInfo.PropertyType.Name, out IRepository childRepository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            try
            {
                var newChild = await childRepository.NewAsync();
                var updatedItem = await repository.AddChildAsync(item, newChild, pInfo, idPInfo);
                return Json(updatedItem);
            }
            catch
            {
                return Json(new { error = "Item could not be added." });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Find(string dataType, string id)
        {
            if (!TryGetRepository(dataType, out IRepository repository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (!Guid.TryParse(id, out Guid guid))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            object item = null;
            try
            {
                item = await repository.FindAsync(guid);
            }
            catch
            {
                return Json(new { error = "Item could not be accessed." });
            }
            if (item == null)
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/404" });
            }
            return Json(item);
        }

        [HttpGet]
        public IActionResult GetAll(string dataType)
        {
            if (!TryGetRepository(dataType, out IRepository repository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            return Json(repository.GetAll());
        }

        [AllowAnonymous]
        [HttpGet("/api/[controller]/[action]")]
        public IActionResult GetChildTypes()
        {
            try
            {
                var types = _context.Model.GetEntityTypes();
                IDictionary<string, dynamic> classes = new Dictionary<string, dynamic>();
                foreach (var type in types)
                {
                    var attr = type.ClrType.GetTypeInfo().GetCustomAttribute<MenuClassAttribute>();
                    if (attr == null)
                    {
                        var childAttr = type.ClrType.GetTypeInfo().GetCustomAttribute<ChildClassAttribute>();
                        var category = string.IsNullOrEmpty(childAttr?.Category) ? "/" : childAttr?.Category;
                        classes.Add(type.ClrType.Name, new { category = category });
                    }
                }
                return Json(classes);
            }
            catch
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
        }

        [HttpGet]
        public IActionResult GetFieldDefinitions(string dataType)
        {
            if (!TryGetRepository(dataType, out IRepository repository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            try
            {
                return Json(repository.GetFieldDefinitions());
            }
            catch
            {
                return Json(new { error = "Data could not be retrieved." });
            }
        }

        [HttpGet]
        public IActionResult GetPage(
            string dataType,
            string search,
            string sortBy,
            bool descending,
            int page,
            int rowsPerPage)
        {
            if (!TryGetRepository(dataType, out IRepository repository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            return Json(repository.GetPage(search, sortBy, descending, page, rowsPerPage));
        }

        [HttpGet]
        public async Task<IActionResult> GetTotal(string dataType)
        {
            if (!TryGetRepository(dataType, out IRepository repository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            var total = await repository.GetTotalAsync();
            return Json(new { response = total });
        }

        [AllowAnonymous]
        [HttpGet("/api/[controller]/[action]")]
        public IActionResult GetTypes()
        {
            try
            {
                var types = _context.Model.GetEntityTypes();
                IDictionary<string, dynamic> classes = new Dictionary<string, dynamic>();
                foreach (var type in types)
                {
                    var attr = type.ClrType.GetTypeInfo().GetCustomAttribute<MenuClassAttribute>();
                    if (attr != null)
                    {
                        classes.Add(type.ClrType.Name,
                            new {
                                category = string.IsNullOrEmpty(attr.Category) ? "/" : attr.Category,
                                iconClass = attr.IconClass
                            });
                    }
                }
                return Json(classes);
            }
            catch
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Remove(string dataType, string id)
        {
            if (!TryGetRepository(dataType, out IRepository repository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (!Guid.TryParse(id, out Guid guid))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            try
            {
                await repository.RemoveAsync(guid);
            }
            catch
            {
                return Json(new { response = "Item could not be removed." });
            }
            return Ok();
        }

        [HttpPost("{id}/{childProp}/{childId}")]
        public async Task<IActionResult> RemoveChild(string dataType, string id, string childProp, string childId)
        {
            if (!TryGetRepository(dataType, out IRepository repository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (string.IsNullOrEmpty(id))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (!Guid.TryParse(id, out Guid guid))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (string.IsNullOrEmpty(childProp))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (string.IsNullOrEmpty(childId))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (!Guid.TryParse(childId, out Guid childGuid))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            object item = null;
            try
            {
                item = await repository.FindItemAsync(guid);
            }
            catch
            {
                return Json(new { error = "Item could not be accessed." });
            }
            if (item == null)
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/404" });
            }
            var childTypeInfo = item.GetType().GetTypeInfo();
            var pInfo = childTypeInfo.GetProperty(childProp.ToInitialCaps());
            if (pInfo == null)
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            var idPInfo = childTypeInfo.GetProperty($"{childProp.ToInitialCaps()}Id");
            if (idPInfo == null)
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (!TryGetRepository(pInfo.PropertyType.Name, out IRepository childRepository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            try
            {
                var updatedItem = await repository.RemoveChildAsync(item, pInfo, idPInfo);
                await childRepository.RemoveAsync(childGuid);
                return Json(updatedItem);
            }
            catch
            {
                return Json(new { error = "Item could not be removed." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RemoveRange(string dataType, [FromBody]List<string> ids)
        {
            if (!TryGetRepository(dataType, out IRepository repository))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            if (ids.Count == 0)
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            List<Guid> guids = new List<Guid>();
            foreach (var id in ids)
            {
                if (!Guid.TryParse(id, out Guid guid))
                {
                    guids.Add(guid);
                }
                else
                {
                    return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
                }
            }
            try
            {
                await repository.RemoveRangeAsync(guids);
            }
            catch
            {
                return Json(new { response = "One or more items could not be removed." });
            }
            return Ok();
        }

        private bool TryGetRepository(string dataType, out IRepository repository)
        {
            repository = null;
            if (string.IsNullOrEmpty(dataType))
            {
                return false;
            }
            var entity = _context.Model.GetEntityTypes().FirstOrDefault(e => e.Name.Substring(e.Name.LastIndexOf('.') + 1) == dataType);
            if (entity == null)
            {
                return false;
            }
            var type = entity.ClrType;
            if (type == null)
            {
                return false;
            }
            repository = (IRepository)Activator.CreateInstance(typeof(Repository<>).MakeGenericType(type), _context);
            return true;
        }

        private IRepository GetRepository(Type type)
            => (IRepository)Activator.CreateInstance(typeof(Repository<>).MakeGenericType(type), _context);

        private bool TryResolveObject(string dataType, JObject item, out object obj, out Type type)
        {
            obj = null;
            type = null;
            if (item == null)
            {
                return false;
            }
            if (string.IsNullOrEmpty(dataType))
            {
                return false;
            }
            var entity = _context.Model.GetEntityTypes().FirstOrDefault(e => e.Name.Substring(e.Name.LastIndexOf('.') + 1) == dataType);
            if (entity == null)
            {
                return false;
            }
            type = entity.ClrType;
            try
            {
                obj = item.ToObject(type);
            }
            catch
            {
                return false;
            }
            return true;
        }

        [HttpPost]
        public async Task<IActionResult> Update(string dataType, [FromBody]JObject item)
        {
            if (!TryResolveObject(dataType, item, out object obj, out Type type))
            {
                return RedirectToAction(nameof(HomeController.Index), new { forwardUrl = "/error/400" });
            }
            IRepository repository = GetRepository(type);
            try
            {
                var updatedItem = await repository.UpdateAsync(obj);
                return Json(updatedItem);
            }
            catch
            {
                return Json(new { error = "Item could not be updated." });
            }
        }
    }
}
