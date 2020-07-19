using System;
using System.IO;

namespace NBT
{
    class Program
    {
        static void Main(string[] args)
        {
            NBT.Deserialize<LevelDat>(File.ReadAllBytes("level.dat"));
        }
    }

    public class LevelDat
    {

    }
}
