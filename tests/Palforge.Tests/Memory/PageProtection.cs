namespace Palforge.Tests.Memory;

internal static class PageProtection
{
    public const uint NoAccess = 0x01;
    public const uint ReadOnly = 0x02;
    public const uint ReadWrite = 0x04;
    public const uint Execute = 0x10;
    public const uint ExecuteRead = 0x20;
}