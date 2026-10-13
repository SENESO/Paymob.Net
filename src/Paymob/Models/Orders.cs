using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Paymob.Models
{
    /// <summary>
    /// Full order object returned by the order-inquiry endpoint
    /// (<c>GET /api/ecommerce/orders/{id}</c>), including every transaction
    /// attempt tied to the order — useful when you only stored the order id
    /// and want to reconcile all payment attempts against it.
    /// </summary>
    public class OrderDetails
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("amount_cents")]
        public int AmountCents { get; set; }

        [JsonPropertyName("paid_amount_cents")]
        public int PaidAmountCents { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("merchant_order_id")]
        public string MerchantOrderId { get; set; }

        /// <summary>Order status as reported by Paymob (e.g. "PENDING").</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("items")]
        public List<OrderItem> Items { get; set; }

        /// <summary>Every transaction attempt tied to this order.</summary>
        [JsonPropertyName("transactions")]
        public List<TransactionData> Transactions { get; set; }
    }

    /// <summary>
    /// Result of a mobile-wallet pay request
    /// (<c>POST /api/acceptance/payments/pay</c>). Carries the transaction
    /// fields plus, for redirect-based payment sources, the authorization URL.
    /// </summary>
    public class WalletPayResult : TransactionData
    {
        /// <summary>
        /// URL to redirect the customer to so they can authorize the wallet
        /// payment. Present for wallet pay requests; null for sources that
        /// settle without a redirect.
        /// </summary>
        [JsonPropertyName("redirect_url")]
        public string RedirectUrl { get; set; }
    }
}
