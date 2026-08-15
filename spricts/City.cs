using Godot;

public partial class City : Facility
{
    [Export] public int fundsPerTurn = 1000;
    private AnimatedSprite2D animSprite;
    private Sprite2D baseSprite;
    private Sprite2D normalSprite;

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

        animSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        baseSprite = GetNodeOrNull<Sprite2D>("BaseSprite");
        normalSprite = GetNodeOrNull<Sprite2D>("NormalSprite");
        if (animSprite == null && baseSprite == null)
        {
            GD.PushError(TranslateGroup.T("[City] 找不到任何视觉节点！"));
            return;
        }

        UpdateCityVisual();
        ApplyPaletteMaterial();
    }
    
    /// <summary>
    /// ✅ 为城市应用调色板Shader（双图层 + 兼容模式）
    /// </summary>
    private void ApplyPaletteMaterial()
    {
        // ✅ 双图层方案
        if (baseSprite != null && IsInstanceValid(baseSprite) &&
            normalSprite != null && IsInstanceValid(normalSprite))
        {
            var baseMat = FactionPaletteManager.GetBaseLayerMaterial(facilityTeam);
            if (baseMat != null)
            {
                baseSprite.Material = (ShaderMaterial)baseMat.Duplicate();
                baseSprite.Show();
            }
            normalSprite.Material = null;
            normalSprite.Show();
            return;
        }
        
        // Fallback：兼容模式
        if (animSprite == null) return;
        
        var mat = FactionPaletteManager.GetMaterial(facilityTeam);
        if (mat == null) return;
        
        var cityMat = (ShaderMaterial)mat.Duplicate();
        animSprite.Material = cityMat;
    }

    public override void UpdateCityVisual()
    {
        if (animSprite == null) return;

        string animName = facilityTeam switch
        {
            "Player1" => "cityteam1",
            "Player2" => "cityteam2",
            "Player0" => "P0City",
            "Player" => "cityteam1",
            _ => "cityteam1"
        };

        if (animSprite.SpriteFrames.HasAnimation(animName))
        {
            animSprite.Play(animName);
        }
        else
        {
            GD.PushWarning($"[City] 动画 '{animName}' 不存在于 SpriteFrames 中");
        }

        // ✅ LUT模式：通过Shader tone参数调整（替代Modulate）
        if (animSprite.Material is ShaderMaterial shaderMat)
        {
            Color tint = (facilityTeam == TeamHelper.Player)
                ? TeamHelper.GetTeamColor(facilityTeam)
                : new Color(1f, 1f, 1f, 1f);
            shaderMat.SetShaderParameter("tone", tint);
        }
        else
        {
            // Fallback: 旧模式
            Color tint = (facilityTeam == TeamHelper.Player)
                ? TeamHelper.GetTeamColor(facilityTeam)
                : Colors.White;
            animSprite.Modulate = tint;
        }
    }
}
