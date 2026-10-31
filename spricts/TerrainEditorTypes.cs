using Godot;
using System;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;

// 由 TerrainEditor.cs 拆出的共享类型

public class UnitTemplate
{
    public string DisplayName;
    public string ScenePath;
    public string Category;
    public string Team;
    public Color DisplayColor;
    public string Description;
}
public enum SpawnMode { None, Unit, Weapon, Facility }
