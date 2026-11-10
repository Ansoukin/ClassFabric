<!--markdownlint-disable MD001 MD033 MD041 MD051-->

<div align="center">

# <image src="ClassIsland/Assets/AppLogo_AppLogo.svg" height="28" width="28"/> ClassFabric

<!-- ![软件截图 - 主界面](https://github.com/ClassIsland/ClassIsland/assets/55006226/65e2bdba-be83-444c-b42f-b893aaace9c3) -->

![Banner](https://res.classisland.tech/banners/banner-v2-1.webp)

[![Stars](https://img.shields.io/github/stars/Ansoukin/ClassFabric?label=Stars)](https://github.com/Ansoukin/ClassFabric)
[![正式版 Release](https://img.shields.io/github/v/release/Ansoukin/ClassFabric?style=flat-square&color=%233fb950&label=正式版)](https://github.com/Ansoukin/ClassFabric/releases/latest)
[![测试版 Release](https://img.shields.io/github/v/release/Ansoukin/ClassFabric?include_prereleases&style=flat-square&label=测试版)](https://github.com/Ansoukin/ClassFabric/releases/)
[![下载量](https://img.shields.io/github/downloads/Ansoukin/ClassFabric/total?style=social&label=下载量&logo=github)](https://github.com/Ansoukin/ClassFabric/releases/latest)<br/>
![.NET 版本](https://img.shields.io/badge/.NET-10-512bd4?style=flat-square)
![GitHub Repo size](https://img.shields.io/github/repo-size/Ansoukin/ClassFabric?style=flat-square&color=3cb371)
[![GitHub Repo Languages](https://img.shields.io/github/languages/top/Ansoukin/ClassFabric?style=flat-square)](https://github.com/Ansoukin/ClassFabric/search?l=c%23)

ClassFabric 是一款适用于班级多媒体屏幕的课表信息显示工具，在 Windows 设备上即可呈现课表、提醒、天气等信息。<br/>
名字中的 “Fabric” 取自 Minecraft 模组加载器 Fabric，寓意把社区与个人的功能连接在一起。

> [!NOTE]
> ClassFabric 是 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 的个人修改版。我们持续同步上游的最新功能，并在此基础上加入个人与社区的改动。
>
> 底层代码与上游保持一致，从 ClassIsland 切换过来无需复杂设置：已有的课表、自动化、插件和主题都能直接沿用。
>
> 文中指向 ClassIsland 官网、文档、社区的链接属于**上游资源**，内容同样适用于本项目；但上游站点的下载入口提供的是 ClassIsland，不是 ClassFabric。请从下方的 GitHub Releases 下载本项目。

#### 💬[ClassIsland QQ 频道](https://pd.qq.com/s/grr6qwqwj) | [ClassIsland QQ 群组 1 群](https://qm.qq.com/q/4NsDQKiAuQ) | [2 群](https://qm.qq.com/q/MhWCvrk7mu)

#### [🌐 ClassFabric 官网](https://cf.ansoukin.top) | [🌐 上游官网](https://classisland.tech/) | [📚 上游文档](https://docs.classisland.tech) | [🚀 上游下载](https://classisland.tech/download) | [🗳 上游功能投票](https://github.com/ClassIsland/voting/discussions?discussions_q=is%3Aopen+sort%3Atop)

###### [观看上游介绍视频，快速了解 ClassIsland →](https://www.bilibili.com/video/BV12fFoefEGn/)

</div>

## 功能

> [!TIP]
>
> 您可以点击下方链接，或查看上游的 [ClassIsland 文档](https://docs.classisland.tech)（同样适用于 ClassFabric）了解更多。

### 课表信息显示

- [x] 显示当天的课表与正在进行的课程信息
- [x] 在上下课等重要时间点发出 [提醒](https://docs.classisland.tech/app/notifications)，可自选音效、强调特效、语音与置顶效果，打造 [强调提醒](https://docs.classisland.tech/app/notifications#强调提醒)
- [x] 可自定义课表的隐藏条件，支持临时隐藏与鼠标穿透，不影响授课

### 课表编辑与管理

- [x] 简洁直观的 [课表编辑工具](https://docs.classisland.tech/app/classplan)
- [x] 从 Excel 表格、[CSES](https://github.com/SmartTeachCN/CSES) 或其他软件 [导入课表](https://docs.classisland.tech/app/profile/#%E4%BB%8E%E8%A1%A8%E6%A0%BC%E5%AF%BC%E5%85%A5)
- [x] 将课表信息导出到 Excel 表格
- [x] 多周轮换、快速录入时间表、丰富的自定义设置
- [x] 单日或跨天临时换课
- [x] 提前预定需要临时启用的课表

### 自定义

- [x] 通过 [组件](https://docs.classisland.tech/app/basic#组件)（日期、时间、天气简报、倒计日等）自定义显示的内容，支持多行显示、组件轮播与组件滚动
- [x] 通过 [插件](https://docs.classisland.tech/app/basic#组件) 扩展应用功能，打造专属于你的 ClassFabric
- [x] 通过主题系统定制主界面外观

### 其它功能

- [x] 通过[自动化](https://docs.classisland.tech/app/automation.html)在特定事件或特定时间自动执行操作，如显示提醒、打开应用、文件或网页，提升教学效率
- [x] 显示当前的[天气](https://docs.classisland.tech/app/advanced#天气)信息、降水提示、6 小时 / 3 天天气预报与极端天气预警，支持根据定位获取
- [x] 自动同步软件时间、手动对齐铃声
- [x] 使用密码等认证方式保护应用设置和课表配置
- [x] 丝滑流畅的过渡动画
- [x] 自动获取与系统配色搭配的主题色
- [x] 自动软件更新

### ClassFabric 的改动

- [x] 多语言界面：基于 Crowdin，已支持简体中文、繁体中文与英文（实验性，仍有部分界面在适配中）
- [x] 更多天气源：除内置的小米天气外，新增 OpenWeather 与和风天气（QWeather）
- [x] 更清晰的插件市场详情预览
- [ ] 云功能（计划中）：远程提醒、云端集中管理、云备份
- [ ] ……

## 软件截图

> 截图来自上游 ClassIsland，界面以 ClassFabric 实际版本为准。背景图片来自 [Pixiv@辰暮 sora](https://pixiv.net/artworks/110847880)

### 主界面

##### 1. 软件效果

![软件截图 - 整体效果](https://res.classisland.tech/screenshots/ClassIsland/Snipaste_2025-03-28_11-47-04.png)

##### 2. 演示上课提醒视频

<video src="https://res.classisland.tech/screenshots/ClassIsland/notifications.mp4" loop label="软件截图 - 上课提醒" autoplay muted></video>

<details>
<summary>查看更多软件截图……</summary>

### 档案编辑器

##### 1. 课表编辑

![软件截图 - 课表编辑](https://res.classisland.tech/screenshots/ClassIsland/1.png)

##### 2. 时间表编辑

![软件截图 - 时间表编辑](https://res.classisland.tech/screenshots/ClassIsland/3.png)

##### 3. 科目编辑

![软件截图 - 科目编辑](https://res.classisland.tech/screenshots/ClassIsland/4.png)

##### 4. 预定调课

![软件截图 - 跨天换课](https://res.classisland.tech/screenshots/ClassIsland/5.png)

![软件截图 - 预定课表](https://res.classisland.tech/screenshots/ClassIsland/6.png)

### 设置界面

##### 1. 基本设置

![软件截图 - 应用设置](https://res.classisland.tech/screenshots/ClassIsland/Snipaste_2025-03-29_09-33-56.png)

##### 2. 组件设置

![软件截图 - 组件设置](https://res.classisland.tech/screenshots/ClassIsland/Snipaste_2025-03-28_11-47-25.png)

</details>

## 开始使用

**开始之前，请确认您的设备满足以下要求：**

- Windows 10 或更高版本

框架依赖（`full`）版本需要先安装 [.NET 10.0 桌面运行时](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0)；自包含（`selfContained`）版本已内置运行时，无需另外安装。

ClassFabric 与部分会修改窗口材质的窗口美化工具（如 Mica For Everyone）**不兼容**。如需同时使用，请把 ClassFabric 加入这些工具的排除列表。

> [!IMPORTANT]
> **详细安装说明请参阅上游的 [ClassIsland 安装指南](https://docs.classisland.tech/app/setup)。**
>
> 不建议在低于 Windows 10 的系统上运行本应用。在 Windows 7 中，.NET 运行时会出现**严重的内存泄漏问题**；如果仍要在 Windows 7 上使用，请参阅 [在 Windows 7 中安装 ClassIsland](https://docs.classisland.tech/app/setup#检查系统需求)。


您可以通过以下渠道获取本软件，请根据自己的网络环境选择。

- [**GitHub Releases（推荐）**](https://github.com/Ansoukin/ClassFabric/releases/)（在 Release 的 Assets 中下载安装包）
- [上游 ClassIsland 官网](https://classisland.tech/download)（该入口提供的是 ClassIsland，不是 ClassFabric）

## 获取帮助＆加入社区

遇到问题时，可以先查阅**帮助文档**：

- [ClassIsland 帮助文档](https://docs.classisland.tech/app)（上游文档，同样适用于 ClassFabric）

也可以到下面这些地方**寻求帮助**（QQ 频道与群组属于上游 ClassIsland 社区）：

[![GitHub Issues](https://img.shields.io/github/issues-search/Ansoukin/ClassFabric?query=is%3Aopen&style=flat-square&logo=github&label=Issues&color=%233fb950)](https://github.com/Ansoukin/ClassFabric/issues)
[![GitHub Discussions](https://img.shields.io/github/discussions/Ansoukin/ClassFabric?style=flat-square&logo=Github&label=Discussions)](https://github.com/Ansoukin/ClassFabric/discussions)
[![加入 QQ 频道](https://img.shields.io/badge/ClassIsland_QQ_%E9%A2%91%E9%81%93-classisland-%230066cc?style=flat-square&logo=TencentQQ)](https://pd.qq.com/s/scb3wzia)
[![加入 QQ 群](https://img.shields.io/badge/ClassIsland_QQ_%E7%BE%A4_1-958840932-%230066cc?style=flat-square&logo=TencentQQ)](https://qm.qq.com/q/4NsDQKiAuQ)
[![加入 QQ 群](https://img.shields.io/badge/ClassIsland_QQ_%E7%BE%A4_2-769582982-%230066cc?style=flat-square&logo=TencentQQ)](https://qm.qq.com/q/MhWCvrk7mu)

如果确定遇到的是 **Bug**，或想提出**新功能**，请[提交 Issue](https://github.com/Ansoukin/ClassFabric/issues/new/choose)。

## 开发

各分支的开发状态如下：

| 分支 | 开发状态 | 状态 |
|----|------|----|
| [`master`](https://github.com/Ansoukin/ClassFabric/tree/master) | 发布分支，保存当前发布版本。 | [![Build](https://github.com/Ansoukin/ClassFabric/actions/workflows/build_release.yml/badge.svg?branch=master)](https://github.com/Ansoukin/ClassFabric/actions/workflows/build_release.yml) |
| [`V2.2-Mikhail`](https://github.com/Ansoukin/ClassFabric/tree/V2.2-Mikhail) | 在此分支上开发 2.2 - Mikhail 版本。 | [![Build](https://github.com/Ansoukin/ClassFabric/actions/workflows/build_release.yml/badge.svg?branch=V2.2-Mikhail)](https://github.com/Ansoukin/ClassFabric/actions/workflows/build_release.yml) |

要在本地编译应用，请参考上游文档[配置 ClassIsland 本体开发环境](https://docs.classisland.tech/dev/get-started/development.html)。

如果您想为 ClassFabric 贡献代码，请先阅读[贡献指南](CONTRIBUTING.md)。我们欢迎任何新功能或改进的 [Pull Request](https://github.com/Ansoukin/ClassFabric/pulls)；对上游同样有价值的改动，我们也会择机整理后提交回上游。

项目结构可以参考 [DeepWiki](https://deepwiki.com/Ansoukin/ClassFabric)。 [![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/Ansoukin/ClassFabric)

## 致谢

<!-- ALL-CONTRIBUTORS-BADGE:START - Do not remove or modify this section -->
[![All Contributors](https://img.shields.io/badge/all_contributors-53-orange.svg?style=flat-square)](#contributors-)
<!-- ALL-CONTRIBUTORS-BADGE:END -->

上游项目 ClassIsland 受到 [DuguSand/class_form](https://github.com/DuguSand/class_form) 的启发而开发。

感谢以下同学为 ClassIsland 及本项目的开发提供支持（[✨](https://allcontributors.org/docs/zh-cn/emoji-key)）：

<!-- autocorrect-disable -->
<!-- ALL-CONTRIBUTORS-LIST:START - Do not remove or modify this section -->
<!-- prettier-ignore-start -->
<!-- markdownlint-disable -->
<table>
  <tbody>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/HelloWRC"><img src="https://avatars.githubusercontent.com/u/55006226?v=4?s=100" width="100px;" alt="HelloWRC"/><br /><sub><b>HelloWRC</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=HelloWRC" title="Code">💻</a> <a href="#design-HelloWRC" title="Design">🎨</a> <a href="https://github.com/Ansoukin/ClassFabric/commits?author=HelloWRC" title="Documentation">📖</a> <a href="#ideas-HelloWRC" title="Ideas, Planning, & Feedback">🤔</a> <a href="#maintenance-HelloWRC" title="Maintenance">🚧</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Doctor-yoi"><img src="https://avatars.githubusercontent.com/u/106463935?v=4?s=100" width="100px;" alt="Doctor-yoi"/><br /><sub><b>Doctor-yoi</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=Doctor-yoi" title="Code">💻</a> <a href="#question-Doctor-yoi" title="Answering Questions">💬</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://www.jiangyin14.top/"><img src="https://avatars.githubusercontent.com/u/106649516?v=4?s=100" width="100px;" alt="姜胤"/><br /><sub><b>姜胤</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=jiangyin14" title="Documentation">📖</a> <a href="#ideas-jiangyin14" title="Ideas, Planning, & Feedback">🤔</a> <a href="https://github.com/Ansoukin/ClassFabric/issues?q=author%3Ajiangyin14" title="Bug reports">🐛</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Ansoukin"><img src="https://github.com/Ansoukin.png?size=100" width="100px;" alt="Ansoukin"/><br /><sub><b>Ansoukin</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=Ansoukin" title="Code">💻</a> <a href="#ideas-Ansoukin" title="Ideas, Planning, & Feedback">🤔</a> <a href="#maintenance-Ansoukin" title="Maintenance">🚧</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://fossa.com/"><img src="https://avatars.githubusercontent.com/u/29791463?v=4?s=100" width="100px;" alt="fossabot"/><br /><sub><b>fossabot</b></sub></a><br /><a href="#infra-fossabot" title="Infrastructure (Hosting, Build-Tools, etc)">🚇</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://allcontributors.org/"><img src="https://avatars.githubusercontent.com/u/46410174?v=4?s=100" width="100px;" alt="All Contributors"/><br /><sub><b>All Contributors</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=all-contributors" title="Documentation">📖</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/TV-ZHU"><img src="https://avatars.githubusercontent.com/u/88492699?v=4?s=100" width="100px;" alt="DSZDev"/><br /><sub><b>DSZDev</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=TV-ZHU" title="Documentation">📖</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/LiuYan-xwx"><img src="https://avatars.githubusercontent.com/u/66517348?v=4?s=100" width="100px;" alt="流焰xwx"/><br /><sub><b>流焰xwx</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=LiuYan-xwx" title="Documentation">📖</a> <a href="https://github.com/Ansoukin/ClassFabric/commits?author=LiuYan-xwx" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/TuanZiGit"><img src="https://avatars.githubusercontent.com/u/46892455?v=4?s=100" width="100px;" alt="团子"/><br /><sub><b>团子</b></sub></a><br /><a href="#example-TuanZiGit" title="Examples">💡</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://www.gusui.site/"><img src="https://avatars.githubusercontent.com/u/170245818?v=4?s=100" width="100px;" alt="吕璟辰"/><br /><sub><b>吕璟辰</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=GusuiCommunity" title="Documentation">📖</a> <a href="#promotion-GusuiCommunity" title="Promotion">📣</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/jizilin6732"><img src="https://avatars.githubusercontent.com/u/162853646?v=4?s=100" width="100px;" alt="jizilin6732"/><br /><sub><b>jizilin6732</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=jizilin6732" title="Documentation">📖</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://www.khyan.top/"><img src="https://avatars.githubusercontent.com/u/56215525?v=4?s=100" width="100px;" alt="clover_yan"/><br /><sub><b>clover_yan</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=clover-yan" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/DryIce-cc"><img src="https://avatars.githubusercontent.com/u/165131008?v=4?s=100" width="100px;" alt="干冰DryIce"/><br /><sub><b>干冰DryIce</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=DryIce-cc" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/RoboMico"><img src="https://avatars.githubusercontent.com/u/59791306?v=4?s=100" width="100px;" alt="RoboMico"/><br /><sub><b>RoboMico</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=RoboMico" title="Code">💻</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/user111192"><img src="https://avatars.githubusercontent.com/u/67212831?v=4?s=100" width="100px;" alt="user111192"/><br /><sub><b>user111192</b></sub></a><br /><a href="#example-user111192" title="Examples">💡</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://lyrin-owo.top/"><img src="https://avatars.githubusercontent.com/u/141834038?v=4?s=100" width="100px;" alt="零音LyRin"/><br /><sub><b>零音LyRin</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=LyRin-owo" title="Documentation">📖</a> <a href="#promotion-LyRin-owo" title="Promotion">📣</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://lipoly.ink"><img src="https://avatars.githubusercontent.com/u/110595296?v=4?s=100" width="100px;" alt="LiPolymer"/><br /><sub><b>LiPolymer</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=LiPolymer" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://www.mczhiguang.icoc.me/"><img src="https://avatars.githubusercontent.com/u/53519310?v=4?s=100" width="100px;" alt="Amiya"/><br /><sub><b>Amiya</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=mcAmiya" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://xiaowuap.com"><img src="https://avatars.githubusercontent.com/u/44547885?v=4?s=100" width="100px;" alt="Enzo Wu"/><br /><sub><b>Enzo Wu</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=xiaowuap" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://itshenryz.com"><img src="https://avatars.githubusercontent.com/u/58730749?v=4?s=100" width="100px;" alt="itsHenry"/><br /><sub><b>itsHenry</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=itsHenry35" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://blog.tsxc.xyz"><img src="https://avatars.githubusercontent.com/u/94750616?v=4?s=100" width="100px;" alt="星澜曦光"/><br /><sub><b>星澜曦光</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=tsxc-github" title="Code">💻</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/kaokao221"><img src="https://avatars.githubusercontent.com/u/88539021?v=4?s=100" width="100px;" alt="kaokao221"/><br /><sub><b>kaokao221</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=kaokao221" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/AWDNDWindowsXP"><img src="https://avatars.githubusercontent.com/u/132193112?v=4?s=100" width="100px;" alt="爱玩电脑的WindowsXP"/><br /><sub><b>爱玩电脑的WindowsXP</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=AWDNDWindowsXP" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/FengzihangCode"><img src="https://avatars.githubusercontent.com/u/91676896?v=4?s=100" width="100px;" alt="DannyFeng"/><br /><sub><b>DannyFeng</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=FengzihangCode" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/garyxxxx2021"><img src="https://avatars.githubusercontent.com/u/106256371?v=4?s=100" width="100px;" alt="garyxxxx2021"/><br /><sub><b>garyxxxx2021</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=garyxxxx2021" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/wjj-8283"><img src="https://avatars.githubusercontent.com/u/82750345?v=4?s=100" width="100px;" alt="wjj"/><br /><sub><b>wjj</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=wjj-8283" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/diann34"><img src="https://avatars.githubusercontent.com/u/95152427?v=4?s=100" width="100px;" alt="Nikoa"/><br /><sub><b>Nikoa</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=diann34" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Griseo-bh3rd"><img src="https://avatars.githubusercontent.com/u/121101691?v=4?s=100" width="100px;" alt="【繁星】格蕾修"/><br /><sub><b>【繁星】格蕾修</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=Griseo-bh3rd" title="Code">💻</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/sjzyQwQ"><img src="https://avatars.githubusercontent.com/u/68456645?v=4?s=100" width="100px;" alt="Misaka10072"/><br /><sub><b>Misaka10072</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=sjzyQwQ" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/HuYihe2008"><img src="https://avatars.githubusercontent.com/u/76118866?v=4?s=100" width="100px;" alt="JamYido"/><br /><sub><b>JamYido</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=HuYihe2008" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://cszj.wang/"><img src="https://avatars.githubusercontent.com/u/41245552?v=4?s=100" width="100px;" alt="吕舒君"/><br /><sub><b>吕舒君</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=Lvshujun0918" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/tlnkmc"><img src="https://avatars.githubusercontent.com/u/122972130?v=4?s=100" width="100px;" alt="Mux_Yang"/><br /><sub><b>Mux_Yang</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=tlnkmc" title="Documentation">📖</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://www.pizero.top/"><img src="https://avatars.githubusercontent.com/u/71167373?v=4?s=100" width="100px;" alt="Pizero"/><br /><sub><b>Pizero</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=pizeroLOL" title="Code">💻</a> <a href="#platform-pizeroLOL" title="Packaging/porting to new platform">📦</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/lrsgzs"><img src="https://avatars.githubusercontent.com/u/99574908?v=4?s=100" width="100px;" alt="lrs2187"/><br /><sub><b>lrs2187</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=lrsgzs" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://im.baiyao105.top/"><img src="https://avatars.githubusercontent.com/u/121741105?v=4?s=100" width="100px;" alt="白杳"/><br /><sub><b>白杳</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=baiyao105" title="Code">💻</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Pulsar33550336"><img src="https://avatars.githubusercontent.com/u/226428598?v=4?s=100" width="100px;" alt="Pulsar"/><br /><sub><b>Pulsar</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=Pulsar33550336" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://blog.lao-shui.top/"><img src="https://avatars.githubusercontent.com/u/79132480?v=4?s=100" width="100px;" alt="LaoShui"/><br /><sub><b>LaoShui</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=laoshuikaixue" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/windflag-studio"><img src="https://avatars.githubusercontent.com/u/143715287?v=4?s=100" width="100px;" alt="Ivan"/><br /><sub><b>Ivan</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=windflag-studio" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/denglihong2007"><img src="https://avatars.githubusercontent.com/u/98096191?v=4?s=100" width="100px;" alt="denglihong2007"/><br /><sub><b>denglihong2007</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=denglihong2007" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/unDefFtr"><img src="https://avatars.githubusercontent.com/u/83688818?v=4?s=100" width="100px;" alt="谭麒峰"/><br /><sub><b>谭麒峰</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=unDefFtr" title="Documentation">📖</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/IsHPDuwu"><img src="https://avatars.githubusercontent.com/u/120182813?v=4?s=100" width="100px;" alt="HPD HE"/><br /><sub><b>HPD HE</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=IsHPDuwu" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://yusancky.github.io/"><img src="https://avatars.githubusercontent.com/u/83524927?v=4?s=100" width="100px;" alt="yusancky"/><br /><sub><b>yusancky</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=yusancky" title="Code">💻</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/oldernewbee"><img src="https://avatars.githubusercontent.com/u/209997500?v=4?s=100" width="100px;" alt="oldernewbee"/><br /><sub><b>oldernewbee</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=oldernewbee" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Hongbro886"><img src="https://avatars.githubusercontent.com/u/185684679?v=4?s=100" width="100px;" alt="Hongbro886"/><br /><sub><b>Hongbro886</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=Hongbro886" title="Documentation">📖</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://xfyweb.cn/home"><img src="https://avatars.githubusercontent.com/u/194925706?v=4?s=100" width="100px;" alt="xfy2412"/><br /><sub><b>xfy2412</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=xfy2412" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Chineseshuaji"><img src="https://avatars.githubusercontent.com/u/173371838?v=4?s=100" width="100px;" alt="SyntaxError"/><br /><sub><b>SyntaxError</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=Chineseshuaji" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/OutHimic"><img src="https://avatars.githubusercontent.com/u/136602369?v=4?s=100" width="100px;" alt="OutHimic"/><br /><sub><b>OutHimic</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=OutHimic" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/wan-an-zz"><img src="https://avatars.githubusercontent.com/u/158546327?v=4?s=100" width="100px;" alt="晚安"/><br /><sub><b>晚安</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=wan-an-zz" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Akiyama-Mizuki-44"><img src="https://avatars.githubusercontent.com/u/63501294?v=4?s=100" width="100px;" alt="Lewis Hamilton"/><br /><sub><b>Lewis Hamilton</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=Akiyama-Mizuki-44" title="Code">💻</a></td>
    </tr>
    <tr>
      <td align="center" valign="top" width="14.28%"><a href="http://jbyc.cc"><img src="https://avatars.githubusercontent.com/u/177214309?v=4?s=100" width="100px;" alt="Jbyccc"/><br /><sub><b>Jbyccc</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=Braydenccc" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/Victory-Aogu"><img src="https://avatars.githubusercontent.com/u/294148985?v=4?s=100" width="100px;" alt="TZ^"/><br /><sub><b>TZ^</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=Victory-Aogu" title="Documentation">📖</a></td>
      <td align="center" valign="top" width="14.28%"><a href="http://winddrift.top"><img src="https://avatars.githubusercontent.com/u/115442729?v=4?s=100" width="100px;" alt="浮笙-WindDrift"/><br /><sub><b>浮笙-WindDrift</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=WindDrift" title="Code">💻</a></td>
      <td align="center" valign="top" width="14.28%"><a href="https://github.com/HickoryTrail"><img src="https://avatars.githubusercontent.com/u/80823417?v=4?s=100" width="100px;" alt="HickoryTrail"/><br /><sub><b>HickoryTrail</b></sub></a><br /><a href="https://github.com/Ansoukin/ClassFabric/commits?author=HickoryTrail" title="Code">💻</a></td>
    </tr>
  </tbody>
</table>

<!-- markdownlint-restore -->
<!-- prettier-ignore-end -->

<!-- ALL-CONTRIBUTORS-LIST:END -->
<!-- autocorrect-enable -->
<!--markdownlint-disable MD001 MD033 MD041 MD051-->

本项目使用了[这些第三方库和框架](./doc/Dependencies.md)。

感谢 [JetBrains](https://www.jetbrains.com.cn/) 为本项目的开发人员提供的[开源开发许可证](https://www.jetbrains.com.cn/community/opensource/)。

更详细的致谢信息请在[【应用设置】->【关于 ClassFabric】](classisland://app/settings/about)界面中查看。

## 供应商 / Suppliers

感谢以下供应商对本项目的支持。

<table>
  <tr>
    <td>
      <img alt="qvps.top" src="https://www.qvps.top/assets/logo-mark.svg?v=7d2eedfd" width="50" />
    </td>
    <td>
    由 <a href="https://qvps.top">qvps.top</a> 提供云服务器支持。
    </td>
  </tr>
</table>

## 隐私政策

[隐私政策](./doc/Privacy.md)

## 许可证

本项目中的以下项目基于 GNU Lesser General Public License v3.0 获得许可：

- [ClassIsland.PluginSdk](ClassIsland.PluginSdk)
- [ClassIsland.Core](ClassIsland.Core)
- [ClassIsland.Shared.IPC](ClassIsland.Shared.IPC)
- [ClassIsland.Shared](ClassIsland.Shared)

本项目的其余部分（包括但不限于应用本体）基于 [GNU General Public License v3.0](LICENSE.txt) 获得许可。

## Stars 历史

[![Star 历史](https://starchart.cc/Ansoukin/ClassFabric.svg?variant=adaptive)](https://starchart.cc/Ansoukin/ClassFabric)

<div align="center">

如果这个项目对您有帮助，请点亮 Star ⭐

</div>
