using System;
using System.Collections.Generic;
using System.Text;

namespace NBT
{
    /// <summary>
    /// Denotes that the attached class represents an NBT compound.
    /// </summary>
    public class NBTCompound : Attribute { }

    /// <summary>
    /// Attribute class for all NBT data tags.
    /// </summary>
    public class NBTItem : Attribute
    {
        public string Name { get; set; }
        public bool IsRegex { get; set; }
        public NBTItem(string name = null, bool isRegex = false)
        {
            Name = name;
            IsRegex = isRegex;
        }
    }
}
