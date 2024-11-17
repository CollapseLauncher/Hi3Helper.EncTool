using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Hi3Helper.EncTool
{
    public static class TypeExtensions
    {
        public static bool IsStructEqual<TStruct>(TStruct self, TStruct to)
            where TStruct : struct => IsStructEqualFromTo(self, to);

        public static unsafe bool IsStructEqualFromTo<TFrom, TTo>(TFrom self, TTo to)
            where TFrom : struct
            where TTo : struct
        {
            // Check if the object is equal based on reference or struct can be compared
            // as a raw data (does not contain any reference type)
            if (self.Equals(to)) return true;

            // If not, then fallback to check using marshalling
            // Get the size of the struct
            int sizeOfTFrom = Marshal.SizeOf<TFrom>();
            int sizeOfTTo = Marshal.SizeOf<TTo>();

            // Allocate the HGlobal buffer pointer
            nint bufferPtr = Marshal.AllocHGlobal(sizeOfTFrom + sizeOfTTo); // Allocate size for from and to
            // Marshal struct into the pointer
            Marshal.StructureToPtr(self, bufferPtr, true);
            Marshal.StructureToPtr(to, bufferPtr + sizeOfTFrom, true);

            // Assign as a span and compare the buffer
            ReadOnlySpan<byte> spanSelf = new ReadOnlySpan<byte>((byte*)bufferPtr, sizeOfTFrom);
            ReadOnlySpan<byte> spanTo = new ReadOnlySpan<byte>((byte*)bufferPtr + sizeOfTFrom, sizeOfTTo);
            bool isRawDataEqual = spanSelf.SequenceEqual(spanTo);

            // Free the HGlobal buffer pointer
            Marshal.FreeHGlobal(bufferPtr);

            // Return the comparison value
            return isRawDataEqual;
        }

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
    }
}
