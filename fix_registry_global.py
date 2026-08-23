path = r'C:\Users\honor\Documents\aw-one-new\spricts\Infantry.cs'
with open(path, 'r', encoding='utf-8') as f:
    content = f.read()

old = '''    // 武器 - 主
    hasPrimaryWeapon = config.HasPrimaryWeapon;
    if (config.HasPrimaryWeapon)
    {
        maxPrimaryAmmo = config.MaxPrimaryAmmo;
        primaryHasLimitedAmmo = config.PrimaryHasLimitedAmmo;
        if (currentPrimaryAmmo <= 0) currentPrimaryAmmo = maxPrimaryAmmo;
        primaryAntiArmor = config.PrimaryAntiArmor;
        primaryAntiInfantry = config.PrimaryAntiInfantry;
    }
    if (config.BaseAttack > 0) baseAttack = config.BaseAttack;

    // 武器 - 副
    hasSecondaryWeapon = config.HasSecondaryWeapon;
    if (config.HasSecondaryWeapon && config.SecondaryAttack > 0)
        secondaryAttack = config.SecondaryAttack;
    secondaryAntiArmor = config.SecondaryAntiArmor;
    secondaryAntiInfantry = config.SecondaryAntiInfantry;

    // 射程
    if (config.UseMinMaxAttackRange)
    {
        useMinMaxAttackRange = true;
        minAttackRange = config.MinAttackRange;
        maxAttackRange = config.MaxAttackRange;
        attackRange = maxAttackRange;
    }
    canAttackAfterMoving = config.CanAttackAfterMoving;
    cannotCounterWhenAttacked = config.CannotCounterWhenAttacked;
    canCounterWhenDefending = config.CanCounterWhenDefending;
    canCounterAtRange = config.CanCounterAtRange;

    // 燃料
    if (config.MaxFuel != 99) maxFuel = config.MaxFuel;
    consumeFuel = config.ConsumeFuel;
    if (config.DailyFuelConsumption > 0) dailyFuelConsumption = config.DailyFuelConsumption;
    destroyOnOutOfFuel = config.DestroyOnOutOfFuel;
    if (config.LowFuelThreshold != 15) lowFuelThreshold = config.LowFuelThreshold;

    // 防御
    if (config.BaseDefense > 0) baseDefense = config.BaseDefense;
    if (config.CounterMul != 0.5f) counterMul = config.CounterMul;

    // 占领
    captureAbility = config.CaptureAbility;
    if (config.CapturePower > 0) capturePower = config.CapturePower;

    // 特殊能力
    canDevour = config.CanDevour;
    if (config.CanIlluminate) canIlluminate = true;

    // 重叠规则
    overlapType = config.OverlapType;
    attackType = config.AttackType;
    isArmoredUnit = config.IsArmoredUnit;
    IsAirUnit = config.IsAirUnit;'''

new = '''    // 武器 - 主（只在 UnitRegistry 明确开启时覆盖，不强制关闭）
    if (config.HasPrimaryWeapon)
    {
        hasPrimaryWeapon = true;
        maxPrimaryAmmo = config.MaxPrimaryAmmo;
        primaryHasLimitedAmmo = config.PrimaryHasLimitedAmmo;
        if (currentPrimaryAmmo <= 0) currentPrimaryAmmo = maxPrimaryAmmo;
        primaryAntiArmor = config.PrimaryAntiArmor;
        primaryAntiInfantry = config.PrimaryAntiInfantry;
    }
    if (config.BaseAttack > 0) baseAttack = config.BaseAttack;

    // 武器 - 副（只在 UnitRegistry 明确开启时覆盖，不强制关闭）
    if (config.HasSecondaryWeapon)
    {
        hasSecondaryWeapon = true;
        if (config.SecondaryAttack > 0) secondaryAttack = config.SecondaryAttack;
    }
    if (config.SecondaryAntiArmor) secondaryAntiArmor = true;
    if (config.SecondaryAntiInfantry) secondaryAntiInfantry = true;

    // 射程
    if (config.UseMinMaxAttackRange)
    {
        useMinMaxAttackRange = true;
        minAttackRange = config.MinAttackRange;
        maxAttackRange = config.MaxAttackRange;
        attackRange = maxAttackRange;
    }
    if (config.CanAttackAfterMoving) canAttackAfterMoving = true;
    if (config.CannotCounterWhenAttacked) cannotCounterWhenAttacked = true;
    if (config.CanCounterWhenDefending) canCounterWhenDefending = true;
    if (config.CanCounterAtRange) canCounterAtRange = true;

    // 燃料
    if (config.MaxFuel != 99) maxFuel = config.MaxFuel;
    if (config.ConsumeFuel) consumeFuel = true;
    if (config.DailyFuelConsumption > 0) dailyFuelConsumption = config.DailyFuelConsumption;
    if (config.DestroyOnOutOfFuel) destroyOnOutOfFuel = true;
    if (config.LowFuelThreshold != 15) lowFuelThreshold = config.LowFuelThreshold;

    // 防御
    if (config.BaseDefense > 0) baseDefense = config.BaseDefense;
    if (config.CounterMul != 0.5f) counterMul = config.CounterMul;

    // 占领（只在 UnitRegistry 明确指定时覆盖）
    if (config.CaptureAbility != default) captureAbility = config.CaptureAbility;
    if (config.CapturePower > 0) capturePower = config.CapturePower;

    // 特殊能力（只在 UnitRegistry 明确开启时覆盖）
    if (config.CanDevour) canDevour = true;
    if (config.CanIlluminate) canIlluminate = true;

    // 重叠规则（只在 UnitRegistry 明确指定时覆盖，不强制覆盖默认值）
    if (config.OverlapType != default) overlapType = config.OverlapType;
    if (config.AttackType != default) attackType = config.AttackType;
    if (config.IsArmoredUnit) isArmoredUnit = true;
    if (config.IsAirUnit) IsAirUnit = true;'''

if old in content:
    content = content.replace(old, new)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(content)
    print("Fixed ApplyUnitRegistryConfig: unconditional overwrites removed")
else:
    print("Pattern not found")
