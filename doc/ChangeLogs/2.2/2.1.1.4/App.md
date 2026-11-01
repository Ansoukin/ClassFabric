# ClassFabric 2.2 - MikHail（米哈伊尔） Developer Preview 4

这是 ClassFabric 2.2 “MikHail（米哈伊尔）”的第四个开发者预览版本，包含自上个版本以来的多项功能更新与问题修复。部分功能仍在开发中，尚未完成。开发者预览版本可能存在不稳定或功能不完整的情况，建议在测试环境中使用。

> [!WARNING]
> 强烈建议仍在使用 2.1.1.4 以下版本的用户尽快升级至此版本：此版本包含多项重要修复，并包含工程实名回归（移除插件兼容垫片）等对后续使用有影响的破坏性变更。

> [!IMPORTANT]
> 自本版本起，用户数据目录更名：首次启动时，旧目录（`%APPDATA%\ClassFabric` 与对应的本地数据目录）将自动迁移至标准目录名（`ClassIsland`），配置、档案与插件数据原样搬迁，开机自启快捷方式同步更名；旧版注册的 `classfabric://` 协议在过渡期与新 `classisland://` 协议同时有效。迁移失败时应用将在新目录按首次启动流程运行，旧目录数据原样保留，不会丢失。

## 🚀 新增功能与优化

### 主界面与课表

- 【主界面（实验性）】推进主页日程课表编辑功能：新增主页日程课表的编辑视图与课程、科目编辑子页面（[fd780c4](https://github.com/Ansoukin/ClassFabric/commit/fd780c4b20f75923c1700802dce1df4e2442ed4b)、[f0cfbdc](https://github.com/Ansoukin/ClassFabric/commit/f0cfbdc4ff70520ba0f78eca0801e1393453c8b9)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【档案】日程课表支持调课功能（[8cf88d7](https://github.com/Ansoukin/ClassFabric/commit/8cf88d72f23bfdfd36830fe50e8c43c5621bee5c)）、日程课程支持禁用（[9d05ac4](https://github.com/Ansoukin/ClassFabric/commit/9d05ac47d51fb7a18d5065e34da8cf7a57e43fd4)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【档案】课程支持临时覆盖另一个课程（[fa52477](https://github.com/Ansoukin/ClassFabric/commit/fa52477fef6bf15484997bf76a1f2f603af2ee11)），支持在课程层级覆盖科目设置的上课地点（[0e6a182](https://github.com/Ansoukin/ClassFabric/commit/0e6a182edba007a84700bb56dd18ea68bfd031a7)，上游 [#339](https://github.com/ClassIsland/ClassIsland/issues/339)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【档案】临时课程操作逻辑抽取到 ProfileService 统一管理（[ba51668](https://github.com/Ansoukin/ClassFabric/commit/ba51668d345600acad7a8b9216d840abe70afd72)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【课表编辑】日程课表视图窄屏幕适配（[4f3d0f9](https://github.com/Ansoukin/ClassFabric/commit/4f3d0f9a752e66ac81b343a8f5724c76f744f6c4)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【课表视图】当周课程获取逻辑整合进课表控件（[94373d2](https://github.com/Ansoukin/ClassFabric/commit/94373d2bd867cbf51cc98897d0034ea5da94dad6)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【主界面】日程课表编辑器支持手势缩放（[4adffff2](https://github.com/Ansoukin/ClassFabric/commit/4adffff2f1cdb9d85b437f919e9adb64112ea79b)）by [**@HelloWRC**](https://github.com/HelloWRC)

### 档案与时间规则

- 【档案】允许在时间启用规则中排除和附加特定日期（[30c5858](https://github.com/Ansoukin/ClassFabric/commit/30c5858d20937f0c664da882558ab91e7af2ef70)），并引入日期多选控件（[da84175](https://github.com/Ansoukin/ClassFabric/commit/da84175eae0b5fadd63b9b905be7cbabbbeab806)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【档案】优化档案编辑器在日程模式和经典模式下的行为（[607a4d9](https://github.com/Ansoukin/ClassFabric/commit/607a4d9f24ddc760bd62c91d8f75e8cac2872a43)）by [**@HelloWRC**](https://github.com/HelloWRC)

### 界面与主题

- 【主题】新增 Rectangles 主题（[24422da](https://github.com/Ansoukin/ClassFabric/commit/24422dae8d581156e7990ee9180bfb508d2028b7)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【界面】支持通过滑动消除界面 toast（[519ce2d](https://github.com/Ansoukin/ClassFabric/commit/519ce2d069120598bb84f195433bca96d82cfbaf)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【课表组件】课表组件支持显示科目图标（[8fb47f7](https://github.com/Ansoukin/ClassFabric/commit/8fb47f7b748ee6ce4c67b3d872d642d7fe8ccd29)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【提醒】上课提醒和准备上课提醒显示科目图标（[4fe5954](https://github.com/Ansoukin/ClassFabric/commit/4fe595418cb574fb3094a0f6c37307e80e39aa99)，上游 [#85](https://github.com/ClassIsland/ClassIsland/issues/85)），支持选择显示上课地点、任课教师和持续时间（[417e265](https://github.com/Ansoukin/ClassFabric/commit/417e2658cdae82db2b0095c74aa1a27b82e0e3ca)）；为实时通知设置页面添加示意图（[340beb2](https://github.com/Ansoukin/ClassFabric/commit/340beb24ef0cfbef48c168fe62c0a14c376b170c)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【界面】升级 Avalonia 到 12.1.4（[02ffacb](https://github.com/Ansoukin/ClassFabric/commit/02ffacb175b0c275055a6159a6ed5d6a42b64b61)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【界面】竖直抽屉支持手势控制，并引入全高度模式（[2f2311b7](https://github.com/Ansoukin/ClassFabric/commit/2f2311b735d426ca4667d1c55134cfd6b6897671)、[3351ccf8](https://github.com/Ansoukin/ClassFabric/commit/3351ccf8d78e9f081c6eaa4eb7c74db2c0f6f9bd)）by [**@HelloWRC**](https://github.com/HelloWRC)

### 设置与插件市场

- 【设置】设置窗口新增设置项搜索：输入即搜，命中设置项标题、描述、页面名与页面 ID，点击结果跳转对应设置页并定位到目标控件（[d23aa85](https://github.com/Ansoukin/ClassFabric/commit/d23aa85e04b1527e7f46d8d2f7292019649f71be)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【插件市场】重构插件详情页，支持 README 受控清洗与媒体轮播（[3edb5ae](https://github.com/Ansoukin/ClassFabric/commit/3edb5ae890df7ea11ed787751efe79307f0eaa96)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【设置】高级设置新增 Skia GPU 缓存大小调节，缓解大图显示卡顿（[cc2d1e2](https://github.com/Ansoukin/ClassFabric/commit/cc2d1e2935081693b8f5a8b9fa11402f29a4ea2f)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【设置】通用页新增翻译贡献宣传入口（[5b046ec](https://github.com/Ansoukin/ClassFabric/commit/5b046ec93774379f810ba26e37b90d0a69db1c17)）；关于页版权声明拆分独立条目并新增官网链接（[e610621](https://github.com/Ansoukin/ClassFabric/commit/e6106213a2eda4a6aac654d03a9f027c02baa6d8)）；第三方库列表新增 Markdig（[6f8ad3e](https://github.com/Ansoukin/ClassFabric/commit/6f8ad3e1b48622ad67a8f181c5e3f5a7490f9553)）；鸣谢天气数据源补充 OpenWeather（[85ea1eb](https://github.com/Ansoukin/ClassFabric/commit/85ea1eb975e5365a2eb4194a48295f069a81c369)）by [**@Ansoukin**](https://github.com/Ansoukin)

### 天气

- 【天气】接入和风天气数据源，支持 API 用量限制（[25d3280](https://github.com/Ansoukin/ClassFabric/commit/25d3280c3c61f7709d78fbefd8ad481791d6d12f)）by [**@Ansoukin**](https://github.com/Ansoukin)

### 本地化（进行中）

- 【本地化】接入 Crowdin 本地化平台，逐步回流简中、繁中与英文翻译资源，档案编辑器、组件显示、换课窗口、数据迁移页、教程编辑器与更新、档案转换、插件服务等界面已接入多语言（[29845a7](https://github.com/Ansoukin/ClassFabric/commit/29845a74468f721b7258e6126760ef630f2b116e)、[5076afa](https://github.com/Ansoukin/ClassFabric/commit/5076afa17ae9a44ef0af4b53cbb32f83c82871ad)、[93444cd](https://github.com/Ansoukin/ClassFabric/commit/93444cdb93399737354666b581849e42ae9b6137)、[f196d98](https://github.com/Ansoukin/ClassFabric/commit/f196d985b46787601710e79a5753a65647912436)、[5a749a7](https://github.com/Ansoukin/ClassFabric/commit/5a749a733b0654850682a10bae0a6f1b020849e0)、[824d16d](https://github.com/Ansoukin/ClassFabric/commit/824d16d3bd7bb6ce9b7115959eaee5e0ddc3fbf5)、[f95956a](https://github.com/Ansoukin/ClassFabric/commit/f95956a6f4b39aa7956e56f4719e3941e3bbf34e)、[24c3629](https://github.com/Ansoukin/ClassFabric/commit/24c3629f48e434b2dfbc182c5a454abb81a3f3ad)）by [**@Ansoukin**](https://github.com/Ansoukin)

## 🐛 Bug 修复

- 【自动化】修复时间偏移调整后 cron 触发器执行两次的问题（[9b69a82](https://github.com/Ansoukin/ClassFabric/commit/9b69a82d56f0b7f58a6272d90ede6d7a1d2a88bc)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【界面】修复关闭设置窗口时的空引用异常（[983a4dd](https://github.com/Ansoukin/ClassFabric/commit/983a4dd1bd1867d6b6995a22d0843b2cbcc8da9a)）；修复调试开关切换后右窗格空白的问题（[f9ee229](https://github.com/Ansoukin/ClassFabric/commit/f9ee2296307efe6e6e396f2787e387a42572161d)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【界面】修复视图内存泄露的问题（[dff09a7](https://github.com/Ansoukin/ClassFabric/commit/dff09a76e7bda6794dfaef81e60367ad9bdf50fc)）；修复特定情况下的页面样式应用问题（[39055f7](https://github.com/Ansoukin/ClassFabric/commit/39055f708cff17b82fc8e70f39b730e666b2161b)）by [**@HelloWRC**](https://github.com/HelloWRC)
- 【迎新】修复特定区域配置下 OOBE 开屏动画闪退的问题（[fdf3b4c](https://github.com/Ansoukin/ClassFabric/commit/fdf3b4c9cc0aa666a479cdd862b0d2a778f620f2)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【界面】优化托盘菜单与弹层在 Avalonia 12 下的半透明渲染（[3f2c8dc](https://github.com/Ansoukin/ClassFabric/commit/3f2c8dc435d4b37b5e01b307789e4ec6930fb500)）；修复 Rectangles 主题在展示天气提醒时崩溃的问题（[247c295](https://github.com/Ansoukin/ClassFabric/commit/247c295572d04b9fee9ddae2cc0b7730555e6a5b)）by [**@Ansoukin**](https://github.com/Ansoukin)、[**@HelloWRC**](https://github.com/HelloWRC)
- 【组件】修复主题切换后天气简报数值消失的问题（[b19d0d1](https://github.com/Ansoukin/ClassFabric/commit/b19d0d151b861d49492370775bc7fcce1ba7cabf)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【天气】天气源返回无效数据时保留上次数据（[aeccc65](https://github.com/Ansoukin/ClassFabric/commit/aeccc65abe7de00a31b56be9d67df9950beb0c4d)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【档案】修复上游同步重放引入的多余科目字段标签，并补齐启动屏位图释放（[2af73b2](https://github.com/Ansoukin/ClassFabric/commit/2af73b2014e12972ecd70010e428445fd0d103bc)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【插件市场】修复详情页 SVG 图片无法加载的问题（[cc857ba](https://github.com/Ansoukin/ClassFabric/commit/cc857ba197e99a45d09523fddab5fa8f1a8d29b7)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【构建】修复 Debug 构建启动时的两处崩溃（[f241487](https://github.com/Ansoukin/ClassFabric/commit/f241487d654c94786421a6cdbd110ab36b31d34a)）by [**@Ansoukin**](https://github.com/Ansoukin)

## 🧹 其它变更

- 【工程】移除插件兼容垫片并将工程实名回归 ClassIsland，上游市场插件免垫片直接加载；用户可见品牌保留 ClassFabric（[94e34b5](https://github.com/Ansoukin/ClassFabric/commit/94e34b54b4b8701fc04788359c85ffc1c050f17d)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【工程】引入 blob 资产系统并重构覆盖层资源加载（[4f9d228](https://github.com/Ansoukin/ClassFabric/commit/4f9d22809e32db19677010223a0a53be10e28133)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【界面】永久跳过教学引导并移除倒卖警告横幅（[f053d99](https://github.com/Ansoukin/ClassFabric/commit/f053d992c5af29cc9113354f2a31cc21a3f18dfb)）；移除开发者预览提示弹窗，启动不再需要手动关闭提示（[32216b2](https://github.com/Ansoukin/ClassFabric/commit/32216b27dd26aae4240f90689684ebb1d153804d)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【迎新】OOBE 开屏动画文本更名为 ClassFabric，关于页许可声明并入许可信息条目（[6ba002e](https://github.com/Ansoukin/ClassFabric/commit/6ba002e36db84e4c1163570620e2113f8f171b8d)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【构建】补齐 16 个插件兼容类型转发声明（[a1e96a2](https://github.com/Ansoukin/ClassFabric/commit/a1e96a20542755866e5b0de1cf3bd97c9f4fbb88)、[e482bb4](https://github.com/Ansoukin/ClassFabric/commit/e482bb41c6743b2f16cabb5bd0dc2cf7bd02f903)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【CI】固定 .NET SDK 版本（[769c8a6](https://github.com/Ansoukin/ClassFabric/commit/769c8a6f4dd6e73a2a2f02c88cb898349b724465)）by [**@HelloWRC**](https://github.com/HelloWRC)
