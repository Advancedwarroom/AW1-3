using Godot;

public partial class City : Facility
{

    public override void _Ready()
    {
        base._Ready();

        // 补给配置：支持所有陆军单位
        healMode = 1;       // 百分比模式
        healPercent = 0.2f; // 20%最大HP
        flareAmmoSupply = 999;
        explodeAmmoSupply = 999;
        primaryAmmoSupply = 999;
        fuelSupply = 999;
        supportedUnitTypesForHeal = new Godot.Collections.Array<string>
        {
            "Infantry", "Mech", "Bike", "Oozium",
            "LightTank", "Artillery", "APC", "AntiAir",
            "AntiTank", "MdTank", "Recon", "Flare"
        };
        supportedUnitTypesForFlare = new Godot.Collections.Array<string>(supportedUnitTypesForHeal);
        supportedUnitTypesForExplode = new Godot.Collections.Array<string>(supportedUnitTypesForHeal);
        supportedUnitTypesForPrimaryAmmo = new Godot.Collections.Array<string>(supportedUnitTypesForHeal);
        supportedUnitTypesForFuel = new Godot.Collections.Array<string>(supportedUnitTypesForHeal);
    }
}
