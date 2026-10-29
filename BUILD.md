# AWone1 安卓云打包手册

> 2026-10-27 深夜实战产出。每一行配置都是踩坑换来的，**勿动「关键配置」章节**。

## 日常使用（全程手机）

1. **改代码**：Acode / VSCodroid 里编辑 `.cs` 文件
2. **推送**：git push 到 main
3. **收包**：`Actions` → 最新运行 → 最下面 `Artifacts → AWone1-android` → 下载安装（约 2~15 分钟）
4. **红了**：去失败运行的 `Artifacts → export-log` 拿日志排障

## 流水线架构（workflow 每步在干嘛）

| 步骤 | 作用 |
|---|---|
| 装 .NET 10 / JDK 17 / Android SDK | 构建链三件套（SDK 装独立目录，双环境变量指向同一处） |
| 下载 Godot 4.7.1 Mono 无头版 | 官方 linux x86_64 mono 编辑器，命令行跑 |
| 装导出模板 (tpz) | 解压到 `~/.local/share/godot/export_templates/4.7.1.stable.mono` |
| 装 Gradle 构建模板 | `android_source.zip` → `android/build/` 子目录 + 写 `.build_version` + `.gdignore` |
| 修导出预设 | 只开 `gradle_build/use_gradle_build=true` |
| 导入资源 → 杀 MSBuild → 手动发布 → 导出 | 顺序不能乱：预热 NuGet 缓存 + 防文件锁 |

## 关键配置（雷区，已全部排掉）

- **Mono + `android-arm64`**：Godot 用自己的胶水宿主 Mono，不需要 libmonodroid
- **`.NET 10 SDK + net10.0 + UseMonoRuntime=true`**
- ❌ **永远不要开 `dotnet/android_use_linux_bionic`**：bionic 的 Mono 包（`Microsoft.NETCore.App.Runtime.Mono.linux-arm64`）微软从未发布，一开必炸 NU1102。PC 上能导出是因为包装进了本地 NuGet 缓存
- ❌ CoreCLR+bionic 也别走：`libcoreclr.so` 是个壳，运行时 dlopen 不存在的 `libmonodroid.so`，闪退
- **Gradle 构建必须开**（C# 安卓唯一正路），且模板必须在 `android/build/` **子目录**（Godot 检查的是 `res://android/build/build.gradle`）
- **C# 安卓导出不能用安卓版 Godot 编辑器开发**（Android 禁 JIT，编辑器跑不了 C#）——这是整个云打包方案存在的根本原因

## 维护备忘

- **升级 Godot**：改 workflow 顶部 `GODOT_VERSION` + csproj 里 `Godot.NET.Sdk` 版本号
- **PC 端打开工程**：需安装 .NET 10 SDK（项目已改 net10.0）
- **发正式版**：把 workflow 里 `keytool` 生成 debug 签名那步，换成正式 keystore（存 GitHub Secrets）
- **GitHub 免费额度**：公开仓库 Actions 无限用；私有仓库 2000 分钟/月，一次构建约 2~15 分钟

## 事故档案（本次排掉的雷，按时间序）

1. Godot Android 编辑器不支持 C#（系统禁 JIT）→ 云打包方案立项
2. 云打包必需的 Actions 写权限在 GitHub 手机网页端存不住 → 放弃 token，改用手动网页编辑
3. `setup-android` action 有 bug（sdkmanager 找不到 platform-tools）→ 手动装 SDK
4. YAML 缩进 → 整文件重贴
5. Gradle 模板装错层级 → 对照 Godot 源码确认 `res://android/build/`
6. 系统自带 SDK 与自建 SDK 环境变量打架 → 双变量统一
7. **终极 BOSS**：bionic 死路（见上）→ 回 Mono 正路，全线贯通

—— 由 AWone1 的"开创人"与 Kimi 在 2026-10-28 03:01 共同签署 ✅
