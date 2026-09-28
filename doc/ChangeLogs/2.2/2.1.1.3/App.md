# ClassFabric 2.2 - MikHail（米哈伊尔） Developer Preview 3

这是 ClassFabric 2.2 “MikHail（米哈伊尔）”的第三个开发者预览版本，带来多语言界面、课表编辑器视觉增强与多项上游同步改进。开发者预览版本可能仍存在不稳定或功能不完整的情况，建议在测试环境中使用。

## 🚀 新增功能与优化

- 【设置】新增界面语言设置（简体中文 / 繁體中文 / English），重启应用后生效（[01b5c9c](https://github.com/Ansoukin/ClassFabric/commit/01b5c9c382d42c3575457d455e7c671338c9f63d)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【界面】主窗口菜单、托盘菜单与启动流程文本接入多语言资源，随界面语言切换（[2634c02](https://github.com/Ansoukin/ClassFabric/commit/2634c0257423f9b54a755c7a822ba72f35fe01b3)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【教程】入门教程支持三语并按界面语言加载对应语言版本，同时清理 46 处已失效的贴纸图标表达式（[777b284](https://github.com/Ansoukin/ClassFabric/commit/777b284d502646ac680aed317726cfef2c698631)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【档案】为科目引入主题色、图标和地点字段，课程编辑器展示科目主题色与图标并优化编辑器 UI，科目扩展信息自动迁移（[49a5a86](https://github.com/Ansoukin/ClassFabric/commit/49a5a86eee778e3451501867ae8de147859d4a85)、[191f313](https://github.com/Ansoukin/ClassFabric/commit/191f31385840a2a0c20e9657ef9b6822c94f56eb)、[cf91f31](https://github.com/Ansoukin/ClassFabric/commit/cf91f31a013e068e92c15ea1972450657b564bcc)、[537fdd2](https://github.com/Ansoukin/ClassFabric/commit/537fdd2e13bef0ef507b91292579269ab83a2c7d)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【课表编辑】日程课表视图窄屏幕适配，修复日程课程编辑器触屏不可用的问题（[90866f4](https://github.com/Ansoukin/ClassFabric/commit/90866f40a58fed0305aec216ccc5c6c6a0ca301c)、[ada2555](https://github.com/Ansoukin/ClassFabric/commit/ada2555485d1b4bf60deb5ec5f0cddc4d8efd879)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【主界面（实验性）】同步上游新版主界面：AdaptiveNavigationView 导航框架、竖直版课程控件与主页一日课表。该功能仍在孵化中，暂无正式入口，可通过调试菜单体验（[ae4bb52](https://github.com/Ansoukin/ClassFabric/commit/ae4bb5231512b06453ca22f7e56978489a04cffb)、[4466bf7](https://github.com/Ansoukin/ClassFabric/commit/4466bf777040ca65a10ae1a85c85ea8a2739e831)、[9ed12fc](https://github.com/Ansoukin/ClassFabric/commit/9ed12fcdcb85d76072cd01cf4e11b6ec72bf5f25)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【天气】支持多天气源，请求失败时自动回退到其他天气源（[4b92e3f](https://github.com/Ansoukin/ClassFabric/commit/4b92e3f937b3c1f29c79a9ec45a088993502040b)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【更新】临时目录警告新增红色「我就要运行」按钮，明确风险承担（[b4f309c](https://github.com/Ansoukin/ClassFabric/commit/b4f309c377d87f374afe183da0992fff8c816e70)）by [**@Ansoukin**](https://github.com/Ansoukin)

## 🐛 Bug 修复

- 【语言】修复部分交互路径下界面语言回退到系统语言的问题（随 [01b5c9c](https://github.com/Ansoukin/ClassFabric/commit/01b5c9c382d42c3575457d455e7c671338c9f63d) 修复）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【更新】修复更新通道下拉框为空的问题并改进检查更新反馈；仅在限流时回退本地缓存，其余检查失败不再伪装为已是最新；停止下载时先取消并等待收敛，再清理临时目录（[ad4260d](https://github.com/Ansoukin/ClassFabric/commit/ad4260d16b2cddd15944f6cc57b88bfd08dc1e20)、[515e9b7](https://github.com/Ansoukin/ClassFabric/commit/515e9b7d7eb35941519db2aa15d831de39ace0fe)、[59ed695](https://github.com/Ansoukin/ClassFabric/commit/59ed695e4e2cd223dccf8b9cef0cce6ac228788b)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【编辑器】修复 ProfileRecoveryWindow 的 XAML 命名空间（[ce03ec1](https://github.com/Ansoukin/ClassFabric/commit/ce03ec1fe9ffdfd319408170a4565f4e77d06bc9)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【核心】修复各种流泄露的问题（[877d575](https://github.com/Ansoukin/ClassFabric/commit/877d575b343386ad6abaad27b376645a4cbfe2e0)）by [**@HelloWRC**](https://github.com/HelloWRC)

## 🧹 其它变更

- 【构建】补齐 20 个插件兼容类型转发声明，覆盖上游同步新增的插件可见公开类型（[121627e](https://github.com/Ansoukin/ClassFabric/commit/121627ee311414c5bba095269f18607db4c6504a)、[a12c199](https://github.com/Ansoukin/ClassFabric/commit/a12c199801a298725b884716f0f3326ef23d1896)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【CI】固定 .NET SDK 版本及版本变量（[84cd827](https://github.com/Ansoukin/ClassFabric/commit/84cd8273e5d5b3a2f2749a05fb070118f317f707)）by [**@HelloWRC**](https://github.com/HelloWRC)
