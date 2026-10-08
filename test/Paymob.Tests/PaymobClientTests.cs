using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Paymob;
using Paymob.Models;

namespace Paymob.Tests
{
    public class PaymobClientTests
    {
        private static PaymobClientOptions Options() => new PaymobClientOptions
        {
            ApiKey = "test-api-key"
        };

        private static CheckoutRequest Checkout() => new CheckoutRequest
        {
            AmountCents = 50000,
            Currency = "EGP",
            MerchantOrderId = "order-1",
            IntegrationId = 111,
            IframeId = 222,
            BillingData = new BillingData
            {
                FirstName = "Ahmed",
                LastName = "Hassan",
                Email = "ahmed@test.com",
                PhoneNumber = "+201012345678"
            }
        };

        [Test]
        public async Task CreateCheckoutAsync_RunsThreeStepFlowAndBuildsIframeUrl()
        {
            var handler = new SequencedHandler(new[]
            {
                @"{ ""token"": ""auth-tok"" }",
                @"{ ""id"": 987654321 }",
                @"{ ""token"": ""pay-key"" }"
            });
            var client = new PaymobClient(Options(), new HttpClient(handler));

            var result = await client.CreateCheckoutAsync(Checkout());

            Assert.AreEqual(987654321, result.PaymobOrderId);
            Assert.AreEqual("pay-key", result.PaymentToken);
            Assert.AreEqual(
                "https://accept.paymob.com/api/acceptance/iframes/222?payment_token=pay-key",
                result.IframeUrl);

            Assert.AreEqual(3, handler.Requests.Count);
            Assert.IsTrue(handler.Requests[0].Url.EndsWith("/api/auth/tokens"));
            Assert.IsTrue(handler.Requests[1].Url.EndsWith("/api/ecommerce/orders"));
            Assert.IsTrue(handler.Requests[2].Url.EndsWith("/api/acceptance/payment_keys"));

            // order payload carries the amount and merchant order id
            using (var doc = JsonDocument.Parse(handler.Requests[1].Body))
            {
                Assert.AreEqual(50000, doc.RootElement.GetProperty("amount_cents").GetInt32());
                Assert.AreEqual("order-1", doc.RootElement.GetProperty("merchant_order_id").GetString());
            }

            // payment key payload carries order id + integration id + billing data
            using (var doc = JsonDocument.Parse(handler.Requests[2].Body))
            {
                Assert.AreEqual(987654321, doc.RootElement.GetProperty("order_id").GetInt64());
                Assert.AreEqual(111, doc.RootElement.GetProperty("integration_id").GetInt32());
                Assert.AreEqual("Ahmed",
                    doc.RootElement.GetProperty("billing_data").GetProperty("first_name").GetString());
            }
        }

        [Test]
        public void ApiError_ThrowsPaymobApiException()
        {
            var handler = new SequencedHandler(
                new[] { @"{ ""detail"": ""bad key"" }" }, HttpStatusCode.Unauthorized);
            var client = new PaymobClient(Options(), new HttpClient(handler));

            var ex = Assert.ThrowsAsync<PaymobApiException>(() => client.GetAuthTokenAsync());
            Assert.AreEqual(401, ex.StatusCode);
        }

        [Test]
        public void MissingApiKey_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                new PaymobClient(new PaymobClientOptions()));
        }

        private class SequencedHandler : HttpMessageHandler
        {
            private readonly Queue<string> _bodies;
            private readonly HttpStatusCode _status;
            public List<(string Url, string Body)> Requests { get; } = new List<(string, string)>();

            public SequencedHandler(IEnumerable<string> bodies, HttpStatusCode status = HttpStatusCode.OK)
            {
                _bodies = new Queue<string>(bodies);
                _status = status;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content == null
                    ? null
                    : await request.Content.ReadAsStringAsync();
                Requests.Add((request.RequestUri.ToString(), body));

                return new HttpResponseMessage(_status)
                {
                    Content = new StringContent(_bodies.Dequeue(), Encoding.UTF8, "application/json")
                };
            }
        }
    }
}
