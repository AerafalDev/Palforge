namespace Palforge.Extensions;

internal static class FlagExtensions
{
    extension<T>(T enumFlag)
        where T : unmanaged, Enum
    {
        public bool HasAnyFlag(params T[] flags)
        {
            return flags.Any(flag => enumFlag.HasFlag(flag));
        }
    }
}