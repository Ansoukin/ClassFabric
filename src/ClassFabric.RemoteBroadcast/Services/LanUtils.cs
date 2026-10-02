using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace ClassFabric.RemoteBroadcast.Services;

/// <summary>
/// R1：本机 IPv4 局域网地址枚举（二维码与设置页展示用）。
/// 127.0.0.1 与 169.254.* 一律排除——前者手机连不上，后者是 DHCP 失败的废地址，进了二维码只会害老师白扫一上午。
/// </summary>
public static class LanUtils
{
    public sealed record LanAddress(string Ip, string AdapterName);

    public static List<LanAddress> GetLanAddresses()
    {
        var result = new List<LanAddress>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up ||
                nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            foreach (var ip in nic.GetIPProperties().UnicastAddresses)
            {
                if (ip.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                var text = ip.Address.ToString();
                if (text.StartsWith("127.") || text.StartsWith("169.254."))
                {
                    continue;
                }

                result.Add(new LanAddress(text, nic.Name));
            }
        }

        return result;
    }

    /// <summary>二维码用地址：优先用户在设置页点选的网卡，否则取第一个候选。</summary>
    public static string? PickPreferredAddress(IEnumerable<LanAddress> addresses, string preferredIp)
    {
        var list = addresses.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        return list.FirstOrDefault(x => x.Ip == preferredIp)?.Ip ?? list[0].Ip;
    }
}
