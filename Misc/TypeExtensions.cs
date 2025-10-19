using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace

namespace Hi3Helper.EncTool
{
    public static class TypeExtensions
    {
        public static bool IsInstancePropertyEqual<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]T>(T self, T to)
        {
            // Check if the one of the value is null, if true check the other value if it's null
            if (self == null)
            {
                return to == null;
            }
            if (to == null)
            {
                return false;
            }

            // Get the type of the instance
            Type type = typeof(T);

            // ReSharper disable once LoopCanBeConvertedToQuery
            // Enumerate the PropertyInfo out of instance
            foreach (PropertyInfo pi in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                // Get the property name and value from both self and to
                object selfValue = type.GetProperty(pi.Name)?.GetValue(self, null);
                object toValue   = type.GetProperty(pi.Name)?.GetValue(to,   null);

                // If the value on both self and to is different, then return false (not equal)
                if (selfValue != toValue && (selfValue == null || !selfValue.Equals(toValue)))
                {
                    return false;
                }
            }

            // If all passes, then return true (equal)
            return true;
        }

        public static T RandomSelectSingle<T>(this ReadOnlySpan<T> source) => source[Random.Shared.Next(0, source.Length)];
    }
}
