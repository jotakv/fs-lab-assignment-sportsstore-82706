using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

namespace SportsStore.Services.Payments
{
    public class StripePaymentService : IPaymentService
    {
        private readonly SessionService _sessionService;

        public StripePaymentService(IConfiguration config)
        {
            var secretKey = config["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                // No explota en CI si nadie llama al servicio, pero sí da error claro si intentas pagar sin key
                throw new InvalidOperationException("Stripe SecretKey is missing. Set it via user-secrets or environment variables.");
            }

            StripeConfiguration.ApiKey = secretKey;
            _sessionService = new SessionService();
        }

        public async Task<Session> CreateCheckoutSessionAsync(
            IEnumerable<(string Name, long UnitAmountCents, int Quantity)> items,
            string successUrl,
            string cancelUrl,
            string? customerEmail,
            string correlationId)
        {
            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = successUrl, // debe incluir {CHECKOUT_SESSION_ID}
                CancelUrl = cancelUrl,
                ClientReferenceId = correlationId, // trazabilidad interna :contentReference[oaicite:2]{index=2}
                CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
                LineItems = items.Select(i => new SessionLineItemOptions
                {
                    Quantity = i.Quantity,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = "eur",
                        UnitAmount = i.UnitAmountCents,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = i.Name
                        }
                    }
                }).ToList()
            };

            return await _sessionService.CreateAsync(options);
        }

        public async Task<Session> GetCheckoutSessionAsync(string sessionId)
        {
            return await _sessionService.GetAsync(sessionId);
        }
    }
}