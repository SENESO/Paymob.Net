using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Paymob.Models
{
    /// <summary>
    /// Customer billing data required for the payment key request.
    /// </summary>
    public class BillingData
    {
        [JsonPropertyName("first_name")]
        public string FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string LastName { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }

        [JsonPropertyName("phone_number")]
        public string PhoneNumber { get; set; }

        [JsonPropertyName("apartment")]
        public string Apartment { get; set; } = "NA";

        [JsonPropertyName("floor")]
        public string Floor { get; set; } = "NA";

        [JsonPropertyName("street")]
        public string Street { get; set; } = "NA";

        [JsonPropertyName("building")]
        public string Building { get; set; } = "NA";

        [JsonPropertyName("postal_code")]
        public string PostalCode { get; set; } = "NA";

        [JsonPropertyName("city")]
        public string City { get; set; } = "NA";

        [JsonPropertyName("country")]
        public string Country { get; set; } = "NA";

        [JsonPropertyName("state")]
        public string State { get; set; } = "NA";

        [JsonPropertyName("shipping_method")]
        public string ShippingMethod { get; set; } = "NA";
    }

    public class OrderItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("amount_cents")]
        public int AmountCents { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;
    }

    /// <summary>
    /// Everything needed to take a customer to Paymob's checkout in one call.
    /// </summary>
    public class CheckoutRequest
    {
        public int AmountCents { get; set; }
        public string Currency { get; set; } = "EGP";
        public string MerchantOrderId { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();

        /// <summary>Payment integration id (card, wallet, …) from the dashboard.</summary>
        public int IntegrationId { get; set; }

        /// <summary>Iframe id that renders the checkout page.</summary>
        public int IframeId { get; set; }

        public BillingData BillingData { get; set; }

        /// <summary>Payment key lifetime in seconds. Defaults to 3600 (1 hour).</summary>
        public int ExpirationSeconds { get; set; } = 3600;
    }

    /// <summary>
    /// Result of <see cref="PaymobClient.CreateCheckoutAsync"/>.
    /// </summary>
    public class CheckoutResult
    {
        /// <summary>Paymob's order id.</summary>
        public long PaymobOrderId { get; set; }

        /// <summary>The payment token (payment key).</summary>
        public string PaymentToken { get; set; }

        /// <summary>Full iframe URL to redirect the customer to.</summary>
        public string IframeUrl { get; set; }
    }

    /// <summary>
    /// Error thrown when Paymob answers with a non-success status.
    /// </summary>
    public class PaymobApiException : System.Exception
    {
        public int StatusCode { get; }

        public PaymobApiException(int statusCode, string message)
            : base($"Paymob API error (HTTP {statusCode}): {message}")
        {
            StatusCode = statusCode;
        }
    }

    // ---- Wire shapes (internal) ----

    internal class AuthTokenResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }
    }

    internal class CreateOrderResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }

    internal class PaymentKeyResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
}
