using NUnit.Framework;
using Paymob.Models;
using Paymob.Webhooks;

namespace Paymob.Tests
{
    public class WebhookValidatorTests
    {
        private const string HmacSecret = "test-hmac-secret";

        private static TransactionData Data() => new TransactionData
        {
            Id = 123456,
            AmountCents = 50000,
            Currency = "EGP",
            Success = true,
            Pending = false,
            CreatedAt = "2026-10-08T12:00:00.000Z",
            ErrorOccured = false,
            HasParentTransaction = false,
            IntegrationId = 111,
            Is3dSecure = true,
            IsAuth = false,
            IsCapture = false,
            IsRefunded = false,
            IsStandalonePayment = true,
            IsVoided = false,
            Owner = 555,
            Order = new CallbackOrder { Id = 987654321, MerchantOrderId = "order-1" },
            SourceData = new SourceData { Type = "card", SubType = "MasterCard", Pan = "2346" }
        };

        private static string CallbackJson(string hmac) =>
            $@"{{ ""type"": ""TRANSACTION"",
                ""hmac"": ""{hmac}"",
                ""obj"": {{
                    ""id"": 123456, ""amount_cents"": 50000, ""currency"": ""EGP"",
                    ""success"": true, ""pending"": false,
                    ""created_at"": ""2026-10-08T12:00:00.000Z"",
                    ""error_occured"": false, ""has_parent_transaction"": false,
                    ""integration_id"": 111, ""is_3d_secure"": true,
                    ""is_auth"": false, ""is_capture"": false,
                    ""is_refunded"": false, ""is_standalone_payment"": true,
                    ""is_voided"": false, ""owner"": 555,
                    ""order"": {{ ""id"": 987654321, ""merchant_order_id"": ""order-1"" }},
                    ""source_data"": {{ ""type"": ""card"", ""sub_type"": ""MasterCard"", ""pan"": ""2346"" }}
                }} }}";

        [Test]
        public void ValidCallback_Passes()
        {
            var hmac = PaymobWebhookValidator.ComputeHmac(HmacSecret, Data());
            Assert.IsTrue(PaymobWebhookValidator.IsValidCallback(HmacSecret, CallbackJson(hmac)));
        }

        [Test]
        public void TamperedAmount_Fails()
        {
            var hmac = PaymobWebhookValidator.ComputeHmac(HmacSecret, Data());
            var tampered = CallbackJson(hmac).Replace("50000", "50001");
            Assert.IsFalse(PaymobWebhookValidator.IsValidCallback(HmacSecret, tampered));
        }

        [Test]
        public void WrongSecret_Fails()
        {
            var hmac = PaymobWebhookValidator.ComputeHmac(HmacSecret, Data());
            Assert.IsFalse(PaymobWebhookValidator.IsValidCallback("other-secret", CallbackJson(hmac)));
        }

        [Test]
        public void GarbageJson_Fails()
        {
            Assert.IsFalse(PaymobWebhookValidator.IsValidCallback(HmacSecret, "not json"));
        }
    }
}
