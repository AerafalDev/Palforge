namespace Palforge.Unreal.Reflection;

[Flags]
public enum EFunctionFlags : uint
{
    None = 0x00000000,
    Final = 0x00000001,
    BlueprintAuthorityOnly = 0x00000004,
    Net = 0x00000040,
    Static = 0x00002000,
    Native = 0x00000400,
    Event = 0x00000800,
    BlueprintCallable = 0x04000000,
    BlueprintPure = 0x10000000
}