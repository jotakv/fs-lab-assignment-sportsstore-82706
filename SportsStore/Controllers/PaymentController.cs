using Microsoft.AspNetCore.Mvc;
using SportsStore.Models;
using SportsStore.Services.Payments;
using System.Text.Json;

namespace SportsStore.Controllers
{
    public class PaymentController : Controller
    {
        private readonly IPaymentService _paymentService;
        private readonly IOrderRepository _orderRepository;
        private readonly Cart _cart;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            IOrderRepository orderRepository,
            Cart cart,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _orderRepository = orderRepository;
            _cart = cart;
            _logger = logger;
        }

        public async Task<IActionResult> Success(string session_id)
        {
            if (string.IsNullOrWhiteSpace(session_id))
                return BadRequest("Missing session_id");

            try
            {
                var session = await _paymentService.GetCheckoutSessionAsync(session_id);

                _logger.LogInformation(
                    "Stripe success callback. SessionId={SessionId} PaymentStatus={PaymentStatus} PaymentIntentId={PaymentIntentId}",
                    session.Id,
                    session.PaymentStatus,
                    session.PaymentIntentId
                );

                // 1) Si NO está pagado, NO guardes Order
                if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Payment not paid. SessionId={SessionId} PaymentStatus={PaymentStatus}",
                        session.Id, session.PaymentStatus
                    );
                    return View("Failed");
                }

                // 2) Recuperar draft order (shipping details)
                var pendingJson = HttpContext.Session.GetString("PendingOrder");
                if (string.IsNullOrWhiteSpace(pendingJson))
                {
                    _logger.LogError("PendingOrder missing in session. SessionId={SessionId}", session.Id);
                    return View("Failed");
                }

                var order = JsonSerializer.Deserialize<Order>(pendingJson);
                if (order is null)
                {
                    _logger.LogError("PendingOrder invalid JSON. SessionId={SessionId}", session.Id);
                    return View("Failed");
                }

                // 3) Reconstruir líneas desde el carrito
                order.Lines = _cart.Lines.ToArray();
                var cartId = HttpContext.Session.GetString("CartId") ?? string.Empty;

                // 4) Guardar confirmación de pago en Order (esto es parte del rubric)
                order.StripeSessionId = session.Id;
                order.StripePaymentIntentId = session.PaymentIntentId;
                order.StripePaymentStatus = session.PaymentStatus;
                order.PaidAtUtc = DateTime.UtcNow;

                // 5) Guardar order SOLO AHORA (después de pago OK)
                _orderRepository.SaveOrder(order);

                _logger.LogInformation(
                    "Order confirmed after payment. OrderId={OrderId} SessionId={SessionId} Total={Total} Items={Items}",
                    order.OrderID,
                    session.Id,
                    order.Lines.Sum(l => l.Quantity * l.Product.Price),
                    order.Lines.Sum(l => l.Quantity)
                );

                // (Bonus traceability) items del pedido
                foreach (var line in order.Lines)
                {
                    _logger.LogInformation(
                        "Order item confirmed. OrderId={OrderId} CartId={CartId} ProductId={ProductId} ProductName={ProductName} Qty={Qty} UnitPrice={UnitPrice}",
                        order.OrderID,
                        cartId,
                        line.Product.ProductID,
                        line.Product.Name,
                        line.Quantity,
                        line.Product.Price
                    );
                }

                // 6) Limpieza
                HttpContext.Session.Remove("PendingOrder");
                _cart.Clear();

                return RedirectToPage("/Completed", new { orderId = order.OrderID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stripe success handling failed. SessionId={SessionId}", session_id);
                return View("Failed");
            }
        }

        public IActionResult Cancel()
        {
            _logger.LogWarning("Payment cancelled by user. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);

            // opcional: limpiar pending order
            // HttpContext.Session.Remove("PendingOrder");

            return View();
        }

        public IActionResult Failed()
        {
            return View();
        }
    }
}
