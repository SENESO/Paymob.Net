using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Paymob.Models;

namespace Paymob
{
    /// <summary>
    /// Minimal client for Paymob's Accept API (the classic 3-step flow):
    /// auth token → order → payment key → iframe checkout.
    /// Pass your own <see cref="HttpClient"/> (e.g. from IHttpClientFactory)
    /// to control lifetime, retries and logging.
    /// </summary>
    public class PaymobClient
    {
        private readonly PaymobClientOptions _options;
        private readonly HttpClient _http;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public PaymobClient(PaymobClientOptions options, HttpClient httpClient = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(options.ApiKey))
                throw new ArgumentException("ApiKey is required.", nameof(options));

            _http = httpClient ?? new HttpClient();
        }

        /// <summary>
        /// One call that runs the whole flow and returns an iframe URL you can
        /// redirect the customer to: auth → order → payment key.
        /// </summary>
        public async Task<CheckoutResult> CreateCheckoutAsync(
            CheckoutRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.BillingData == null) throw new ArgumentException("BillingData is required.", nameof(request));

            var authToken = await GetAuthTokenAsync(cancellationToken).ConfigureAwait(false);
            var orderId = await CreateOrderAsync(authToken, request, cancellationToken).ConfigureAwait(false);
            var paymentToken = await CreatePaymentKeyAsync(authToken, orderId, request, cancellationToken).ConfigureAwait(false);

            return new CheckoutResult
            {
                PaymobOrderId = orderId,
                PaymentToken = paymentToken,
                IframeUrl = BuildIframeUrl(request.IframeId, paymentToken)
            };
        }

        /// <summary>
        /// Step 1: exchange the API key for a short-lived auth token.
        /// </summary>
        public virtual async Task<string> GetAuthTokenAsync(CancellationToken cancellationToken = default)
        {
            var res = await PostAsync<AuthTokenResponse>(
                "/api/auth/tokens", new { api_key = _options.ApiKey }, cancellationToken).ConfigureAwait(false);
            return res.Token;
        }

        /// <summary>
        /// Step 2: register the order with Paymob.
        /// </summary>
        public virtual async Task<long> CreateOrderAsync(
            string authToken, CheckoutRequest request, CancellationToken cancellationToken = default)
        {
            var res = await PostAsync<CreateOrderResponse>(
                "/api/ecommerce/orders",
                new
                {
                    auth_token = authToken,
                    delivery_needed = "false",
                    amount_cents = request.AmountCents,
                    currency = request.Currency,
                    merchant_order_id = request.MerchantOrderId,
                    items = request.Items
                },
                cancellationToken).ConfigureAwait(false);
            return res.Id;
        }

        /// <summary>
        /// Step 3: generate the payment key (payment token) for the iframe.
        /// </summary>
        public virtual async Task<string> CreatePaymentKeyAsync(
            string authToken, long orderId, CheckoutRequest request, CancellationToken cancellationToken = default)
        {
            var res = await PostAsync<PaymentKeyResponse>(
                "/api/acceptance/payment_keys",
                new
                {
                    auth_token = authToken,
                    amount_cents = request.AmountCents,
                    expiration = request.ExpirationSeconds,
                    order_id = orderId,
                    billing_data = request.BillingData,
                    currency = request.Currency,
                    integration_id = request.IntegrationId
                },
                cancellationToken).ConfigureAwait(false);
            return res.Token;
        }

        /// <summary>
        /// Builds the iframe checkout URL for a payment token.
        /// </summary>
        public string BuildIframeUrl(int iframeId, string paymentToken)
            => $"{_options.BaseUrl.TrimEnd('/')}/api/acceptance/iframes/{iframeId}?payment_token={paymentToken}";

        /// <summary>
        /// Raw POST helper. Protected virtual so tests can intercept without HTTP.
        /// </summary>
        protected virtual async Task<T> PostAsync<T>(
            string path, object payload, CancellationToken cancellationToken)
        {
            var url = _options.BaseUrl.TrimEnd('/') + path;
            var json = JsonSerializer.Serialize(payload, JsonOptions);

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new PaymobApiException((int)response.StatusCode, body);
                    return JsonSerializer.Deserialize<T>(body, JsonOptions);
                }
            }
        }
    }
}
