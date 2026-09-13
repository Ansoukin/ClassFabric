# ClassFabric 2.2 - MikHail（米哈伊尔） Developer Preview 2

这是 ClassFabric 2.2 “MikHail（米哈伊尔）”的第二个开发者预览版本，主要用于验证 GitHub Releases 自动更新链路的端到端可用性。本版本为临时验证版本，验证完成后可能被移除或替换。开发者预览版本可能仍存在不稳定或功能不完整的情况，建议在测试环境中使用。

## 🚀 新增功能与优化

- 【更新】新增 GitHub Releases 更新源，可直接从 ClassFabric 仓库获取发行版本，并支持在「正式版 / 预览版」更新通道之间切换（[b3aee8f](https://github.com/Ansoukin/ClassFabric/commit/b3aee8fd5503c525c897424a10991b3a2fed5a0d)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【插件】插件加载失败时给出可读的诊断结论，并提供「复制诊断信息」以获取完整技术详情（[4ab0b6b](https://github.com/Ansoukin/ClassFabric/commit/4ab0b6beb8444a9f3ea17c1dc9a4c6dfd46826a7)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【API/自动化】为动作与触发器新增统一分类元数据，供后续选择器按分类展示（[8b89949](https://github.com/Ansoukin/ClassFabric/commit/8b899493d2f76472fe78a48707046cdb9026d185)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【应用设置/关于】完善版权年份、仓库链接与鸣谢信息（[112a5a7](https://github.com/Ansoukin/ClassFabric/commit/112a5a73b520a36e75e0e3f707d010502ca06526)）by [**@Ansoukin**](https://github.com/Ansoukin)
- 【应用】移除内置贴纸资源及其全部引用（[5955a48](https://github.com/Ansoukin/ClassFabric/commit/5955a481a5c01e5c4a2f3d1a467fdad856e81295)）by [**@Ansoukin**](https://github.com/Ansoukin)

## 🐛 Bug 修复

- 【应用】修复资源路径大小写不一致导致教程加载崩溃的问题（[ffcff88](https://github.com/Ansoukin/ClassFabric/commit/ffcff886bc0fea6ac4e4b75acbb69a4d9dea9fe5)）by [**@Ansoukin**](https://github.com/Ansoukin)

## 🧹 其它变更

- 【构建】发布流程不再推送 PDCC 与 NuGet 制品（[26a9289](https://github.com/Ansoukin/ClassFabric/commit/26a9289483c81bc3a81a4da4922f9feddca32e09)）by [**@Ansoukin**](https://github.com/Ansoukin)
