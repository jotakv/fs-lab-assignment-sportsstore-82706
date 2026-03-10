using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using SportsStore.Models;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading.Tasks;
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
            var cartId = GetOrCreateCartId();
            var productIds = cart.Lines.Select(l => l.Product.ProductID).ToArray();
            _logger.LogInformation(
                "Checkout started. User={User} CartItems={ItemCount} CartId={CartId} ProductIds={ProductIds}",
                User?.Identity?.Name ?? "anonymous",
                cart.Lines.Count(),
                cartId,
                productIds
            );

            return View(new Order());
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(Order order)
        {
            var cartId = GetOrCreateCartId();
            var productIds = cart.Lines.Select(l => l.Product.ProductID).ToArray();
            _logger.LogInformation(
                "Checkout submitted. User={User} CartItems={ItemCount} CartId={CartId} ProductIds={ProductIds}",
                User?.Identity?.Name ?? "anonymous",
                cart.Lines.Count(),
                cartId,
                productIds
            );

            if (cart.Lines.Count() == 0)
            {
                ModelState.AddModelError("", "Sorry, your cart is empty!");
                _logger.LogWarning(
                    "Checkout blocked: cart empty. User={User} CartId={CartId}",
                    User?.Identity?.Name ?? "anonymous",
                    cartId
                );
            }

            if (ModelState.IsValid)
            {
                try
                {
                    //  1) Guardar draft order (shipping details) en Session
                    HttpContext.Session.SetString("PendingOrder", JsonSerializer.Serialize(order));

                    //  2) Preparar items desde el carrito
                    var items = cart.Lines.Select(l => (
                        Name: l.Product.Name,
                        UnitAmountCents: (long)(l.Product.Price * 100m),
                        Quantity: l.Quantity
                    ));

                    //  3) URLs de retorno
                    var successBaseUrl = Url.Action(
                        action: "Success",
                        controller: "Payment",
                        values: null,
                        protocol: Request.Scheme
                    )!;
                    var successUrl = $"{successBaseUrl}?session_id={{CHECKOUT_SESSION_ID}}";

                    var cancelUrl = Url.Action(
                        action: "Cancel",
                        controller: "Payment",
                        values: null,
                        protocol: Request.Scheme
                    )!;

                    //  4) Crear session Stripe (AQUÍ se tiene que invocar el mock en el test)
                    var session = await _paymentService.CreateCheckoutSessionAsync(
                        items,
                        successUrl,
                        cancelUrl,
                        customerEmail: User?.Identity?.Name,
                        correlationId: HttpContext.TraceIdentifier
                    );

                    _logger.LogInformation(
                        "Stripe session created. SessionId={SessionId} Url={Url} CartId={CartId} ProductIds={ProductIds}",
                        session.Id,
                        session.Url,
                        cartId,
                        productIds
                    );

                    // 5) Redirigir a Stripe
                    return Redirect(session.Url);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Checkout failed while creating Stripe session. User={User} CartId={CartId}",
                        User?.Identity?.Name ?? "anonymous",
                        cartId
                    );
                    return RedirectToAction("Failed", "Payment");
                }
            }

            _logger.LogWarning(
                "Checkout failed validation. User={User} Errors={ErrorCount} CartId={CartId}",
                User?.Identity?.Name ?? "anonymous",
                ModelState.ErrorCount,
                cartId
            );

            return View(order);
        }

        private string GetOrCreateCartId()
        {
            var cartId = HttpContext.Session.GetString("CartId");
            if (string.IsNullOrWhiteSpace(cartId))
            {
                cartId = Guid.NewGuid().ToString("N");
                HttpContext.Session.SetString("CartId", cartId);
            }

            return cartId;
        }
    }
}

