using Microsoft.Extensions.Configuration;
using Stripe;
using Stripe.Checkout;

namespace SportsStore.Services.Payments
{
    public class StripePaymentService : IPaymentService
    {
        private readonly SessionService _sessionService;

        private readonly IConfiguration _config;

        public StripePaymentService(IConfiguration config)
        {
            
            _config = config;
            _sessionService = new SessionService();
        }

        private void EnsureApiKey()
        {
            var secretKey = _config["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
                throw new InvalidOperationException("Stripe SecretKey is missing. Set it via user-secrets or environment variables.");

            StripeConfiguration.ApiKey = secretKey;
        }

        public async Task<Session> CreateCheckoutSessionAsync(
            IEnumerable<(string Name, long UnitAmountCents, int Quantity)> items,
            string successUrl,
            string cancelUrl,
            string? customerEmail,
            string correlationId)
        {
            EnsureApiKey();
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