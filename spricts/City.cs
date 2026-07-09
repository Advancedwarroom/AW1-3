using Godot;

public partial class City : Facility
{
    private AnimatedSprite2D animSprite;

    public override void _Ready()
    {
        base._Ready();

        animSprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
        if (animSprite == null)
        {
            GD.PushError("[City] 找不到 AnimatedSprite2D 节点！");
            return;
        }

        UpdateCityVisual();
    }

    public override void UpdateCityVisual()
    {
        if (animSprite == null) return;

        string animName = facilityTeam switch
        {
            "Player1" => "cityteam1",
            "Player2" => "cityteam2",
            "Player0" => "cityteam1",
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

        // 应用势力色调（P0灰白、P-1浅紫）
        Color tint = (facilityTeam == TeamHelper.Player0 || facilityTeam == TeamHelper.Player)
            ? TeamHelper.GetTeamColor(facilityTeam)
            : Colors.White;
        animSprite.Modulate = tint;

        GD.Print($"[City] 城市 {ParentGrid?.GridIndex} 视觉切换为 {facilityTeam} -> 动画 {animName}");
    }
}
