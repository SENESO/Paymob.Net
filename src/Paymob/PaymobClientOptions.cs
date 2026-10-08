namespace Paymob
{
    /// <summary>
    /// Options for <see cref="PaymobClient"/>.
    /// </summary>
    public class PaymobClientOptions
    {
        /// <summary>
        /// Your Paymob API key (dashboard → Developers → API Keys).
        /// Used for the classic 3-step flow (auth token → order → payment key).
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Your Paymob secret key (dashboard → Developers).
        /// Used as <c>Authorization: Token {secret_key}</c> for post-payment
        /// operations (refund/void/capture) and the Intention API.
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// Your Paymob public key, used to build unified-checkout URLs.
        /// </summary>
        public string PublicKey { get; set; }

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
