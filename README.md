![NBT.NET](logo.png)

*A serialization library for the NBT file format created by Notch.*

![](https://img.shields.io/travis/com/c272/nbt.net)
![](https://img.shields.io/github/issues/c272/nbt.net)
![](https://img.shields.io/github/license/c272/nbt.net)

# Overview
NBT.NET is a serialization library designed to work with Notch's "Named Binary Tag" format, created for Minecraft. It includes functions to deserialize NBT data into standard C# classes, as well as vice versa. The entire project is written on .NET Core 3.1 and can be retargeted to .NET Standard.

# Usage
To deserialize a file into a specified C# class, you would first create classes with `NBTItem` and `NBTCompound` attributes, like so:
```cs
using nbt.net;
...

[NBTCompound]
public class ExampleNBT 
{
    [NBTItem]
    public int SomeInteger { get; set; }
    
    [NBTItem("nbtPropertyName")] //this sets the name to match from the nbt as "nbtPropertyName" rather than the C# property name
    public string SomeString { get; set; }
    
    [NBTItem("^[A-Za-z0-9]+_thing$", true)] //this sets the deserializer to match properties with the given regex
    public long[] RegexedProperty { get; set; }
}
```

After you define classes to hold the NBT data, you can deserialize like so:
```cs
byte[] nbtData = File.ReadAllBytes("level.dat");
ExampleNBT deserialized = NBT.Deserialize<ExampleNBT>(nbtData);
```
