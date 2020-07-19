using System;
using System.Collections.Generic;
using System.Text;

namespace NBT
{
    /// <summary>
    /// Base attribute class for all NBT data tags.
    /// </summary>
    public class NBTItem : Attribute
    {
        public string Name { get; private set; }
        public bool IsRegex { get; private set; }
        public NBTItem(string name, bool isRegex = false)
        {
            Name = name;
            IsRegex = isRegex;
        }
    }
}
