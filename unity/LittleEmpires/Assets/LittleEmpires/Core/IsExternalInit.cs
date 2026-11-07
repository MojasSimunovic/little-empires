// Unity 6 and netstandard2.1 do not ship this marker type required by C# 9 records.
// Compiled conditionally so net8.0 does not redefine the built-in type.
#if !NET8_0
namespace System.Runtime.CompilerServices
{
    internal sealed class IsExternalInit { }
}
#endif
