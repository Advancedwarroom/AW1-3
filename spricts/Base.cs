using Godot;
using System;

public partial class Base : City
{
    public override void _Ready()
    {
        base._Ready();

        // 初始化生产模块默认值
        if (!canProduce && producibleUnitNames.Count == 0)
        {
            canProduce = true;
            producibleUnitNames = new Godot.Collections.Array<string>
            {
                "Infantry", "Mech", "Bike", "Oozium",
                "LightTank", "MdTank", "Rocket", "Artillery",
                "APC", "AntiAir", "Recon", "AntiTank", "Flare", "PipeRunner", "AntiAirMissile", "Dog",
                "Neotank", "Megatank", "WarTank", "Rig",
            };
        }
    }
}
