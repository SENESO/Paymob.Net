using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Paymob.Models;

namespace Paymob.Webhooks
{
    /// <summary>
    /// Validates Paymob transaction callbacks using the HMAC-SHA512 signature
    /// Paymob attaches to every "transaction processed" callback.
    ///
    /// Per Paymob's docs, the signature is HMAC-SHA512(hmacSecret,
    /// concatenation of the fields below, in this exact order, booleans as
    /// "true"/"false"):
    /// amount_cents, created_at, currency, error_occured,
    /// has_parent_transaction, id, integration_id, is_3d_secure, is_auth,
    /// is_capture, is_refunded, is_standalone_payment, is_voided, order.id,
    /// owner, pending, source_data.pan, source_data.sub_type,
    /// source_data.type, success
    ///
    /// Always validate callbacks — otherwise anyone can forge a "payment
    /// succeeded" POST to your notification URL.
    /// </summary>
    public static class PaymobWebhookValidator
    {
        public static bool IsValidCallback(string hmacSecret, string callbackJson)
        {
            if (string.IsNullOrEmpty(hmacSecret) || string.IsNullOrEmpty(callbackJson))
                return false;

            TransactionCallback callback;
            try
            {
                callback = JsonSerializer.Deserialize<TransactionCallback>(callbackJson);
            }
            catch (JsonException)
            {
                return false;
            }

            return IsValidCallback(hmacSecret, callback);
        }

        public static bool IsValidCallback(string hmacSecret, TransactionCallback callback)
        {
            if (string.IsNullOrEmpty(hmacSecret)
                || callback?.Data == null
                || string.IsNullOrEmpty(callback.Hmac))
                return false;

            var computed = ComputeHmac(hmacSecret, callback.Data);
            return FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(callback.Hmac.ToLowerInvariant()));
        }

        /// <summary>
        /// Computes the expected HMAC for a transaction (useful in tests).
        /// </summary>
        public static string ComputeHmac(string hmacSecret, TransactionData data)
        {
            var sb = new StringBuilder();
            sb.Append(data.AmountCents);
            sb.Append(data.CreatedAt);
            sb.Append(data.Currency);
            sb.Append(Bool(data.ErrorOccured));
            sb.Append(Bool(data.HasParentTransaction));
            sb.Append(data.Id);
            sb.Append(data.IntegrationId);
            sb.Append(Bool(data.Is3dSecure));
            sb.Append(Bool(data.IsAuth));
            sb.Append(Bool(data.IsCapture));
            sb.Append(Bool(data.IsRefunded));
            sb.Append(Bool(data.IsStandalonePayment));
            sb.Append(Bool(data.IsVoided));
            sb.Append(data.Order?.Id);
            sb.Append(data.Owner);
            sb.Append(Bool(data.Pending));
            sb.Append(data.SourceData?.Pan);
            sb.Append(data.SourceData?.SubType);
            sb.Append(data.SourceData?.Type);
            sb.Append(Bool(data.Success));

            using (var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hmacSecret)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static string Bool(bool value) => value ? "true" : "false";

        // Constant-time comparison (netstandard2.0 has no CryptographicOperations).
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
