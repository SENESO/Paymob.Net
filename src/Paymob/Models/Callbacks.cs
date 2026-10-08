using System.Text.Json.Serialization;

namespace Paymob.Models
{
    /// <summary>
    /// The "transaction processed" callback Paymob POSTs to your notification URL.
    /// </summary>
    public class TransactionCallback
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("hmac")]
        public string Hmac { get; set; }

        [JsonPropertyName("obj")]
        public TransactionData Data { get; set; }
    }

    public class TransactionData
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("amount_cents")]
        public int AmountCents { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("pending")]
        public bool Pending { get; set; }

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; }

        [JsonPropertyName("error_occured")]
        public bool ErrorOccured { get; set; }

        [JsonPropertyName("has_parent_transaction")]
        public bool HasParentTransaction { get; set; }

        [JsonPropertyName("integration_id")]
        public int IntegrationId { get; set; }

        [JsonPropertyName("is_3d_secure")]
        public bool Is3dSecure { get; set; }

        [JsonPropertyName("is_auth")]
        public bool IsAuth { get; set; }

        [JsonPropertyName("is_capture")]
        public bool IsCapture { get; set; }

        [JsonPropertyName("is_refunded")]
        public bool IsRefunded { get; set; }

        [JsonPropertyName("is_standalone_payment")]
        public bool IsStandalonePayment { get; set; }

        [JsonPropertyName("is_voided")]
        public bool IsVoided { get; set; }

        [JsonPropertyName("owner")]
        public int Owner { get; set; }

        [JsonPropertyName("order")]
        public CallbackOrder Order { get; set; }

        [JsonPropertyName("source_data")]
        public SourceData SourceData { get; set; }
    }

    public class CallbackOrder
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("merchant_order_id")]
        public string MerchantOrderId { get; set; }
    }

    public class SourceData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("sub_type")]
        public string SubType { get; set; }

        [JsonPropertyName("pan")]
        public string Pan { get; set; }
    }
}
