// Laser.cs - 高级激光炮系统（修复版）
// 修复内容：
// 1. 多次攻击同步 remainingAttacks 和 attacksRemainingInCycle
// 2. 添加局内角度配置API（EnableAngle/DisableAngle/SetFireAngles等）
// 3. ✅ 移除 attackedThisRound 限制：允许同一回合多次攻击同一目标
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Laser · Angles（由 Laser.cs 按模块拆分；partial class，Export 分组已显式标注）
public partial class Laser
{

    // ========== ✅ 局内角度配置API ==========

    public void EnableAngle(float angle)
    {
        float normalized = NormalizeAngle(angle);
        if (!enabledAngles.Contains(normalized))
        {
            enabledAngles.Add(normalized);
            enabledAngles.Sort();
            angleConfig[normalized] = true;
        }
    }

    public void DisableAngle(float angle)
    {
        float normalized = NormalizeAngle(angle);
        if (enabledAngles.Contains(normalized))
        {
            enabledAngles.Remove(normalized);
            if (currentFireAngles.Contains(normalized))
            {
                currentFireAngles.Remove(normalized);
                if (currentFireAngles.Count == 0 && enabledAngles.Count > 0)
                    currentFireAngles.Add(enabledAngles[0]);
            }
            angleConfig[normalized] = false;
            rayCache.Clear();
        }
    }

    public void SetFireAngles(List<float> angles)
    {
        currentFireAngles.Clear();
        foreach (var angle in angles)
        {
            float normalized = NormalizeAngle(angle);
            if (enabledAngles.Contains(normalized) && !currentFireAngles.Contains(normalized))
                currentFireAngles.Add(normalized);
        }
        if (currentFireAngles.Count == 0 && enabledAngles.Count > 0)
            currentFireAngles.Add(enabledAngles[0]);
        currentFireAngles.Sort((a, b) => enabledAngles.IndexOf(a).CompareTo(enabledAngles.IndexOf(b)));
        initialFireAngles.Clear();
        foreach (var angle in currentFireAngles)
            initialFireAngles[angle] = true;
        rayCache.Clear();
        UpdateLaserVisual();
    }

    public void AddFireAngle(float angle)
    {
        float normalized = NormalizeAngle(angle);
        if (enabledAngles.Contains(normalized) && !currentFireAngles.Contains(normalized))
        {
            currentFireAngles.Add(normalized);
            currentFireAngles.Sort((a, b) => enabledAngles.IndexOf(a).CompareTo(enabledAngles.IndexOf(b)));
            initialFireAngles[normalized] = true;
            rayCache.Clear();
            UpdateLaserVisual();
        }
    }

    public void RemoveFireAngle(float angle)
    {
        float normalized = NormalizeAngle(angle);
        if (currentFireAngles.Contains(normalized))
        {
            currentFireAngles.Remove(normalized);
            initialFireAngles[normalized] = false;
            rayCache.Clear();
            UpdateLaserVisual();
        }
    }

    public List<float> GetEnabledAngles() => new List<float>(enabledAngles);
    public List<float> GetCurrentFireAngles() => new List<float>(currentFireAngles);

    // ========== 角度系统 ==========

    public void InitializeAngleSystem()
    {
        enabledAngles.Clear();
        currentFireAngles.Clear();

        foreach (var kvp in angleConfig)
        {
            if (kvp.Value)
            {
                float normalizedAngle = NormalizeAngle(kvp.Key);
                if (!enabledAngles.Contains(normalizedAngle))
                    enabledAngles.Add(normalizedAngle);
            }
        }

        if (enabledAngles.Count == 0)
        {
            enabledAngles.Add(0f);
            enabledAngles.Add(90f);
            enabledAngles.Add(180f);
            enabledAngles.Add(270f);
        }

        enabledAngles.Sort();

        foreach (var kvp in initialFireAngles)
        {
            if (kvp.Value)
            {
                float normalizedAngle = NormalizeAngle(kvp.Key);
                if (enabledAngles.Contains(normalizedAngle) && !currentFireAngles.Contains(normalizedAngle))
                    currentFireAngles.Add(normalizedAngle);
            }
        }

        if (currentFireAngles.Count == 0 && enabledAngles.Count > 0)
            currentFireAngles.Add(enabledAngles[0]);

        currentFireAngles.Sort();
    }

    private float NormalizeAngle(float angle)
    {
        while (angle < 0) angle += 360;
        while (angle >= 360) angle -= 360;
        return angle;
    }

    public void RotateLaser(bool clockwise)
    {
        if (!canRotate || enabledAngles.Count == 0) return;

        var newFireAngles = new List<float>();
        foreach (float currentAngle in currentFireAngles)
        {
            int currentIndex = enabledAngles.IndexOf(currentAngle);
            if (currentIndex < 0) continue;
            int newIndex = clockwise
                ? (currentIndex + 1) % enabledAngles.Count
                : (currentIndex - 1 + enabledAngles.Count) % enabledAngles.Count;
            float newAngle = enabledAngles[newIndex];
            if (!newFireAngles.Contains(newAngle))
                newFireAngles.Add(newAngle);
        }
        newFireAngles.Sort((a, b) => enabledAngles.IndexOf(a).CompareTo(enabledAngles.IndexOf(b)));
        currentFireAngles = newFireAngles;
        rayCache.Clear();
        UpdateLaserVisual();
    }

    // ✅ 新增：公共方法，供 TerrainEditor 应用角度配置变更
    public void ApplyAngleConfigChanges()
    {
        InitializeAngleSystem();
        rayCache.Clear();
        UpdateLaserVisual();
    }
}
