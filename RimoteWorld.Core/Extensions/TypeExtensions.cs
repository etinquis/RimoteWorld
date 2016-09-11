using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RimoteWorld.Core.Extensions
{
    public static class TypeExtensions
    {
        public static bool IsNullable(this Type type)
        {
            if (!type.IsValueType) return true;
            if (Nullable.GetUnderlyingType(type) != null) return true;
            return false;
        }
    }
}
