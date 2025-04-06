namespace SSLTrack.Extensions;

public static class IpAddressHelper
{
    private static readonly IPNetwork IpV4BlockLoopback = new(IPAddress.Parse("127.0.0.0"), 8);
    private static readonly IPNetwork IpV4Block24Bits = new(IPAddress.Parse("10.0.0.0"), 8);
    private static readonly IPNetwork IpV4Block20Bits = new(IPAddress.Parse("172.16.0.0"), 12);
    private static readonly IPNetwork IpV4Block16Bits = new(IPAddress.Parse("192.168.0.0"), 16);
    private static readonly IPNetwork IpV6Block = new(IPAddress.Parse("fc00::"), 7);

    public static bool IsPrivateIp(this IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPrivateIpV4(address),
            AddressFamily.InterNetworkV6 => IsPrivateIpV6(address),
            _ => throw new ArgumentException(null, nameof(address)),
        };
    }

    private static bool IsPrivateIpV4(IPAddress address)
    {
        return IpV4BlockLoopback.Contains(address)
               || IpV4Block24Bits.Contains(address)
               || IpV4Block20Bits.Contains(address)
               || IpV4Block16Bits.Contains(address);
    }

    private static bool IsPrivateIpV6(IPAddress address)
    {
        return address.Equals(IPAddress.IPv6Loopback)
               || address.IsIPv6LinkLocal
               || IpV6Block.Contains(address);
    }
}