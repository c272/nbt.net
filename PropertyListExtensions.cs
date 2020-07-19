using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

//Dumb nullable warning.
#pragma warning disable CS8632

namespace NBT
{
    /// <summary>
    /// Utility extension methods for lists of property information.
    /// </summary>
    public static class PropertyListExtensions
    {
        /// <summary>
        /// Sets property values for all the properties in the list.
        /// </summary>
        public static void SetValue(this List<PropertyInfo> list, object? target, object? value)
        {
            foreach (var item in list)
            {
                item.SetValue(target, value);
            }
        }
    }
}
