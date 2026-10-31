# 格战全单位伤害矩阵（自动提取自 Infantry.cs defaultMatrix）

> 攻击方 21 种 × 共 288 条。0 = 不能攻击该目标。副武器（机枪等）在 defaultSecondaryMatrix，另行查询。

## 一、攻击视角（我打谁疼）

| 攻击方 | 能打的目标（值=对满血10HP目标的伤害%） |
|---|---|
| APC | （全0，无攻击力） |
| AntiAir | FlyBomb:120 BCopter:120 TCopter:120 Infantry:105 Mech:105 Bike:105 PipeRunner:85 Bomber:75 Stealth:75 Fighter:65 Recon:60 Rocket:55 AntiAirMissile:55 Artillery:50 APC:50 Flare:50 AntiAir:45 LightTank:25 AntiTank:25 Oozium:15 MdTank:10 |
| AntiAirMissile | FlyBomb:120 Fighter:100 Bomber:100 BCopter:100 TCopter:100 Stealth:100 |
| AntiTank | AntiAirMissile:85 PipeRunner:85 Infantry:75 Recon:75 AntiAir:75 LightTank:75 AntiTank:75 Flare:75 Rocket:70 Mech:65 MdTank:65 Artillery:65 APC:65 Bike:65 |
| Artillery | Infantry:90 Mech:85 Bike:85 LightTank:80 Recon:80 AntiAirMissile:80 Rocket:75 Flare:75 Artillery:70 APC:70 PipeRunner:70 AntiTank:55 RepairBoat:55 MdTank:45 Oozium:15 |
| BCopter | Artillery:65 Rocket:65 AntiAirMissile:65 APC:60 Recon:55 LightTank:55 AntiTank:55 PipeRunner:55 Flare:55 Bike:55 MdTank:25 AntiAir:25 Oozium:25 RepairBoat:25 |
| Bike | AntiAirMissile:10 PipeRunner:4 |
| Bomber | Infantry:110 Mech:110 Bike:105 Recon:105 APC:105 LightTank:105 Artillery:105 Rocket:105 AntiTank:105 PipeRunner:105 Flare:105 AntiAirMissile:105 MdTank:95 AntiAir:95 RepairBoat:75 Oozium:35 |
| Fighter | FlyBomb:100 Bomber:100 BCopter:100 TCopter:100 Stealth:85 Fighter:55 |
| Flare | AntiAirMissile:28 PipeRunner:12 |
| FlyBomb | （全0，无攻击力） |
| Infantry | Infantry:55 Mech:45 Oozium:35 Artillery:25 Rocket:25 AntiAirMissile:25 LightTank:15 APC:15 Recon:12 MdTank:4 |
| LightTank | Recon:85 AntiAirMissile:85 Flare:80 Infantry:75 Mech:70 Bike:70 LightTank:55 Rocket:55 PipeRunner:55 Artillery:45 APC:45 AntiTank:30 Oozium:25 MdTank:17 RepairBoat:10 |
| MdTank | Infantry:110 Artillery:105 Rocket:105 APC:105 Recon:105 AntiAirMissile:105 Mech:95 Flare:90 LightTank:85 PipeRunner:85 Bike:80 MdTank:55 Oozium:45 AntiTank:35 RepairBoat:35 |
| Mech | LightTank:85 Recon:85 AntiAirMissile:85 Flare:80 Infantry:65 Rocket:65 APC:65 Mech:55 Artillery:55 Oozium:55 AntiTank:55 Bike:55 PipeRunner:55 MdTank:15 |
| Oozium | Infantry:999 Mech:999 LightTank:999 Artillery:999 Rocket:999 APC:999 Oozium:999 MdTank:999 Recon:999 Flare:999 Bike:999 |
| PipeRunner | FlyBomb:120 Fighter:120 BCopter:120 TCopter:120 Infantry:95 Mech:90 Recon:90 AntiAirMissile:90 Bike:90 Rocket:85 AntiAir:85 LightTank:80 APC:80 Artillery:80 PipeRunner:80 Stealth:75 Flare:75 AntiTank:60 RepairBoat:60 MdTank:55 |
| Recon | Infantry:70 Mech:65 Rocket:55 Artillery:45 APC:45 Recon:35 AntiAirMissile:28 Oozium:11 LightTank:6 AntiAir:4 MdTank:1 |
| RepairBoat | （全0，无攻击力） |
| Rocket | Infantry:95 Mech:90 LightTank:90 Recon:90 Bike:90 AntiAirMissile:90 Rocket:85 Flare:85 Artillery:80 APC:80 PipeRunner:80 AntiTank:65 RepairBoat:60 MdTank:55 Oozium:20 |
| Stealth | FlyBomb:120 BCopter:105 TCopter:105 Infantry:95 Mech:90 Bike:90 Recon:90 Rocket:85 AntiAir:85 APC:80 LightTank:80 Artillery:80 AntiTank:80 PipeRunner:80 Flare:80 Bomber:75 Stealth:75 Fighter:65 RepairBoat:60 MdTank:55 Oozium:15 |
| TCopter | （无条目） |

## 二、防守视角（谁打我疼——给机械狗定防御表的参照）

| 被击方 | 最大威胁（前5） | 最小威胁 |
|---|---|---|
| APC | Oozium:999 MdTank:105 Bomber:105 Rocket:80 PipeRunner:80 | APC:0 FlyBomb:0 RepairBoat:0 |
| AntiAir | Bomber:95 PipeRunner:85 Stealth:85 AntiTank:75 AntiAir:45 | FlyBomb:0 RepairBoat:0 Recon:4 |
| AntiAirMissile | MdTank:105 Bomber:105 Rocket:90 PipeRunner:90 Mech:85 | Bike:10 Infantry:25 Recon:28 |
| AntiTank | Bomber:105 Stealth:80 AntiTank:75 Rocket:65 PipeRunner:60 | Infantry:0 Recon:0 APC:0 |
| Artillery | Oozium:999 MdTank:105 Bomber:105 Rocket:80 PipeRunner:80 | APC:0 FlyBomb:0 RepairBoat:0 |
| BCopter | AntiAir:120 PipeRunner:120 Stealth:105 Fighter:100 AntiAirMissile:100 | Fighter:100 AntiAirMissile:100 Stealth:105 |
| Bike | Oozium:999 AntiAir:105 Bomber:105 Rocket:90 Stealth:90 | RepairBoat:0 Mech:55 BCopter:55 |
| Bomber | Fighter:100 AntiAirMissile:100 AntiAir:75 Stealth:75 | AntiAir:75 Stealth:75 Fighter:100 |
| Fighter | PipeRunner:120 AntiAirMissile:100 AntiAir:65 Stealth:65 Fighter:55 | Fighter:55 AntiAir:65 Stealth:65 |
| Flare | Oozium:999 Bomber:105 MdTank:90 Rocket:85 Mech:80 | RepairBoat:0 AntiAir:50 BCopter:55 |
| FlyBomb | AntiAir:120 PipeRunner:120 AntiAirMissile:120 Stealth:120 Fighter:100 | FlyBomb:0 Infantry:0 Mech:0 |
| Infantry | Oozium:999 MdTank:110 Bomber:110 AntiAir:105 Rocket:95 | APC:0 FlyBomb:0 RepairBoat:0 |
| LightTank | Oozium:999 Bomber:105 Rocket:90 Mech:85 MdTank:85 | APC:0 FlyBomb:0 RepairBoat:0 |
| MdTank | Oozium:999 Bomber:95 AntiTank:65 Rocket:55 MdTank:55 | APC:0 FlyBomb:0 RepairBoat:0 |
| Mech | Oozium:999 Bomber:110 AntiAir:105 MdTank:95 Rocket:90 | APC:0 FlyBomb:0 RepairBoat:0 |
| Oozium | Oozium:999 Mech:55 MdTank:45 Infantry:35 Bomber:35 | APC:0 FlyBomb:0 RepairBoat:0 |
| PipeRunner | Bomber:105 AntiAir:85 MdTank:85 AntiTank:85 Stealth:80 | RepairBoat:0 Bike:4 Flare:12 |
| Recon | Oozium:999 MdTank:105 Bomber:105 Rocket:90 PipeRunner:90 | APC:0 FlyBomb:0 RepairBoat:0 |
| RepairBoat | Bomber:75 Stealth:60 PipeRunner:60 Rocket:60 Artillery:55 | RepairBoat:0 Infantry:0 Mech:0 |
| Rocket | Oozium:999 MdTank:105 Bomber:105 Rocket:85 PipeRunner:85 | APC:0 FlyBomb:0 RepairBoat:0 |
| Stealth | AntiAirMissile:100 Fighter:85 Stealth:75 AntiAir:75 PipeRunner:75 | Stealth:75 AntiAir:75 PipeRunner:75 |
| TCopter | AntiAir:120 PipeRunner:120 Stealth:105 Fighter:100 AntiAirMissile:100 | Fighter:100 AntiAirMissile:100 Stealth:105 |