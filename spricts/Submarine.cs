// Submarine.cs - 潜艇（AW3 原作值：20000 / 移动5 / 视野5 / 弹6 / 油60）
// ✅ 下潜/上浮：直接复用隐身模块（按钮文字在检查器里改成「下潜/上浮」）。
//    下潜时贴身可见（相邻1格），只有巡洋舰和潜艇能攻击下潜潜艇（stealthAttackableBy）。
using Godot;

public partial class Submarine : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Submarine2" : "Submarine1";

    public override void _Ready()
    {
        base._Ready();
    }
}
