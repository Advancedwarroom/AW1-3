using Godot;
using System;

public partial class AirPort : City
{
    // ✅ 检查器优先：默认补给兵种列表重写为空军（仅当检查器未配置时才使用）
    protected override string[] DefaultSupplyUnitTypes() => new string[]
    {
        "FlyBomb", "Fighter", "Bomber", "BCopter", "TCopter", "Stealth"
    };

    public override void _Ready()
    {
        base._Ready();

        animSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animSprite == null)
        {
            GD.PushError(TranslateGroup.T("[AirPort] 找不到 AnimatedSprite2D 节点！"));
            return;
        }

        // 初始化生产模块默认值（检查器优先）
        if (producibleUnitNames.Count == 0)
        {
            canProduce = true;
            producibleUnitNames = new Godot.Collections.Array<string> { "FlyBomb", "Fighter", "Bomber", "BCopter", "TCopter", "Stealth" };
        }

        UpdateCityVisual();
    }

    public override void UpdateCityVisual()
    {
        if (animSprite == null) return;

        string animName = facilityTeam switch
        {
            "Player1" => "airportteam1",
            "Player2" => "airportteam2",
            "Player0" => "P0AirPort",
            "Player" => "airportteam1",
            _ => "P0AirPort"
        };

        if (animSprite.SpriteFrames.HasAnimation(animName))
        {
            animSprite.Play(animName);
        }
        else if (animSprite.SpriteFrames.HasAnimation("P0AirPort"))
        {
            animSprite.Play("P0AirPort");
        }

        Color tint = (facilityTeam == TeamHelper.Player)
            ? TeamHelper.GetTeamColor(facilityTeam)
            : Colors.White;
        animSprite.Modulate = tint;

    }
}
