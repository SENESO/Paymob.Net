namespace Paymob
{
    /// <summary>
    /// Options for <see cref="PaymobClient"/>.
    /// </summary>
    public class PaymobClientOptions
    {
        /// <summary>
        /// Your Paymob API key (dashboard → Developers → API Keys).
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// HMAC secret used to validate transaction callbacks
        /// (dashboard → Developers → HMAC). Optional until you verify webhooks.
        /// </summary>
        public string HmacSecret { get; set; }

        /// <summary>
        /// Override for tests or regional endpoints. Defaults to https://accept.paymob.com.
        /// </summary>
        public string BaseUrl { get; set; } = "https://accept.paymob.com";
    }
}
