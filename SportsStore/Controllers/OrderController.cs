using Microsoft.AspNetCore.Mvc;
using SportsStore.Models;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using SportsStore.Services.Payments;

namespace SportsStore.Controllers
{
    public class OrderController : Controller
    {
        private IOrderRepository repository;
        private Cart cart;
        private readonly ILogger<OrderController> _logger;
        private readonly IPaymentService _paymentService;
        public OrderController(IOrderRepository repoService, Cart cartService, ILogger<OrderController> logger, IPaymentService paymentService)
        {
            repository = repoService;
            cart = cartService;
            _logger = logger;
            _paymentService = paymentService;
        }

        public ViewResult Checkout()
        {
            _logger.LogInformation(
                "Checkout started. User={User} CartItems={ItemCount}",
                User?.Identity?.Name ?? "anonymous",
                cart.Lines.Count()
            );

            return View(new Order());
        }

        [HttpPost]
        public IActionResult Checkout(Order order)
        {
            _logger.LogInformation(
                "Checkout submitted. User={User} CartItems={ItemCount}",
                User?.Identity?.Name ?? "anonymous",
                cart.Lines.Count()
            );

            if (cart.Lines.Count() == 0)
            {
                ModelState.AddModelError("", "Sorry, your cart is empty!");
                _logger.LogWarning(
                    "Checkout blocked: cart empty. User={User}",
                    User?.Identity?.Name ?? "anonymous"
                );
            }

            if (ModelState.IsValid)
            {
                try
                {
                    order.Lines = cart.Lines.ToArray();
                    repository.SaveOrder(order);
                    HttpContext.Session.SetString("PendingOrder", JsonSerializer.Serialize(order));
                    _logger.LogInformation(
                        "Order created. OrderId={OrderId} Total={Total} Items={Items}",
                        order.OrderID,
                        order.Lines.Sum(l => l.Quantity * l.Product.Price),
                        order.Lines.Sum(l => l.Quantity)
                    );

                    foreach (var line in order.Lines)
                    {
                        _logger.LogInformation(
                            "Order item. OrderId={OrderId} ProductId={ProductId} ProductName={ProductName} Qty={Qty} UnitPrice={UnitPrice}",
                            order.OrderID,
                            line.Product.ProductID,
                            line.Product.Name,
                            line.Quantity,
                            line.Product.Price
                        );
                    }

                    cart.Clear();
                    return RedirectToPage("/Completed", new { orderId = order.OrderID });
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Checkout failed. User={User} CartItems={ItemCount}",
                        User?.Identity?.Name ?? "anonymous",
                        cart.Lines.Count()
                    );
                    throw;
                }
            }

            _logger.LogWarning(
                "Checkout failed validation. User={User} Errors={ErrorCount}",
                User?.Identity?.Name ?? "anonymous",
                ModelState.ErrorCount
            );

            return View(order);
        }
    }
}