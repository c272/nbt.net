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
            Type requiredType = GetTypeFromTag((NBT_Tag)data[index]);
            if ((NBT_Tag)data[index] == NBT_Tag.EndCompound)
            {
                return; //Stop processing here if it's the end of a compound.
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
            NBT_Tag tag = (NBT_Tag)data[index];
            switch (tag)
            {
                //Straightforward value type.s
                case NBT_Tag.ByteSigned:
                case NBT_Tag.Short:
                case NBT_Tag.Integer:
                case NBT_Tag.Long:
                case NBT_Tag.Float:
                case NBT_Tag.Double:
                case NBT_Tag.ByteArray:
                case NBT_Tag.String:
                case NBT_Tag.IntArray:
                case NBT_Tag.LongArray:
                    possibleProps.SetValue(typeObj, GetTagValue(tag, afterHeader, dataStart, ref nextIndex));
                    break;

                //List of items.
                case NBT_Tag.List:
                    var list = ParseList(afterHeader, nbtProps, dataStart, ref nextIndex);
                    possibleProps.SetValue(typeObj, list);
                    break;

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

        /// <summary>
        /// Returns the value of a given tag given the type and starting data.
        /// </summary>
        private static object GetTagValue(NBT_Tag tag, IEnumerable<byte> afterHeader, int dataStart, ref int nextIndex)
        {
            switch (tag) 
            {
                //One byte.
                case NBT_Tag.ByteSigned:
                    nextIndex = dataStart + 1;
                    return afterHeader.ElementAt(0);

                //Short.
                case NBT_Tag.Short:
                    nextIndex = dataStart + 2;
                    return BitConverter.ToInt16(afterHeader.Take(2).Reverse().ToArray());

                //Integer (4 bytes, big endian).
                case NBT_Tag.Integer:
                    nextIndex = dataStart + 4;
                    return BitConverter.ToInt32(afterHeader.Take(4).Reverse().ToArray());

                //Long (8 bytes, big endian).
                case NBT_Tag.Long:
                    nextIndex = dataStart + 8;
                    return BitConverter.ToInt64(afterHeader.Take(8).Reverse().ToArray());

                //Float (4 byte IEEE-754 single precision).
                case NBT_Tag.Float:
                    nextIndex = dataStart + 4;
                    return BitConverter.ToSingle(afterHeader.Take(4).ToArray());

                //Double (8 byte IEEE-754 double precision).
                case NBT_Tag.Double:
                    nextIndex = dataStart + 8;
                    return BitConverter.ToDouble(afterHeader.Take(8).ToArray());

                //Byte array (length prefixed w/ signed 4-byte int).
                case NBT_Tag.ByteArray:
                    int arrayLen = BitConverter.ToInt32(afterHeader.Take(4).Reverse().ToArray());
                    nextIndex = dataStart + 4 + arrayLen;
                    return afterHeader.Skip(4).Take(arrayLen).ToArray();

                //String (length prefixed with 2 byte ushort).
                case NBT_Tag.String:
                    int strLen = BitConverter.ToUInt16(afterHeader.Take(2).Reverse().ToArray());
                    nextIndex = dataStart + 2 + strLen;
                    return Encoding.UTF8.GetString(afterHeader.Skip(2).Take(strLen).ToArray());

                //Integer array (length prefixed with signed 4-byte int).
                case NBT_Tag.IntArray:
                    int iArrLen = BitConverter.ToInt32(afterHeader.Take(4).Reverse().ToArray());
                    var ints = new List<int>();
                    for (int i = 0; i < iArrLen; i++)
                    {
                        ints.Add(BitConverter.ToInt32(afterHeader.Skip(4 + 4 * i).Take(4).Reverse().ToArray()));
                    }
                    nextIndex = dataStart + 4 + iArrLen * 4;
                    return ints.ToArray();

                //Long array (length prefixed with signed 4-byte int).
                case NBT_Tag.LongArray:
                    int lArrLen = BitConverter.ToInt32(afterHeader.Take(4).Reverse().ToArray());
                    var longs = new List<long>();
                    for (int i = 0; i < lArrLen; i++)
                    {
                        longs.Add(BitConverter.ToInt64(afterHeader.Skip(4 + 8 * i).Take(8).Reverse().ToArray()));
                    }

                    nextIndex = dataStart + 4 + lArrLen * 8;
                    return longs.ToArray();

                default:
                    //Uh oh.
                    throw new Exception("Tag value cannot be grabbed for type '" + tag.ToString() + "'.");
            }
        }

        /// <summary>
        /// Returns a C# type (or null if not found) for the given NBT tag type.
        /// </summary>
        private static Type GetTypeFromTag(NBT_Tag tag)
        {
            switch (tag)
            {
                case NBT_Tag.ByteSigned:
                    return typeof(byte);
                case NBT_Tag.Short:
                    return typeof(short);
                case NBT_Tag.Integer:
                    return typeof(int);
                case NBT_Tag.Long:
                    return typeof(long);
                case NBT_Tag.Float:
                    return typeof(float);
                case NBT_Tag.Double:
                    return typeof(double);
                case NBT_Tag.ByteArray:
                    return typeof(byte[]);
                case NBT_Tag.String:
                    return typeof(string);
                case NBT_Tag.IntArray:
                    return typeof(int[]);
                case NBT_Tag.LongArray:
                    return typeof(long[]);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Parses an NBT list tag given the starting data.
        /// </summary>
        private static object ParseList(IEnumerable<byte> afterHeader, List<PropertyInfo> possible, int dataStart, ref int nextIndex)
        {
            //Get the length of the list.
            int listLen = BitConverter.ToInt32(afterHeader.Skip(1).Take(4).Reverse().ToArray());

            //Get the list type.
            NBT_Tag tag = (NBT_Tag)afterHeader.ElementAt(0);
            if (tag == NBT_Tag.EndCompound && listLen != 0)
            {
                throw new Exception("NBT list cannot be of type 'EndCompound' and have a non-zero length.");
            }
            Type listType = GetTypeFromTag(tag);
            
            //Whittle down the possible properties (must be lists with generic parameter (if found).
            for (int i=0; i<possible.Count; i++)
            {
                //Make sure it's a list. (todo: check)
                if (possible[i].PropertyType != typeof(System.Collections.IList) 
                    || !possible[i].PropertyType.IsGenericType
                    || possible[i].PropertyType.GetGenericArguments().Length == 0)
                {
                    possible.RemoveAt(i);
                    i--;
                    continue;
                }

                //Is the list generic of the right type (if type required).
                if (listType != null && possible[i].PropertyType.GetGenericArguments()[0] != listType)
                {
                    possible.RemoveAt(i);
                    i--;
                    continue;
                }
            }

            //Make a list of the type and get values for the straightforward value types.
            if (listType != null)
            {
                var lt = typeof(List<>).MakeGenericType(listType);
                var list = (System.Collections.IList)Activator.CreateInstance(lt);
                int startIndex = 5;
                for (int i=0; i<listLen; i++)
                {
                    list.Add(GetTagValue(tag, afterHeader.Skip(startIndex), startIndex, ref startIndex));
                }

                //Set next, return the list.
                nextIndex = dataStart + startIndex;
                return list;
            }
            else
            {
                return null;
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
