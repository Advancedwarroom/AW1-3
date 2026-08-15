// MdTank.cs
using Godot;

public partial class MdTank : Infantry
{
    [Export] public AnimatedSprite2D MdSprite;

    public override void _Ready()
    {
        base._Ready();
        if (animSprite == null && MdSprite != null)
            animSprite = MdSprite;
    }

    public override string GetIdleAnimName() => team == "Player2" ? "Md2" : "Md1";
}
