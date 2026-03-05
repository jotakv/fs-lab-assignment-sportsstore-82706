using fs_serilog_seq_a_20260219.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SportsStore.Models;
using SportsStore.Models.ViewModels;
using System.Diagnostics;

namespace SportsStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly IStoreRepository _repository;
        private readonly ILogger<HomeController> _logger;

        public int PageSize = 4;

        public HomeController(IStoreRepository repo, ILogger<HomeController> logger)
        {
            _repository = repo;
            _logger = logger;
        }

        public ViewResult Index(string? category, int productPage = 1)
        {
            _logger.LogInformation(
                "Home Index requested. Category={Category} ProductPage={ProductPage} PageSize={PageSize}",
                category ?? "all",
                productPage,
                PageSize
            );

            var productsQuery = _repository.Products
                .Where(p => category == null || p.Category == category);

            var totalItems = category == null
                ? _repository.Products.Count()
                : _repository.Products.Where(e => e.Category == category).Count();

            var products = productsQuery
                .OrderBy(p => p.ProductID)
                .Skip((productPage - 1) * PageSize)
                .Take(PageSize);

            _logger.LogInformation(
                "Products query prepared. TotalItems={TotalItems} ReturnedCount={ReturnedCount}",
                totalItems,
                products.Count()
            );

            return View(new ProductsListViewModel
            {
                Products = products,
                PagingInfo = new PagingInfo
                {
                    CurrentPage = productPage,
                    ItemsPerPage = PageSize,
                    TotalItems = totalItems
                },
                CurrentCategory = category
            });
        }

        // Similar to professor example: Error action with RequestId
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            _logger.LogWarning(
                "Error page requested. RequestId={RequestId}",
                Activity.Current?.Id ?? HttpContext.TraceIdentifier
            );

            // If you already have an ErrorViewModel in your project, use it.
            // Otherwise, create one (see note below).
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}