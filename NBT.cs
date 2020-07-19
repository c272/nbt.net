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

            //Make sure the NBT file begins with a compound.
            if (data[0] != (int)NBT_Tag.StartCompound || data.Length <= 3)
            {
                throw new Exception("Invalid NBT start tag (must be an empty compound tag).");
            }

            //Get valid property data for destination type.
            var nbtProps = typeof(T).GetProperties()
                                    .Where(x => x.GetCustomAttribute(typeof(NBTItem)) != null);

            //Recursively process NBT tags.
            T typeObj = (T)Activator.CreateInstance(typeof(T));
            ProcessTag(data, 3, typeObj, nbtProps); //skip to index 3 to pass static header 0A 00 00.

            throw new NotImplementedException();
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
            var possibleProps = nbtProps.Select(x => (NBTItem)x.GetCustomAttribute(typeof(NBTItem)))
                                        .Where(x => x.Name == name || (x.IsRegex && Regex.IsMatch(name, x.Name)))
                                        .ToList();

            //Skip this tag if there are no possible properties.
            if (possibleProps.Count == 0) { return; }

            //Whittle down the possible properties more.
            switch ((NBT_Tag)data[index])
            {
                //Finished processing this set of tags.
                case NBT_Tag.EndCompound:
                    return;

                case NBT_Tag.ByteSigned:
                    nextIndex = ProcessSignedByte(data, dataStart, name, typeObj, possibleProps[0]);
                    break;

                case NBT_Tag.Short:

                    return;

                case NBT_Tag.Integer:
                    return;

                case NBT_Tag.Long:
                    return;

                case NBT_Tag.Float:
                    return;

                case NBT_Tag.Double:
                    return;

                case NBT_Tag.ByteArray:
                    return;

                case NBT_Tag.String:
                    return;

                case NBT_Tag.List:
                    return;

                case NBT_Tag.StartCompound:
                    return;

                case NBT_Tag.IntArray:
                    return;

                case NBT_Tag.LongArray:
                    return;

                default:
                    throw new Exception("Invalid tag type (0x" + data[index].ToString("X") + ") at index " + index + ".");
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
