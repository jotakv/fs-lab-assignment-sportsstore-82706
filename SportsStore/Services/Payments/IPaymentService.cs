using Stripe.Checkout;

namespace SportsStore.Services.Payments
{
    public interface IPaymentService
    {
        Task<Session> CreateCheckoutSessionAsync(
            IEnumerable<(string Name, long UnitAmountCents, int Quantity)> items,
            string successUrl,
            string cancelUrl,
            string? customerEmail,
            string correlationId);

        Task<Session> GetCheckoutSessionAsync(string sessionId);
    }
}