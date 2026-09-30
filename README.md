# ClassFabric.RemoteBroadcast

ClassFabric 远程广播插件（Remote Broadcast）：教师通过手机网页对教室大屏发起 Quick Call（学生 + 目的地 → EdgeTTS 合成呼叫）与语音广播（规划中）。

- 宿主：ClassFabric 2.2（插件 API ≥ 2.0.0.0）
- 形态：外挂插件，白板 = 服务端，局域网直连；不修改 CF 主仓任何源码
- 当前状态：**M0 工程骨架**（任务书见 `docs/`）
- 许可：与 ClassFabric 主仓一致（见 LICENSE）

## 构建

前置：本机已构建 ClassFabric 2.2 Desktop（Debug），或设置环境变量 `ClassFabricDevOutput` 指向宿主输出目录。

```bash
dotnet build src/ClassFabric.RemoteBroadcast/ClassFabric.RemoteBroadcast.csproj -c Debug
```

## 部署到宿主调试环境

将插件输出目录中的内容复制到宿主输出目录的 `Plugins/ClassFabric.RemoteBroadcast/` 下（宿主自身程序集不会复制，`Private` 引用已关闭），启动宿主后在「设置 → 插件」中确认加载。
