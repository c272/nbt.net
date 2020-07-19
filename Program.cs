using System;
using System.IO;

namespace NBT
{
    class Program
    {
        static void Main(string[] args)
        {
            var levDat = NBT.Deserialize<LevelDat>(File.ReadAllBytes("level.dat"));
        }
    }

    [NBTCompound]
    public class LevelDat
    {
        [NBTItem]
        public Data_LevelDat Data { get; set; }
    }

    [NBTCompound]
    public class Data_LevelDat
    {
        [NBTItem]
        public double BorderCenterX { get; set; }

        [NBTItem]
        public int DataVersion { get; set; }

        [NBTItem]
        public int WanderingTraderSpawnChance { get; set; }

        [NBTItem]
        public long DayTime { get; set; }

        [NBTItem]
        public byte Difficulty { get; set; }

        [NBTItem]
        public string generatorName { get; set; }
    }
}
