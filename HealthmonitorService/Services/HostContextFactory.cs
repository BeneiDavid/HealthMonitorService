using HealthMonitorService.Model;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace HealthMonitorService.Services
{
    public static class HostContextFactory
    {
        public static HostContext Create()
        {
            return new HostContext
            {
                DeviceIP = GetLocalIpAddress(),
                MachineName = Environment.MachineName,
                OperatingSystem = GetNormalizedOperatingSystem(),
                MonitoringStartTime = DateTime.UtcNow,
            };
        }

        private static string GetLocalIpAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());

            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ip))
                {
                    return ip.ToString();
                }
            }

            return "Unknown";
        }

        private static string GetNormalizedOperatingSystem()
        {
            if (OperatingSystem.IsWindows())
            {
                return GetNormalizedWindowsName();
            }

            if (OperatingSystem.IsLinux())
            {
                return GetNormalizedLinuxName();
            }

            string architecture = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => "unknown"
            };

            return $"Unknown_{architecture}";
        }

        private static string GetNormalizedWindowsName()
        {
            string architecture = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => "unknown"
            };

            int build = Environment.OSVersion.Version.Build;

            string versionLabel = build >= 22000 ? "11" : "10";

            return $"Windows_{versionLabel}_{architecture}";
        }

        private static string GetNormalizedLinuxName()
        {
            string architecture = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => "unknown"
            };

            try
            {
                var lines = File.ReadAllLines("/etc/os-release");

                string? id = lines.FirstOrDefault(l => l.StartsWith("ID="))?.Split('=')[1].Trim('"');
                string? version = lines.FirstOrDefault(l => l.StartsWith("VERSION_ID="))?.Split('=')[1].Trim('"');

                string distro = id?.ToLowerInvariant() switch
                {
                    "debian" => "Debian",
                    "ubuntu" => "Ubuntu",
                    _ => "Linux"
                };

                return $"{distro}_{version ?? "Unknown"}_{architecture}";
            }
            catch
            {
                return $"Linux_Unknown_{architecture}";
            }
        }
    }
}
