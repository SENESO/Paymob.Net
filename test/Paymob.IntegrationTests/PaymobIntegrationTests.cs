using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using NUnit.Framework;
using Paymob;
using Paymob.Models;

namespace Paymob.IntegrationTests
{
    /// <summary>
    /// Live tests against Paymob's real API (https://accept.paymob.com).
    ///
    /// These tests only create orders, payment keys and intentions — they never
    /// submit card details or complete a payment, so no real charge can occur.
    /// Each test uses a small test-mode amount (1 EGP = 100 piasters).
    ///
    /// Credentials come from environment variables (see INTEGRATION_TESTING.md).
    /// Every test calls Assert.Ignore() naming the missing variable when a
    /// required one is absent, so the suite stays green on machines without
    /// sandbox credentials.
    /// </summary>
    [Category("Integration")]
    public class PaymobIntegrationTests
    {
        private const string ApiKeyVar = "PAYMOB_API_KEY";
        private const string SecretKeyVar = "PAYMOB_SECRET_KEY";
        private const string PublicKeyVar = "PAYMOB_PUBLIC_KEY";
        private const string IntegrationIdVar = "PAYMOB_TEST_INTEGRATION_ID";
        private const string IframeIdVar = "PAYMOB_TEST_IFRAME_ID";

        // 1 EGP in piasters — a small test-mode amount. Never charged: these
        // tests stop at order / payment-key / intention creation and never
        // submit any payment credentials.
        private const int TestAmountCents = 100;

        [Test]
        public async Task Authenticate_AgainstLiveApi_ReturnsAuthToken()
        {
            RequireEnvVars(ApiKeyVar);

            var client = CreateClient(new PaymobClientOptions
            {
                ApiKey = Environment.GetEnvironmentVariable(ApiKeyVar)
            });

            var token = await client.GetAuthTokenAsync();

            Assert.IsNotNull(token);
            Assert.IsNotEmpty(token);
        }

        [Test]
        public async Task CreateOrder_AgainstLiveApi_ReturnsOrderId()
        {
            RequireEnvVars(ApiKeyVar);

            var client = CreateClient(new PaymobClientOptions
            {
                ApiKey = Environment.GetEnvironmentVariable(ApiKeyVar)
            });

            var authToken = await client.GetAuthTokenAsync();

            var orderId = await client.CreateOrderAsync(authToken, new CheckoutRequest
            {
                AmountCents = TestAmountCents,
                Currency = "EGP",
                MerchantOrderId = NewMerchantOrderId(),
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Name = "Integration test item",
                        Description = "Created by Paymob.IntegrationTests",
                        AmountCents = TestAmountCents,
                        Quantity = 1
                    }
                }
            });

            Assert.Greater(orderId, 0);
        }

        [Test]
        public async Task CreateCheckout_AgainstLiveApi_ReturnsPaymentTokenAndIframeUrl()
        {
            RequireEnvVars(ApiKeyVar, IntegrationIdVar, IframeIdVar);

            var client = CreateClient(new PaymobClientOptions
            {
                ApiKey = Environment.GetEnvironmentVariable(ApiKeyVar)
            });

            var result = await client.CreateCheckoutAsync(new CheckoutRequest
            {
                AmountCents = TestAmountCents,
                Currency = "EGP",
                MerchantOrderId = NewMerchantOrderId(),
                IntegrationId = int.Parse(Environment.GetEnvironmentVariable(IntegrationIdVar)),
                IframeId = int.Parse(Environment.GetEnvironmentVariable(IframeIdVar)),
                BillingData = TestBillingData(),
                Items = new List<OrderItem>
                {
                    new OrderItem
                    {
                        Name = "Integration test item",
                        Description = "Created by Paymob.IntegrationTests",
                        AmountCents = TestAmountCents,
                        Quantity = 1
                    }
                }
            });

            Assert.Greater(result.PaymobOrderId, 0);
            Assert.IsNotNull(result.PaymentToken);
            Assert.IsNotEmpty(result.PaymentToken);
            StringAssert.StartsWith(
                "https://accept.paymob.com/api/acceptance/iframes/",
                result.IframeUrl);
            StringAssert.Contains(result.PaymentToken, result.IframeUrl);
        }

        [Test]
        public async Task CreateIntention_AgainstLiveApi_ReturnsClientSecret()
        {
            RequireEnvVars(ApiKeyVar, SecretKeyVar, IntegrationIdVar);

            var client = CreateClient(new PaymobClientOptions
            {
                ApiKey = Environment.GetEnvironmentVariable(ApiKeyVar),
                SecretKey = Environment.GetEnvironmentVariable(SecretKeyVar),
                PublicKey = Environment.GetEnvironmentVariable(PublicKeyVar)
            });

            var result = await client.CreateIntentionAsync(new IntentionRequest
            {
                Amount = TestAmountCents,
                Currency = "EGP",
                PaymentMethods = new List<int>
                {
                    int.Parse(Environment.GetEnvironmentVariable(IntegrationIdVar))
                },
                BillingData = TestBillingData(),
                Customer = new IntentionCustomer
                {
                    FirstName = "Ahmed",
                    LastName = "Hassan",
                    Email = "ahmed@test.com"
                },
                Items = new List<IntentionItem>
                {
                    new IntentionItem
                    {
                        Name = "Integration test item",
                        Description = "Created by Paymob.IntegrationTests",
                        Amount = TestAmountCents,
                        Quantity = 1
                    }
                },
                NotificationUrl = "https://example.com/paymob/webhook",
                RedirectionUrl = "https://example.com/paymob/done"
            });

            Assert.Greater(result.Id, 0);
            Assert.IsNotNull(result.ClientSecret);
            Assert.IsNotEmpty(result.ClientSecret);

            // Building the checkout URL is offline logic, but verifying it
            // against a real client secret proves the pair works together.
            var publicKey = Environment.GetEnvironmentVariable(PublicKeyVar);
            if (!string.IsNullOrWhiteSpace(publicKey))
            {
                var url = client.BuildUnifiedCheckoutUrl(result.ClientSecret);
                StringAssert.Contains(result.ClientSecret, url);
                StringAssert.Contains(publicKey, url);
            }
        }

        private static void RequireEnvVars(params string[] names)
        {
            var missing = new List<string>();
            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                    missing.Add(name);
            }

            if (missing.Count > 0)
            {
                Assert.Ignore(
                    "Skipping: set the following environment variable(s) to run this " +
                    "integration test: " + string.Join(", ", missing));
            }
        }

        private static PaymobClient CreateClient(PaymobClientOptions options)
        {
            // Bound the wait so a network problem fails fast instead of
            // hanging the test run indefinitely.
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            return new PaymobClient(options, http);
        }

        private static BillingData TestBillingData() => new BillingData
        {
            FirstName = "Ahmed",
            LastName = "Hassan",
            Email = "ahmed@test.com",
            PhoneNumber = "+201012345678"
        };

        private static string NewMerchantOrderId() =>
            "sdk-integration-" + Guid.NewGuid().ToString("N").Substring(0, 12);
    }
}
