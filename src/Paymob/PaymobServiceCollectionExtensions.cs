using System;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Registers <see cref="Paymob.PaymobClient"/> in the DI container.
    /// </summary>
    public static class PaymobServiceCollectionExtensions
    {
        public static IServiceCollection AddPaymob(
            this IServiceCollection services, Paymob.PaymobClientOptions options)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            services.TryAddSingleton(options ?? throw new ArgumentNullException(nameof(options)));
            services.TryAddSingleton<Paymob.PaymobClient>();
            return services;
        }

        public static IServiceCollection AddPaymob(
            this IServiceCollection services, Action<Paymob.PaymobClientOptions> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var options = new Paymob.PaymobClientOptions();
            configure(options);
            return services.AddPaymob(options);
        }
    }
}
