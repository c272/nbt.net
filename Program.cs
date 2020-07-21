using System;
using System.Collections.Generic;
using System.IO;

namespace NBT
{
    class Program
    {
        static void Main(string[] args)
        {
            var levDat = NBT.Deserialize<LevelDatContainer>(File.ReadAllBytes("level.dat"));
        }
    }

    [NBTCompound]
    public class LevelDatContainer
    {
        [NBTItem("")]
        public LevelDat Root { get; set; }
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
        public Data_GameRules GameRules { get; set; }

        [NBTItem]
        public Data_Player Player { get; set; }

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

    [NBTCompound]
    public class Data_Player
    {
        [NBTItem]
        public List<InventoryItem> Inventory { get; set; }
    }

    [NBTCompound]
    public class InventoryItem
    {
        [NBTItem]
        public byte Count { get; set; }

        [NBTItem]
        public byte Slot { get; set; }

        [NBTItem]
        public string id { get; set; }
    }

    [NBTCompound]
    public class Data_GameRules
    {
        [NBTItem]
        public string keepInventory { get; set; }
    }
}
