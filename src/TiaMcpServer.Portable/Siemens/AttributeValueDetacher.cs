using System;
using System.Collections.Generic;

namespace TiaMcpServer.Siemens
{
    /// <summary>
    /// Turns a value read from a TIA Portal attribute into one that no longer refers to TIA Portal.
    /// </summary>
    /// <remarks>
    /// Openness answers most attributes with a plain value — a yes or no, a number, a word from an
    /// enumeration — and some with a live engineering object: a device item's <c>Container</c> and
    /// <c>Items</c>, the multilingual text behind <c>CommentML</c>, a certificate. Those are remote
    /// references whose properties lead back to their parents, so a JSON serializer walking one
    /// never reaches the end. Measured on 2026-10-03 on a class project: <c>CommentML</c> cycles
    /// through <c>Culture.Parent</c> and <c>Container</c> through <c>Container.DeviceItems</c>, and
    /// GetDevices failed with "An error occurred." and no reason, because the failure happened after
    /// the tool had returned, while its answer was being written.
    ///
    /// A plain value is kept as it is, so a number still reaches the caller as a number. Anything
    /// else becomes the text it prints as, which says what it was without handing out the object.
    /// </remarks>
    public static class AttributeValueDetacher
    {
        private static readonly HashSet<Type> PlainTypes = new HashSet<Type>
        {
            typeof(string),
            typeof(decimal),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(Guid)
        };

        /// <summary>Returns a value that is safe to keep once the object it was read from is gone.</summary>
        /// <param name="value">The value as Openness returned it, or null.</param>
        /// <returns>
        /// The value itself when it is plain, a copy when it is an array of plain values, and its
        /// text otherwise.
        /// </returns>
        public static object? Detach(object? value)
        {
            if (value == null || IsPlain(value.GetType()))
            {
                return value;
            }

            if (value is Array array && IsPlain(array.GetType().GetElementType()!))
            {
                return array.Clone();
            }

            return value.ToString();
        }

        private static bool IsPlain(Type type)
        {
            return type.IsPrimitive || type.IsEnum || PlainTypes.Contains(type);
        }
    }
}
