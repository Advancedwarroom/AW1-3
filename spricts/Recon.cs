// Recon.cs
using Godot;

public partial class Recon : Infantry
{
    public override string GetIdleAnimName() => team == "Player2" ? "Recon2" : "Recon1";

    public override int CalculateDamageAgainstWeapon(Weapon target, WeaponType weaponType)
    {
        float healthPercent = (float)health / maxHealth;
        return Mathf.Max(1, Mathf.RoundToInt(35 * healthPercent));
    }
}
