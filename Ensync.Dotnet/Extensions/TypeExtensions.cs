namespace Ensync.Dotnet.Extensions;

internal static class TypeExtensions
{
    public static bool IsNullable(this Type type) => IsNullableGeneric(type) || type.Equals(typeof(string)) || type.Equals(typeof(byte[]));

    public static bool IsNullableGeneric(this Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);

    public static bool IsNullableEnum(this Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition().Equals(typeof(Nullable<>)) &&
        type.GetGenericArguments()[0].IsEnum;
}
