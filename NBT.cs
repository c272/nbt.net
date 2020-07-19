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
            ProcessTag(data, 0, typeObj, nbtProps.ToList());

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

                //Correct if it's a null name to name of the property.
                if (nbtAttr.Name == null)
                {
                    nbtAttr.Name = prop.Name;
                }

                //Check if it's a match, add to list if it is.
                if (nbtAttr.Name == name || (nbtAttr.IsRegex && Regex.IsMatch(name, nbtAttr.Name)))
                {
                    possibleProps.Add(prop);
                }
            }

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

            //Switch over the type and set the value.
            int dataStart = index + 3 + nameLen;
            var afterHeader = data.Skip(dataStart);
            int nextIndex = -1;
            switch ((NBT_Tag)data[index])
            {
                //One byte.
                case NBT_Tag.ByteSigned:
                    possibleProps.SetValue(typeObj, data[dataStart]);
                    nextIndex = dataStart + 1;
                    break;

                //Short (2 bytes, big endian).
                case NBT_Tag.Short:
                    possibleProps.SetValue(typeObj, BitConverter.ToInt16(afterHeader.Take(2).Reverse().ToArray()));
                    nextIndex = dataStart + 2;
                    break;

                //Integer (4 bytes, big endian).
                case NBT_Tag.Integer:
                    possibleProps.SetValue(typeObj, BitConverter.ToInt32(afterHeader.Take(4).Reverse().ToArray()));
                    nextIndex = dataStart + 4;
                    break;

                //Long (8 bytes, big endian).
                case NBT_Tag.Long:
                    possibleProps.SetValue(typeObj, BitConverter.ToInt64(afterHeader.Take(8).Reverse().ToArray()));
                    nextIndex = dataStart + 8;
                    break;

                //Float (4 byte IEEE-754 single precision).
                case NBT_Tag.Float:
                    possibleProps.SetValue(typeObj, BitConverter.ToSingle(afterHeader.Take(4).ToArray()));
                    nextIndex = dataStart + 4;
                    break;

                //Double (8 byte IEEE-754 double precision).
                case NBT_Tag.Double:
                    possibleProps.SetValue(typeObj, BitConverter.ToDouble(afterHeader.Take(8).ToArray()));
                    nextIndex = dataStart + 8;
                    break;

                //Byte array (length prefixed w/ signed 4-byte int).
                case NBT_Tag.ByteArray:
                    int arrayLen = BitConverter.ToInt32(afterHeader.Take(4).Reverse().ToArray());
                    possibleProps.SetValue(typeObj, afterHeader.Skip(4).Take(arrayLen).ToArray());
                    nextIndex = dataStart + 4 + arrayLen;
                    break;

                //String (length prefixed with 2 byte ushort).
                case NBT_Tag.String:
                    int strLen = BitConverter.ToUInt16(afterHeader.Take(2).Reverse().ToArray());
                    possibleProps.SetValue(typeObj, Encoding.ASCII.GetString(afterHeader.Skip(2).Take(strLen).ToArray()));
                    nextIndex = dataStart + 2 + strLen;
                    break;

                //Integer array (length prefixed with signed 4-byte int).
                case NBT_Tag.IntArray:
                    int iArrLen = BitConverter.ToInt32(afterHeader.Take(4).Reverse().ToArray());
                    var ints = new List<int>();
                    for (int i=0; i<iArrLen; i++)
                    {
                        ints.Add(BitConverter.ToInt32(afterHeader.Skip(4 + 4 * i).Take(4).Reverse().ToArray()));
                    }
                    possibleProps.SetValue(typeObj, ints.ToArray());
                    nextIndex = dataStart + 4 + iArrLen * 4;
                    break;

                //Long array (length prefixed with signed 4-byte int).
                case NBT_Tag.LongArray:
                    int lArrLen = BitConverter.ToInt32(afterHeader.Take(4).Reverse().ToArray());
                    var longs = new List<long>();
                    for (int i = 0; i < lArrLen; i++)
                    {
                        longs.Add(BitConverter.ToInt64(afterHeader.Skip(4 + 8 * i).Take(8).Reverse().ToArray()));
                    }
                    possibleProps.SetValue(typeObj, longs.ToArray());
                    nextIndex = dataStart + 4 + lArrLen * 8;
                    break;

                //List of items.
                case NBT_Tag.List:
                    Console.WriteLine("todo: nbt list parse");
                    throw new NotImplementedException(); //todo

                //Child compound.
                case NBT_Tag.StartCompound:
                    //Consider only custom classes with NBTCompound tag.
                    for (int i=0; i<possibleProps.Count; i++)
                    {
                        //Not custom/no tag?
                        if (possibleProps[i].PropertyType.Namespace.StartsWith("System")
                            || possibleProps[i].PropertyType.GetCustomAttribute(typeof(NBTCompound)) == null)
                        {
                            continue;
                        }

                        //Get valid property data for destination type.
                        var childValidProps = possibleProps[i].PropertyType.GetProperties()
                                                                    .Where(x => x.GetCustomAttribute(typeof(NBTItem)) != null);

                        //Recursively process NBT tags for child.
                        object childCompound = Activator.CreateInstance(possibleProps[i].PropertyType);
                        ProcessTag(data, dataStart, childCompound, childValidProps.ToList());

                        //Set in parent.
                        possibleProps[i].SetValue(typeObj, childCompound);
                    }
                    nextIndex = dataStart;
                    break;
            }

            //Process the next tag in the compound.
            ProcessTag(data, nextIndex, typeObj, nbtProps);
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
