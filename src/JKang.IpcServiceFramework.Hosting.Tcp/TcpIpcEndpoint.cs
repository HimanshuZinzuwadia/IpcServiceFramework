using JKang.IpcServiceFramework.Services;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace JKang.IpcServiceFramework.Hosting.Tcp
{
    public class TcpIpcEndpoint<TContract> : IpcEndpoint<TContract>
        where TContract : class
    {
        private readonly TcpIpcEndpointOptions _options;
        private readonly TcpListener _listener;

        public TcpIpcEndpoint(
            string name,
            TcpIpcEndpointOptions options,
            IIpcMessageSerializer serializer,
            IValueConverter valueConverter,
            ILogger<TcpIpcEndpoint<TContract>> logger,
            IServiceProvider serviceProvider)
            : base(name, options, serviceProvider, serializer, valueConverter, logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _listener = new TcpListener(_options.IpEndpoint, _options.Port);
            _listener.Start();
        }

        protected override async Task WaitAndProcessAsync(CancellationToken cancellationToken)
        {
            using (TcpClient client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false))
            {
                Stream server = client.GetStream();
                server = TransformStream(server);

                // if SSL is enabled, wrap the stream in an SslStream in client mode
                if (_options.EnableSsl)
                {
                    var ssl = new SslStream(server, false);
                    ssl.AuthenticateAsServer(_options.SslCertificate
                        ?? throw new IpcHostingConfigurationException("Invalid TCP IPC endpoint configured: SSL enabled without providing certificate."));
                    server = ssl;
                }

                await ProcessAsync(server, cancellationToken).ConfigureAwait(false);
                client.Close();
            }
        }
    }
}
