using HealthMonitorService.Model;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HealthMonitorService.Collectors
{
    public abstract class MetricCollectorBase
    {
        protected HostContext HostContext { get; }
        private readonly IPAddress? _defaultGateway;

        protected MetricCollectorBase(HostContext hostContext)
        {
            HostContext = hostContext;
            _defaultGateway = GetDefaultGateway();
        }

        protected static bool GetNetworkAvailable()
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }

        protected async Task<double> GetPacketLossPercentAsync(CancellationToken cancellationToken, int attempts = 3)
        {
            if (!GetNetworkAvailable() || _defaultGateway is null)
            {
                return 100;
            }

            using var ping = new Ping();
            int success = 0;

            for (int i = 0; i < attempts; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var reply = await ping.SendPingAsync(_defaultGateway, 1000);

                    if (reply.Status == IPStatus.Success)
                    {
                        success++;
                    }
                }
                catch
                {
                    // treat as failure
                }
            }

            int lost = attempts - success;
            return Math.Round((double)lost / attempts * 100.0, 2);
        }

        protected MetricSample CreateBaseSample()
        {
            return new MetricSample
            {
                Timestamp = DateTime.UtcNow,
                MonitoringStartTime = HostContext.MonitoringStartTime,
                OperatingSystem = HostContext.OperatingSystem,
                DeviceIp = HostContext.DeviceIP,
                MachineName = HostContext.MachineName,
                Failure = FailureLabel.Unlabeled
            };
        }

        private static IPAddress? GetDefaultGateway()
        {
            return NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.GetIPProperties().GatewayAddresses.Count != 0)
                .SelectMany(n => n.GetIPProperties().GatewayAddresses)
                .Select(g => g.Address)
                .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        }
    }
}
