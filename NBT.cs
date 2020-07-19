using ICSharpCode.SharpZipLib.GZip;
using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace NBT
{
    /// <summary>
    /// Class for NBT serializing and deserializing.
    /// </summary>
    public static class NBT
    {
        /// <summary>
        /// Deserializes a given NBT file into a C# class of the provided type.
        /// </summary>
        /// <typeparam name="T">The type to return.</typeparam>
        /// <param name="data">The NBT file bytes to parse.</param>
        public static T Deserialize<T>(byte[] data)
        {
            //Does the class in question have the "NBTCompound" tag?
            if (typeof(T).GetCustomAttribute(typeof(NBTCompound)) == null)
            {
                throw new Exception("Type to deserialize into must have the NBTCompound attribute.");
            }

            //Check if the data is gzip compressed, decompress if so.
            if (data[0] == 0x1F && data[1] == 0x8B)
            {
                //It's compressed.
                using (var outData = new MemoryStream())
                {
                    GZip.Decompress(new MemoryStream(data), outData, false);
                    data = new byte[outData.Length];
                    outData.Seek(0, SeekOrigin.Begin);
                    outData.Read(data);
                    File.WriteAllBytes("level_decompressed.dat", data);
                }
            }

            //Make sure the NBT file begins with an empty compound (0x0A0000).
            if (data.Length <= 3 || data[0] != (int)NBT_Tag.StartCompound || data[1] != 0x0 || data[2] != 0x0)
            {
                throw new Exception("Invalid NBT start tag (must be an empty compound tag).");
            }

            //Get valid property data for destination type.
            var nbtProps = typeof(T).GetProperties()
                                    .Where(x => x.GetCustomAttribute(typeof(NBTItem)) != null);

            //Recursively process NBT tags.
            T typeObj = (T)Activator.CreateInstance(typeof(T));
            ProcessTag(data, 3, typeObj, nbtProps.ToList()); //skip to index 3 to pass static header 0A 00 00.

            //Return results.
            return typeObj;
        }

        /// <summary>
        /// Processes a single tag at the given location onto the destination object.
        /// </summary>
        private static void ProcessTag(byte[] data, int index, object typeObj, List<PropertyInfo> nbtProps)
        {
            //Get the name of the tag.
            if (index + 2 >= data.Length) { throw new Exception("Invalid tag entrypoint at index " + index + " (no name length)."); }
            int nameLen = BitConverter.ToInt16(data.Skip(index + 1).Take(2).Reverse().ToArray());
            if (index + 2 + nameLen >= data.Length) { throw new Exception("Invalid tag entrypoint at index " + index + " (invalid name length)."); }
            string name = Encoding.ASCII.GetString(data.Skip(index + 3).Take(nameLen).ToArray());

            //Get all valid properties that could apply for this tag.
            //If the property is using regex, then use IsMatch instead of literal name.
            var possibleProps = new List<PropertyInfo>();
            foreach (var prop in nbtProps)
            {
                var nbtAttr = (NBTItem)prop.GetCustomAttribute(typeof(NBTItem));
                if (nbtAttr.Name == name || (nbtAttr.IsRegex && Regex.IsMatch(name, nbtAttr.Name)))
                {
                    possibleProps.Add(prop);
                }
            }

            //Skip this tag if there are no possible properties.
            if (possibleProps.Count == 0) { return; }

            //Whittle down the possible properties more by required type (if a specific one is required).
            Type requiredType = null;
            switch ((NBT_Tag)data[index])
            {
                //End of compound, just stop processing here.
                case NBT_Tag.EndCompound:
                    return;

                case NBT_Tag.ByteSigned: 
                    requiredType = typeof(byte); 
                    break;
                case NBT_Tag.Short: 
                    requiredType = typeof(short); 
                    break;
                case NBT_Tag.Integer: 
                    requiredType = typeof(int); 
                    break;
                case NBT_Tag.Long:
                    requiredType = typeof(long); 
                    break;
                case NBT_Tag.Float:
                    requiredType = typeof(float);
                    break;
                case NBT_Tag.Double:
                    requiredType = typeof(double);
                    break;
                case NBT_Tag.ByteArray:
                    requiredType = typeof(byte[]);
                    break;
                case NBT_Tag.String:
                    requiredType = typeof(string);
                    break;
                case NBT_Tag.IntArray:
                    requiredType = typeof(int[]);
                    break;
                case NBT_Tag.LongArray:
                    requiredType = typeof(long[]);
                    break;
            }

            //Cut by type (if a specific type is required).
            if (requiredType != null)
            {
                for (int i = 0; i < possibleProps.Count; i++)
                {
                    if (possibleProps[i].PropertyType != requiredType)
                    {
                        possibleProps.RemoveAt(i);
                        i--;
                    }
                }
            }

            //Are there any viable properties left? If not, done.
            if (possibleProps.Count == 0) { return; }

            //Still properties that could be viable.
            //Switch over the type and set the value.
            int dataStart = index + 2 + nameLen;
            var afterHeader = data.Skip(dataStart);
            switch ((NBT_Tag)data[index])
            {
                //One byte.
                case NBT_Tag.ByteSigned:
                    possibleProps.SetValue(typeObj, data[dataStart]);
                    return;

                //Short (2 bytes, big endian).
                case NBT_Tag.Short:
                    possibleProps.SetValue(typeObj, BitConverter.ToInt16(afterHeader.Take(2).Reverse().ToArray()));
                    return;

                //Integer (4 bytes, big endian).
                case NBT_Tag.Integer:
                    possibleProps.SetValue(typeObj, BitConverter.ToInt32(afterHeader.Take(4).Reverse().ToArray()));
                    return;

                //Long (8 bytes, big endian).
                case NBT_Tag.Long:
                    possibleProps.SetValue(typeObj, BitConverter.ToInt64(afterHeader.Take(8).Reverse().ToArray()));
                    return;

                //Float (4 byte IEEE-754 single precision).
                case NBT_Tag.Float:
                    possibleProps.SetValue(typeObj, BitConverter.ToSingle(afterHeader.Take(4).ToArray()));
                    return;

                //Double (8 byte IEEE-754 double precision).
                case NBT_Tag.Double:
                    possibleProps.SetValue(typeObj, BitConverter.ToDouble(afterHeader.Take(8).ToArray()));
                    return;

                //Byte array (length prefixed w/ signed 4-byte int).
                case NBT_Tag.ByteArray:
                    int arrayLen = BitConverter.ToInt32(afterHeader.Take(4).Reverse().ToArray());
                    possibleProps.SetValue(typeObj, afterHeader.Skip(4).Take(arrayLen).ToArray());
                    return;

                //String (length prefixed with 2 byte ushort).
                case NBT_Tag.String:
                    int strLen = BitConverter.ToUInt16(afterHeader.Take(2).Reverse().ToArray());
                    possibleProps.SetValue(typeObj, Encoding.ASCII.GetString(afterHeader.Skip(2).Take(strLen).ToArray()));
                    return;

                //List of items.
                case NBT_Tag.List:
                    Console.WriteLine("todo: nbt list parse");
                    return; //todo

                //Child compound.
                case NBT_Tag.StartCompound:
                    //Whittle list down to only custom classes.
            }
        }
    }

    /// <summary>
    /// Represents a single NBT header byte.
    /// </summary>
    public enum NBT_Tag
    {
        EndCompound = 0,
        ByteSigned = 1,
        Short = 2,
        Integer = 3,
        Long = 4,
        Float = 5,
        Double = 6,
        ByteArray = 7,
        String = 8,
        List = 9,
        StartCompound = 10,
        IntArray = 11,
        LongArray = 12
    }
}
