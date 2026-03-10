using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SportsStore.Controllers;
using SportsStore.Models;
using SportsStore.Services.Payments;
using Stripe.Checkout;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SportsStore.Tests
{
    public class OrderControllerTests
    {
        // --- Fake in-memory session for unit tests ---
        private class TestSession : ISession
        {
            private readonly Dictionary<string, byte[]> _store = new();

            public IEnumerable<string> Keys => _store.Keys;
            public string Id { get; } = Guid.NewGuid().ToString();
            public bool IsAvailable => true;

            public void Clear() => _store.Clear();
            public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void Remove(string key) => _store.Remove(key);

            public void Set(string key, byte[] value) => _store[key] = value;
            public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value);
        }

        private static OrderController CreateController(
    IOrderRepository repo,
    Cart cart,
    IPaymentService paymentService)
        {
            var controller = new OrderController(
                repo,
                cart,
                NullLogger<OrderController>.Instance,
                paymentService
            );

            // HttpContext real
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "http";

            //  Session configurada correctamente (ISessionFeature)
            var session = new TestSession();
            httpContext.Features.Set<ISessionFeature>(new SessionFeature { Session = session });

            // ControllerContext (incluye RouteData para MVC)
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
                RouteData = new RouteData()
            };

            //  UrlHelper mock para que Url.Action(...) NO sea null y NO lance excepción
            var urlHelperMock = new Mock<IUrlHelper>();
            urlHelperMock
                .Setup(x => x.Action(It.IsAny<UrlActionContext>()))
                .Returns("http://localhost/payment/success"); // devuelve algo válido siempre

            controller.Url = urlHelperMock.Object;

            return controller;
        }

        [Fact]
        public async Task Cannot_Checkout_Empty_Cart()
        {
            // Arrange
            var repoMock = new Mock<IOrderRepository>();
            var paymentMock = new Mock<IPaymentService>();
            var cart = new Cart();
            var order = new Order();

            var target = CreateController(repoMock.Object, cart, paymentMock.Object);

            // Act
            var result = await target.Checkout(order) as ViewResult;

            // Assert
            repoMock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
            paymentMock.Verify(p => p.CreateCheckoutSessionAsync(
                    It.IsAny<IEnumerable<(string Name, long UnitAmountCents, int Quantity)>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>()),
                Times.Never);

            Assert.True(string.IsNullOrEmpty(result?.ViewName));
            Assert.False(result?.ViewData.ModelState.IsValid);
        }

        [Fact]
        public async Task Cannot_Checkout_Invalid_ShippingDetails()
        {
            // Arrange
            var repoMock = new Mock<IOrderRepository>();
            var paymentMock = new Mock<IPaymentService>();

            var cart = new Cart();
            cart.AddItem(new Product { Name = "P1", Price = 10m }, 1);

            var target = CreateController(repoMock.Object, cart, paymentMock.Object);

            // add model error (invalid)
            target.ModelState.AddModelError("error", "error");

            // Act
            var result = await target.Checkout(new Order()) as ViewResult;

            // Assert
            repoMock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
            paymentMock.Verify(p => p.CreateCheckoutSessionAsync(
                    It.IsAny<IEnumerable<(string Name, long UnitAmountCents, int Quantity)>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>()),
                Times.Never);

            Assert.True(string.IsNullOrEmpty(result?.ViewName));
            Assert.False(result?.ViewData.ModelState.IsValid);
        }

        [Fact]
        public async Task Can_Checkout_And_Redirect_To_Stripe()
        {
            // Arrange
            var repoMock = new Mock<IOrderRepository>();
            var paymentMock = new Mock<IPaymentService>();

            var cart = new Cart();
            cart.AddItem(new Product { ProductID = 1, Name = "Football", Price = 25m }, 2);

            // Stripe session mock return
            var stripeSession = new Session
            {
                Id = "cs_test_123",
                Url = "https://checkout.stripe.com/pay/cs_test_123"
            };

            paymentMock
                .Setup(p => p.CreateCheckoutSessionAsync(
                    It.IsAny<IEnumerable<(string Name, long UnitAmountCents, int Quantity)>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>()))
                .ReturnsAsync(stripeSession);

            var target = CreateController(repoMock.Object, cart, paymentMock.Object);

            // Act
            var result = await target.Checkout(new Order());

            // Assert
            //  Order is NOT saved before payment (rubric requirement)
            repoMock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);

            //  Stripe session must be created once
            paymentMock.Verify(p => p.CreateCheckoutSessionAsync(
                    It.IsAny<IEnumerable<(string Name, long UnitAmountCents, int Quantity)>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<string>()),
                Times.Once);

            //  Redirect to Stripe Checkout URL
            var redirect = Assert.IsType<RedirectResult>(result);
            Assert.Equal("https://checkout.stripe.com/pay/cs_test_123", redirect.Url);
        }
    }
}