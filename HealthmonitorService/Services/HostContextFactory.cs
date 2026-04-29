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
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
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

            string architecture = GetNormalizedArchitecture();

            return $"Unknown_{architecture}";
        }

        private static string GetNormalizedWindowsName()
        {
            string architecture = GetNormalizedArchitecture();
            int build = Environment.OSVersion.Version.Build;
            string versionLabel = build >= 22000 ? "11" : "10";

            return $"Windows_{versionLabel}_{architecture}";
        }

        private static string GetNormalizedLinuxName()
        {
            string architecture = GetNormalizedArchitecture();

            try
            {
                var lines = File.ReadAllLines("/etc/os-release");

                string? id = null;
                string? version = null;

                foreach (var line in lines)
                {
                    if (line.StartsWith("ID=", StringComparison.Ordinal))
                    {
                        id = line[3..].Trim('"');
                    }
                    else if (line.StartsWith("VERSION_ID=", StringComparison.Ordinal))
                    {
                        version = line[11..].Trim('"');
                    }

                    if (id is not null && version is not null)
                    {
                        break;
                    }
                }

                string distro = CapitalizeAscii(id ?? "Linux");

                return $"{distro}_{version ?? "Unknown"}_{architecture}";
            }
            catch
            {
                return $"Linux_Unknown_{architecture}";
            }
        }

        private static string CapitalizeAscii(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return char.ToUpperInvariant(value[0]) + value[1..];
        }

        private static string GetNormalizedArchitecture(){
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => "unknown"
            };
        }
    }
}
