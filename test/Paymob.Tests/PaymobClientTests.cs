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

            Assert.That(result.PaymobOrderId, Is.EqualTo(987654321));
            Assert.That(result.PaymentToken, Is.EqualTo("pay-key"));
            Assert.That(result.IframeUrl, Is.EqualTo("https://accept.paymob.com/api/acceptance/iframes/222?payment_token=pay-key"));

            Assert.That(handler.Requests.Count, Is.EqualTo(3));
            Assert.That(handler.Requests[0].Url.EndsWith("/api/auth/tokens"), Is.True);
            Assert.That(handler.Requests[1].Url.EndsWith("/api/ecommerce/orders"), Is.True);
            Assert.That(handler.Requests[2].Url.EndsWith("/api/acceptance/payment_keys"), Is.True);

            // order payload carries the amount and merchant order id
            using (var doc = JsonDocument.Parse(handler.Requests[1].Body))
            {
                Assert.That(doc.RootElement.GetProperty("amount_cents").GetInt32(), Is.EqualTo(50000));
                Assert.That(doc.RootElement.GetProperty("merchant_order_id").GetString(), Is.EqualTo("order-1"));
            }

            // payment key payload carries order id + integration id + billing data
            using (var doc = JsonDocument.Parse(handler.Requests[2].Body))
            {
                Assert.That(doc.RootElement.GetProperty("order_id").GetInt64(), Is.EqualTo(987654321));
                Assert.That(doc.RootElement.GetProperty("integration_id").GetInt32(), Is.EqualTo(111));
                Assert.That(doc.RootElement.GetProperty("billing_data").GetProperty("first_name").GetString(), Is.EqualTo("Ahmed"));
            }
        }

        [Test]
        public void ApiError_ThrowsPaymobApiException()
        {
            var handler = new SequencedHandler(
                new[] { @"{ ""detail"": ""bad key"" }" }, HttpStatusCode.Unauthorized);
            var client = new PaymobClient(Options(), new HttpClient(handler));

            var ex = Assert.ThrowsAsync<PaymobApiException>(
                new Func<Task>(async () => await client.GetAuthTokenAsync()));
            Assert.That(ex.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public void MissingApiKey_ThrowsOnAuth()
        {
            var client = new PaymobClient(new PaymobClientOptions { SecretKey = "sk" });
            Assert.ThrowsAsync<InvalidOperationException>(
                new Func<Task>(async () => await client.GetAuthTokenAsync()));
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

            Assert.That(await client.RefundAsync(12345, 10000), Is.True);

            Assert.That(handler.Requests.Count, Is.EqualTo(1));
            Assert.That(handler.Requests[0].Url.EndsWith("/api/acceptance/void_refund/refund"), Is.True);
            Assert.That(handler.Requests[0].AuthHeader, Is.EqualTo("Token test-secret-key"));

            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
            {
                Assert.That(doc.RootElement.GetProperty("transaction_id").GetInt64(), Is.EqualTo(12345));
                Assert.That(doc.RootElement.GetProperty("amount_cents").GetInt32(), Is.EqualTo(10000));
            }
        }

        [Test]
        public async Task VoidAsync_PostsToVoidEndpoint()
        {
            var handler = new SequencedHandler(new[] { @"{ ""ok"": true }" });
            var client = new PaymobClient(FullOptions(), new HttpClient(handler));

            Assert.That(await client.VoidAsync(12345), Is.True);

            Assert.That(handler.Requests[0].Url.EndsWith("/api/acceptance/void_refund/void"), Is.True);
            Assert.That(handler.Requests[0].AuthHeader, Is.EqualTo("Token test-secret-key"));
        }

        [Test]
        public async Task CaptureAsync_PostsToCaptureEndpoint()
        {
            var handler = new SequencedHandler(new[] { @"{ ""ok"": true }" });
            var client = new PaymobClient(FullOptions(), new HttpClient(handler));

            Assert.That(await client.CaptureAsync(12345, 50000), Is.True);

            Assert.That(handler.Requests[0].Url.EndsWith("/api/acceptance/capture"), Is.True);
        }

        [Test]
        public void PostPaymentOps_WithoutSecretKey_Throw()
        {
            var client = new PaymobClient(Options()); // ApiKey only

            Assert.ThrowsAsync<InvalidOperationException>(
                new Func<Task>(async () => await client.RefundAsync(1, 100)));
            Assert.ThrowsAsync<InvalidOperationException>(
                new Func<Task>(async () => await client.VoidAsync(1)));
            Assert.ThrowsAsync<InvalidOperationException>(
                new Func<Task>(async () => await client.CaptureAsync(1, 100)));
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
                RedirectionUrl = "https://example.com/done",
                Items = new List<IntentionItem>
                {
                    new IntentionItem { Name = "Widget", Amount = 25000, Description = "Test", Quantity = 1 }
                }
            });

            Assert.That(result.ClientSecret, Is.EqualTo("cs_test"));
            Assert.That(handler.Requests.Count, Is.EqualTo(1));
            Assert.That(handler.Requests[0].Url.EndsWith("/v1/intention/"), Is.True);
            Assert.That(handler.Requests[0].AuthHeader, Is.EqualTo("Token test-secret-key"));

            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
            {
                Assert.That(doc.RootElement.GetProperty("amount").GetInt32(), Is.EqualTo(25000));
                var item = doc.RootElement.GetProperty("items")[0];
                Assert.That(item.GetProperty("amount").GetInt32(), Is.EqualTo(25000));
                Assert.That(item.TryGetProperty("amount_cents", out _), Is.False);
            }
        }

        [Test]
        public void BuildUnifiedCheckoutUrl_UsesPublicKey()
        {
            var client = new PaymobClient(FullOptions());
            Assert.That(client.BuildUnifiedCheckoutUrl("cs_test"), Is.EqualTo("https://accept.paymob.com/unifiedcheckout/?publicKey=test-public-key&clientSecret=cs_test"));
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

            Assert.That(txn.Id, Is.EqualTo(12345));
            Assert.That(txn.AmountCents, Is.EqualTo(50000));
            Assert.That(txn.Success, Is.True);
            Assert.That(handler.Requests[1].Url.EndsWith("/api/acceptance/transactions/12345"), Is.True);
            Assert.That(handler.Requests[1].AuthHeader, Is.EqualTo("Bearer auth-tok"));
        }

        [Test]
        public async Task GetOrderAsync_UsesAuthTokenAndParsesTransactions()
        {
            var handler = new SequencedHandler(new[]
            {
                @"{ ""token"": ""auth-tok"" }",
                @"{ ""id"": 999, ""amount_cents"": 50000, ""paid_amount_cents"": 50000,
                    ""currency"": ""EGP"", ""merchant_order_id"": ""order-1"", ""status"": ""PENDING"",
                    ""transactions"": [ { ""id"": 12345, ""success"": true } ] }"
            });
            var client = new PaymobClient(FullOptions(), new HttpClient(handler));

            var order = await client.GetOrderAsync(999);

            Assert.That(order.Id, Is.EqualTo(999));
            Assert.That(order.PaidAmountCents, Is.EqualTo(50000));
            Assert.That(order.MerchantOrderId, Is.EqualTo("order-1"));
            Assert.That(order.Transactions.Count, Is.EqualTo(1));
            Assert.That(order.Transactions[0].Id, Is.EqualTo(12345));
            Assert.That(order.Transactions[0].Success, Is.True);
            Assert.That(handler.Requests[1].Url.EndsWith("/api/ecommerce/orders/999"), Is.True);
            Assert.That(handler.Requests[1].AuthHeader, Is.EqualTo("Bearer auth-tok"));
        }

        [Test]
        public async Task PayWithWalletAsync_PostsWalletPayRequest()
        {
            var handler = new SequencedHandler(new[]
            {
                @"{ ""id"": 777, ""pending"": true, ""success"": false,
                    ""redirect_url"": ""https://accept.paymob.com/wallet/abc"" }"
            });
            var client = new PaymobClient(FullOptions(), new HttpClient(handler));

            var result = await client.PayWithWalletAsync("pay-key", "01012345678");

            Assert.That(result.RedirectUrl, Is.EqualTo("https://accept.paymob.com/wallet/abc"));
            Assert.That(result.Pending, Is.True);
            Assert.That(1, Is.EqualTo(handler.Requests.Count));
            Assert.That(handler.Requests[0].Url.EndsWith("/api/acceptance/payments/pay"), Is.True);

            using (var doc = JsonDocument.Parse(handler.Requests[0].Body))
            {
                var source = doc.RootElement.GetProperty("source");
                Assert.That(source.GetProperty("identifier").GetString(), Is.EqualTo("01012345678"));
                Assert.That(source.GetProperty("subtype").GetString(), Is.EqualTo("WALLET"));
                Assert.That(doc.RootElement.GetProperty("payment_token").GetString(), Is.EqualTo("pay-key"));
            }
        }

        [Test]
        public void PayWithWalletAsync_RequiresPaymentTokenAndWalletNumber()
        {
            var client = new PaymobClient(FullOptions());

            Assert.Throws<ArgumentException>(
                new Action(() => client.PayWithWalletAsync(null, "01012345678")));
            Assert.Throws<ArgumentException>(
                new Action(() => client.PayWithWalletAsync("pay-key", "")));
        }

        [Test]
        public void BuildIframeUrl_EncodesPaymentToken()
        {
            var client = new PaymobClient(FullOptions());

            // plain tokens are untouched
            Assert.That(client.BuildIframeUrl(222, "pay-key"), Is.EqualTo(
                "https://accept.paymob.com/api/acceptance/iframes/222?payment_token=pay-key"));
            // reserved characters are percent-encoded
            Assert.That(client.BuildIframeUrl(222, "a b+c/d"), Is.EqualTo(
                "https://accept.paymob.com/api/acceptance/iframes/222?payment_token=a%20b%2Bc%2Fd"));
        }

        [Test]
        public void BuildUnifiedCheckoutUrl_EncodesClientSecret()
        {
            var client = new PaymobClient(FullOptions());

            Assert.That(client.BuildUnifiedCheckoutUrl("cs_test"), Is.EqualTo(
                "https://accept.paymob.com/unifiedcheckout/?publicKey=test-public-key&clientSecret=cs_test"));
            Assert.That(client.BuildUnifiedCheckoutUrl("a b"), Is.EqualTo(
                "https://accept.paymob.com/unifiedcheckout/?publicKey=test-public-key&clientSecret=a%20b"));
        }

        [Test]
        public void GetAuthTokenAsync_MissingToken_Throws()
        {
            var handler = new SequencedHandler(new[] { @"{ ""no_token"": true }" });
            var client = new PaymobClient(Options(), new HttpClient(handler));

            var ex = Assert.Throws<InvalidOperationException>(
                new Action(() => client.GetAuthTokenAsync().GetAwaiter().GetResult()));
            Assert.That(ex.Message, Does.Contain("no token"));
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
