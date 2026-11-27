# Plain Craft Launcher (PCL) 小白修改版

> 这是一个**第三方修改版**，基于 [龙腾猫跃](https://meloong.com/) 的 PCL 2.99.0.1 开源代码制作。
> **非官方项目，与 PCL 官方及龙腾猫跃无任何隶属或合作关系。** 官方版本请前往 [PCL 下载页](https://meloong.com/pcl)。

本项目的目标只有一个：**把 Minecraft 的使用门槛降到最低**，让没接触过启动器的新手也能几步进游戏。

## 现在能做什么

- **一键安装**：在「下载 → 一键安装」里选「玩原版 / 加模组」，再选 Fabric 或 Forge，选好游戏版本，点一次「开始安装」。
- 安装完成后按钮直接变成「启动游戏」，不用再回主页找版本。
- 首次使用自动推荐最新正式版，自动开版本隔离。
- 其余功能沿用 PCL 上游：原版下载、Mod / 整合包 / 资源包 / 光影包下载、微软与离线登录、Java 自动选择、崩溃日志分析等。

## 构建

无需安装 Visual Studio，只需要：

- .NET SDK（含 MSBuild）
- .NET Framework 4.8 引用程序集（`C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8`）

```bash
git clone --recursive https://github.com/txdgw80233-sudo/pcl-easy.git
cd pcl-easy
bash build/build-pcl.sh Debug
```

产物在 `Plain Craft Launcher 2/bin/Plain Craft Launcher 2.exe`。构建脚本的来历与原理见 [build/README.md](build/README.md)。

## 已知限制

- **微软正版登录**：需要自己的 Azure 应用 Client ID，且该 Client ID 须通过 Minecraft Services 的应用审核，否则登录会在最后一步返回 `403 Invalid app registration`。审核入口：<https://aka.ms/mce-reviewappid>。审核通过前请使用离线登录。
- **CurseForge**：需要自备 API Key（`PCL_CURSEFORGE_API_KEY`）。
- **BMCLAPI 镜像**：部分网络环境下不可达，会自动回退到 Mojang 官方源。

## 许可与声明

- 本项目基于 PCL 源代码修改，**完整保留**上游 [LICENCE](LICENCE)，并遵守其中对第三方二次创作的全部要求：
  - 名称以「Plain Craft Launcher (PCL)」开头并带第三方后缀；
  - 关于页保留对龙腾猫跃的署名与赞助入口；
  - Windows 下主色相保持蓝色；
  - 保留正版购买劝导，不提供绕过正版验证的功能。
- 本项目**不提供**联机服务、不托管游戏资源、不修改游戏本体。
- 这是一个个人项目，按现状提供，不承诺可用性，也不提供商业支持。

## 反馈

有 Bug 或建议请开 [Issue](https://github.com/txdgw80233-sudo/pcl-easy/issues)。
