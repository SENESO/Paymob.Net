using System;
using System.Collections.Generic;
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

        private static PaymobClientOptions FullOptions() => new PaymobClientOptions
        {
            ApiKey = "test-api-key",
            SecretKey = "test-secret-key",
            PublicKey = "test-public-key"
        };

        [Test]
        public async Task RefundAsync_PostsWithSecretKeyAuth()
        {
            var handler = new SequencedHandler(new[] { @"{ ""ok"": true }" });
            var client = new PaymobClient(FullOptions(), new HttpClient(handler));

            Assert.IsTrue(await client.RefundAsync(12345, 10000));

            Assert.AreEqual(1, handler.Requests.Count);
            Assert.IsTrue(handler.Requests[0].Url.EndsWith("/api/acceptance/void_refund/refund"));
            Assert.AreEqual("Token test-secret-key", handler.Requests[0].AuthHeader);

            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
            {
                Assert.AreEqual(12345, doc.RootElement.GetProperty("transaction_id").GetInt64());
                Assert.AreEqual(10000, doc.RootElement.GetProperty("amount_cents").GetInt32());
            }
        }

        [Test]
        public async Task VoidAsync_PostsToVoidEndpoint()
        {
            var handler = new SequencedHandler(new[] { @"{ ""ok"": true }" });
            var client = new PaymobClient(FullOptions(), new HttpClient(handler));

            Assert.IsTrue(await client.VoidAsync(12345));

            Assert.IsTrue(handler.Requests[0].Url.EndsWith("/api/acceptance/void_refund/void"));
            Assert.AreEqual("Token test-secret-key", handler.Requests[0].AuthHeader);
        }

        [Test]
        public async Task CaptureAsync_PostsToCaptureEndpoint()
        {
            var handler = new SequencedHandler(new[] { @"{ ""ok"": true }" });
            var client = new PaymobClient(FullOptions(), new HttpClient(handler));

            Assert.IsTrue(await client.CaptureAsync(12345, 50000));

            Assert.IsTrue(handler.Requests[0].Url.EndsWith("/api/acceptance/capture"));
        }

        [Test]
        public void PostPaymentOps_WithoutSecretKey_Throw()
        {
            var client = new PaymobClient(Options()); // ApiKey only

            Assert.ThrowsAsync<InvalidOperationException>(() => client.RefundAsync(1, 100));
            Assert.ThrowsAsync<InvalidOperationException>(() => client.VoidAsync(1));
            Assert.ThrowsAsync<InvalidOperationException>(() => client.CaptureAsync(1, 100));
        }

        [Test]
        public async Task CreateIntentionAsync_PostsToIntentionEndpoint()
        {
            var handler = new SequencedHandler(new[] { @"{ ""id"": 777, ""client_secret"": ""cs_test"" }" });
            var client = new PaymobClient(FullOptions(), new HttpClient(handler));

            var result = await client.CreateIntentionAsync(new IntentionRequest
            {
                Amount = 25000,
                Currency = "EGP",
                PaymentMethods = new List<int> { 111 },
                BillingData = Checkout().BillingData,
                Customer = new IntentionCustomer
                {
                    FirstName = "Ahmed", LastName = "Hassan", Email = "ahmed@test.com"
                },
                NotificationUrl = "https://example.com/webhook",
                RedirectionUrl = "https://example.com/done"
            });

            Assert.AreEqual("cs_test", result.ClientSecret);
            Assert.AreEqual(1, handler.Requests.Count);
            Assert.IsTrue(handler.Requests[0].Url.EndsWith("/v1/intention/"));
            Assert.AreEqual("Token test-secret-key", handler.Requests[0].AuthHeader);

            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
            {
                Assert.AreEqual(25000, doc.RootElement.GetProperty("amount").GetInt32());
            }
        }

        [Test]
        public void BuildUnifiedCheckoutUrl_UsesPublicKey()
        {
            var client = new PaymobClient(FullOptions());
            Assert.AreEqual(
                "https://accept.paymob.com/unifiedcheckout/?publicKey=test-public-key&clientSecret=cs_test",
                client.BuildUnifiedCheckoutUrl("cs_test"));
        }

        [Test]
        public async Task GetTransactionAsync_UsesAuthToken()
        {
            var handler = new SequencedHandler(new[]
            {
                @"{ ""token"": ""auth-tok"" }",
                @"{ ""id"": 12345, ""amount_cents"": 50000, ""success"": true,
                    ""pending"": false, ""is_refunded"": false, ""is_voided"": false,
                    ""created_at"": ""2026-10-08"", ""order"": { ""id"": 999 } }"
            });
            var client = new PaymobClient(FullOptions(), new HttpClient(handler));

            var txn = await client.GetTransactionAsync(12345);

            Assert.AreEqual(12345, txn.Id);
            Assert.AreEqual(50000, txn.AmountCents);
            Assert.IsTrue(txn.Success);
            Assert.IsTrue(handler.Requests[1].Url.EndsWith("/api/acceptance/transactions/12345"));
            Assert.AreEqual("Bearer auth-tok", handler.Requests[1].AuthHeader);
        }

        private class SequencedHandler : HttpMessageHandler
        {
            private readonly Queue<string> _bodies;
            private readonly HttpStatusCode _status;
            public List<(string Url, string Body, string AuthHeader)> Requests { get; }
                = new List<(string, string, string)>();

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

                string auth = null;
                if (request.Headers.Authorization != null)
                    auth = request.Headers.Authorization.Scheme + " " + request.Headers.Authorization.Parameter;
                else if (request.Headers.TryGetValues("Authorization", out var values))
                    auth = string.Join(",", values);

                Requests.Add((request.RequestUri.ToString(), body, auth));

                return new HttpResponseMessage(_status)
                {
                    Content = new StringContent(_bodies.Dequeue(), Encoding.UTF8, "application/json")
                };
            }
        }
    }
}
