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

## 第三方依赖声明

- [QRCoder](https://github.com/codebude/QRCoder)（MIT）：设置页内嵌生成接入码二维码。
- EdgeTTS 合成：插件内自实现的最小 WebSocket 客户端（`Services/EdgeTtsClient.cs`），协议与 `Sec-MS-GEC` 鉴权算法参考公开项目 [rany2/edge-tts](https://github.com/rany2/edge-tts)（仅参考其公开协议文档与算法，未复制其代码）；音频播放复用宿主 `IAudioService`，合成全程内存流、不落盘。

## 部署到宿主调试环境

将插件输出目录中的内容复制到宿主输出目录的 `Plugins/ClassFabric.RemoteBroadcast/` 下（宿主自身程序集不会复制，`Private` 引用已关闭），启动宿主后在「设置 → 插件」中确认加载。
