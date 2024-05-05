using System.Net;

namespace Bart.NET.Tests;

public class IPAddressExtensionTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(33)]
    [InlineData(50)]
    [InlineData(128)]
    [InlineData(129)]
    public void ToIPNetwork_Throws_Invalid_IPv4_PrefixLength(int prefixLength)
    {
        IPAddress ip = IPAddress.Parse("192.168.7.58");
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => ip.ToIPNetwork(prefixLength));
        Assert.Equal("prefixLength", ex.ParamName);
        Assert.Equal(prefixLength, ex.ActualValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(129)]
    [InlineData(150)]
    [InlineData(256)]
    public void ToIPNetwork_Throws_Invalid_IPv6_PrefixLength(int prefixLength)
    {
        IPAddress ip = IPAddress.Parse("2001:0DBB:ABCD:1234:5678:90AB:DEAD:BEEF");
        ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => ip.ToIPNetwork(prefixLength));
        Assert.Equal("prefixLength", ex.ParamName);
        Assert.Equal(prefixLength, ex.ActualValue);
    }

    [Theory]
    [InlineData("192.168.7.58", 8, "192.0.0.0/8")]
    [InlineData("192.168.7.58", 16, "192.168.0.0/16")]
    [InlineData("192.168.7.58", 24, "192.168.7.0/24")]
    [InlineData("192.168.7.58", 32, "192.168.7.58/32")]
    [InlineData("2001:0DBB:ABCD:1234:5678:90AB:DEAD:BEEF", 24, "2001:d00::/24")]
    [InlineData("2001:0DBB:ABCD:1234:5678:90AB:DEAD:BEEF", 32, "2001:dbb::/32")]
    [InlineData("2001:0DBB:ABCD:1234:5678:90AB:DEAD:BEEF", 64, "2001:dbb:abcd:1234::/64")]
    [InlineData("2001:0DBB:ABCD:1234:5678:90AB:DEAD:BEEF", 96, "2001:dbb:abcd:1234:5678:90AB::/96")]
    [InlineData("2001:0DBB:ABCD:1234:5678:90AB:DEAD:BEEF", 128, "2001:0DBB:ABCD:1234:5678:90AB:DEAD:BEEF/128")]
    public void ToIPNetwork_Correctly_Converts_Simple_Prefixes(string ip, int prefixLength, string expected)
    {
        IPNetwork actual = IPAddress.Parse(ip).ToIPNetwork(prefixLength);

        Assert.Equal(IPNetwork.Parse(expected), actual);
    }

    [Theory]
    [InlineData("192.168.7.58", 1, "128.0.0.0/1")]
    [InlineData("192.168.7.58", 3, "192.0.0.0/3")]
    [InlineData("192.168.7.58", 9, "192.128.0.0/9")]
    [InlineData("192.168.7.58", 12, "192.160.0.0/12")]
    [InlineData("192.168.7.58", 13, "192.168.0.0/13")]
    [InlineData("192.168.7.58", 19, "192.168.0.0/19")]
    [InlineData("192.168.7.58", 23, "192.168.6.0/23")]
    [InlineData("192.168.7.58", 25, "192.168.7.0/25")]
    [InlineData("192.168.7.58", 31, "192.168.7.58/31")]
    [InlineData("54.67.177.197", 1, "0.0.0.0/1")]
    [InlineData("54.67.177.197", 6, "52.0.0.0/6")]
    [InlineData("54.67.177.197", 9, "54.0.0.0/9")]
    [InlineData("54.67.177.197", 12, "54.64.0.0/12")]
    [InlineData("54.67.177.197", 13, "54.64.0.0/13")]
    [InlineData("54.67.177.197", 19, "54.67.160.0/19")]
    [InlineData("54.67.177.197", 23, "54.67.176.0/23")]
    [InlineData("54.67.177.197", 25, "54.67.177.128/25")]
    [InlineData("54.67.177.197", 31, "54.67.177.196/31")]
    [InlineData("54.67.177.197", 0, "0.0.0.0/0")]
    public void ToIPNetwork_Correctly_Converts_Complex_IPv4_Prefixes(string ip, int prefixLength, string expected)
    {
        IPNetwork actual = IPAddress.Parse(ip).ToIPNetwork(prefixLength);

        Assert.Equal(IPNetwork.Parse(expected), actual);
    }

    [Theory]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", 7, "2000::/7")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", 9, "2000::/9")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", 15, "2000::/15")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", 33, "2001:db8:8000::/33")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", 65, "2001:db8:85a3:0::/65")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", 97, "2001:db8:85a3:0:0:8a2e::/97")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", 119, "2001:db8:85a3::8a2e:0370:7200/119")]
    [InlineData("fe80::d503:4ee:3882:c586", 13, "fe80::/13")]
    [InlineData("fe80::d503:4ee:3882:c586", 27, "fe80::/27")]
    [InlineData("fe80::d503:4ee:3882:c586", 99, "fe80::d503:4ee:2000:0/99")]
    [InlineData("ff05::1:3", 3, "e000::/3")]
    [InlineData("ff05::1:3", 15, "ff04::/15")]
    [InlineData("ff05::1:3", 51, "ff05::/51")]
    [InlineData("ff05::1:3", 117, "ff05::1:0/117")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334", 0, "::/0")]
    public void ToIPNetwork_Correctly_Converts_Complex_IPv6_Prefixes(string ip, int prefixLength, string expected)
    {
        IPNetwork actual = IPAddress.Parse(ip).ToIPNetwork(prefixLength);

        Assert.Equal(IPNetwork.Parse(expected), actual);
    }
}
