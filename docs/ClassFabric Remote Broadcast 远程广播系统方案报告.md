# ClassFabric Remote Broadcast
## 手机远程广播与快速呼叫系统方案报告

**项目属性：** ClassFabric 外围插件 / 独立配套服务  
**首个目标版本：** 国庆期间生产 MVP  
**目标周期：** 7 天  
**核心场景：** 教师使用 iOS / Android 手机，对指定教室内运行 ClassFabric 的大屏进行远程语音广播和快速呼叫

---

## 一、项目背景

学校现有需求是：教师希望能够通过手机，对班级内连接大屏的电脑和音响进行远程喊话，例如通知学生到办公室、集合、前往某处等。

目前有人提出使用第三方“南山对讲”等软件实现这一需求。该方案可以满足基本的语音通信需求，但会产生独立客户端、第三方账号/频道、设备管理、软件安装、后续维护以及与 ClassFabric 现有班级、学生和教室模型脱节等问题。

ClassFabric 因此计划提供一个独立的 **Remote Broadcast（远程广播）** 配套能力：

> 手机负责控制和发起广播，ClassFabric 负责教室端接收、播放和界面联动；网络通信、手机网页等能力作为独立外围系统存在，通过 CF 插件接入。

该方案不是将 ClassFabric 改造为即时通信软件，而是为 ClassFabric 增加面向教室大屏的远程广播能力。

---

# 二、总体产品定位

项目不定义为传统“网络对讲机”，而定义为：

> **ClassFabric Remote Broadcast —— 面向教室大屏的远程语音广播与快速呼叫系统。**

首期主要包括两种能力：

### 1. 语音广播

教师在手机上按住麦克风按钮讲话：

```text
教师手机
    ↓
Push-to-Talk
    ↓
WebRTC
    ↓
ClassFabric
    ↓
教室音响
```

### 2. 快速呼叫

教师选择学生并选择呼叫目标，ClassFabric 自动生成语句并使用已有的 EdgeTTS 能力合成：

```text
小陈
+
办公室
+
刘老师

↓

“请学生小陈到办公室找刘老师。”
```

多人选择时：

```text
小王 + 小张

↓

“请学生小王、小张到办公室找刘老师。”
```

快速呼叫是本项目区别于普通网络对讲软件的重要能力，因为 ClassFabric 本身已经拥有班级、学生等上下文数据。

---

# 三、核心设计原则

## 原则 1：Remote Broadcast 是外挂能力，不进入 CF Core

Remote Broadcast 不直接侵入 ClassFabric 核心，实现：

```text
ClassFabric
 ├── Core
 ├── UI
 ├── Automation
 ├── TTS
 └── RemoteBroadcast Plugin
```

其中 RemoteBroadcast Plugin 只是适配层。

WebRTC、信令服务器、手机 Web/PWA 等全部保持在外围系统中。

这样可以避免 ClassFabric Core 被实时音频通信技术污染。

---

## 原则 2：CF 插件负责“集成”，不负责“重新发明 WebRTC”

建议复用成熟的开源 WebRTC/PTT 项目作为底层通信参考或技术基础，优先选择许可证明确允许修改和再发布的项目。

开发重点放在：

- CF 集成
- 教室身份
- 教师身份
- 广播状态
- 大屏 UI
- 快速呼叫
- 稳定性
- 自动化联动

而不是在七天内从零实现完整 WebRTC 栈。

---

## 原则 3：手机端不开发原生 iOS / Android App

首期采用：

> **响应式 Web + PWA**

教师第一次访问网页后，可以直接添加到手机主屏幕。

iPhone 当前 Safari 支持将网站添加到主屏幕，并可以开启“作为网页 App 打开”；添加后的图标可以像 App 一样直接启动。

因此实际使用流程可以做到：

```text
首次：
Safari
 ↓
打开广播页面
 ↓
登记喊话人
 ↓
添加到主屏幕

日常：
手机桌面
 ↓
ClassFabric 广播
 ↓
按麦克风
 ↓
讲话
```

不要求教师每次打开 Safari 后重新输入网址，更不要求安装独立 App。

---

# 四、教室选择模型

## 决策：不做每次选择教室

教师日常使用时不需要出现：

```text
请选择教室：
301
302
303
304
```

教室身份由**固定入口 URL**决定。

例如：

```text
https://broadcast.example.com/r/302/<token>
```

这个链接本身代表：

> 302 教室

教师保存该网页后，以后直接打开即可。

因此：

```text
手机
 ↓
固定网页
 ↓
固定 Room Token
 ↓
302 教室
```

教师无需知道具体 Room ID，也无需重新扫码。

---

# 五、首次绑定与日常使用

## 首次配置

可以由电教人员或管理员为每间教室生成唯一广播入口：

```text
302 教室
https://broadcast.example.com/r/302/xxxxxx
```

教师第一次打开后：

```text
ClassFabric Remote Broadcast

教室：
302

喊话人：
[ 刘老师 ]

☑ 在此设备记住我

[开始使用]
```

完成后，将网页加入手机主屏幕。

---

## 日常使用

```text
手机桌面
   ↓
📢 ClassFabric
   ↓
自动进入该教室
   ↓
显示“刘老师”
   ↓
[ 🎙 按住讲话 ]
```

不要求：

- 再次扫码
- 再次选择教室
- 再次输入教师姓名
- 下载第三方 App

---

# 六、教师身份持久化

教师端采用 Cookie 保存喊话人信息。

建议至少保存：

```text
teacherName
deviceToken
```

其中：

- `teacherName` 用于显示和生成播报文案
- `deviceToken` 用于识别已经登记的设备

不要只保存姓名。

例如：

```text
teacherName = 刘老师
deviceToken = 随机 Token
```

这样教师修改显示名称不会直接改变设备身份。

Room 身份与教师身份分离：

```text
Room Token
    ↓
决定“喊哪个教室”

Device Token
    ↓
决定“谁在喊”
```

---

# 七、关于 127.0.0.1:5212

`127.0.0.1:5212` 可以保留，但**不能作为教师手机访问地址**。

因为手机访问 `127.0.0.1` 时访问的是手机自身，而不是教室计算机。

推荐将其定义为 ClassFabric 本机服务接口：

```text
ClassFabric
   ↓
RemoteBroadcast Plugin
   ↓
localhost:5212
   ↓
Local Receiver / Companion Service
```

用途可以包括：

- CF 与本地广播客户端通信
- 查询连接状态
- 控制广播接收器
- 获取播放状态
- 本地进程健康检查

教师手机访问的则是：

```text
https://broadcast.example.com/...
```

即：

```text
127.0.0.1:5212 = 本机内部接口
HTTPS URL       = 手机访问入口
```

二者职责明确分离。

---

# 八、系统总体架构

推荐采用三层结构：

```text
                    ┌──────────────────┐
                    │   Mobile PWA     │
                    │ iOS / Android    │
                    └────────┬─────────┘
                             │
                      HTTPS / WebSocket
                             │
                             ▼
              ┌──────────────────────────┐
              │ Remote Broadcast Server │
              │                          │
              │ Room / Device / Signal  │
              │ Authentication          │
              └─────────────┬────────────┘
                            │
                       WebRTC / Signal
                            │
                            ▼
              ┌──────────────────────────┐
              │ ClassFabric 2.2 Plugin  │
              │                          │
              │ RemoteBroadcast Plugin  │
              └─────────────┬────────────┘
                            │
                  Local Receiver / Audio
                            │
                            ▼
                       教室音响
```

---

# 九、CF 插件职责

建议插件名称暂定：

```text
ClassFabric.RemoteBroadcast
```

插件主要负责：

1. 连接 Remote Broadcast 服务
2. 保存本地教室绑定状态
3. 接收远程广播状态
4. 接收/播放实时语音
5. 调用 CF 已有 EdgeTTS
6. 向 CF UI 提供状态
7. 提供通知/自动化联动能力
8. 管理本地 Receiver 生命周期

---

# 十、CF 内部服务模型

插件应向 CF 提供独立服务，例如：

```csharp
IRemoteBroadcastService
```

核心状态建议：

```text
Disconnected
Connecting
Connected
Preparing
Broadcasting
Ending
Error
```

并提供：

```text
RoomId
BroadcasterName
State
StateChanged
```

这样 CF UI、通知、自动化等其他模块不需要直接依赖 WebRTC 实现细节。

---

# 十一、CF 大屏联动

Remote Broadcast 的一个核心价值是：

> **广播发生时，ClassFabric 本身知道发生了什么。**

例如教师开始讲话后，CF 主窗口出现 Overlay：

```text
┌──────────────────────────────┐
│                              │
│       📢 刘老师正在讲话       │
│                              │
└──────────────────────────────┘
```

建立连接过程中：

```text
刘老师正在准备广播……
```

结束后：

```text
Overlay 消失
 ↓
CF 恢复正常课表界面
```

因此广播不是一个完全独立于 CF 的黑盒程序，而是 CF 当前状态的一部分。

---

# 十二、状态与自动化联动

未来插件可以提供 Trigger，例如：

```text
远程广播开始
远程广播结束
远程广播连接
远程广播断开
远程广播异常
```

以及 Action：

```text
开始远程广播
结束远程广播
显示远程广播状态
```

首个七天 MVP 不必实现完整自动化联动，但插件 API 应预留这一能力。

---

# 十三、快速呼叫（Quick Call）

## 产品目标

解决最常见的校园场景：

> 老师需要叫一个或几个学生去办公室，但不希望自己录制/说完整一句话。

教师手机打开 Quick Call：

```text
选择学生

☑ 小陈
☐ 小王
☐ 小张

目的地：
办公室

呼叫人：
刘老师

[📢 立即呼叫]
```

---

## 数据流

```text
手机
 ↓
发送结构化 Quick Call 事件

{
    students: ["小陈"],
    destination: "办公室",
    caller: "刘老师"
}

 ↓
CF RemoteBroadcast Plugin
 ↓
QuickCallTextGenerator
 ↓
CF EdgeTTS
 ↓
本地播放
```

手机不需要自己进行 TTS。

---

# 十四、EdgeTTS 应直接复用 ClassFabric 已有能力

ClassFabric 已经支持 EdgeTTS，因此 Remote Broadcast 不应重复实现 TTS。

正确结构：

```text
Quick Call
   ↓
生成语义参数
   ↓
本地化文本模板
   ↓
ClassFabric EdgeTTS
   ↓
Audio Output
```

例如：

```text
学生：小陈
地点：办公室
喊话人：刘老师
```

生成：

> 请学生小陈到办公室找刘老师。

多人：

> 请学生小王、小张到办公室找刘老师。

---

# 十五、快速呼叫不要硬编码完整中文句子

建议使用模板：

```text
请学生{Students}到{Destination}找{Caller}。
```

以后能够扩展不同表达：

```text
请{Students}到{Destination}。
```

或者：

```text
请学生{Students}立即前往{Destination}，找{Caller}。
```

同时为国际化准备参数化结构：

```text
Students
Destination
Caller
```

而不是在程序代码中硬编码中文字符串。

---

# 十六、多人快速呼叫

首个 MVP 可以支持多人选择。

例如：

```text
小王
小张
小李
```

生成：

```text
请学生小王、小张、小李到办公室找刘老师。
```

如果需要更自然的中文表达，可以后续增加专用文本生成器，负责：

- 中文顿号
- 最后一个“和”
- 过多姓名时的语言优化
- 不同语言的连接词

---

# 十七、广播队列

推荐后续支持：

```text
Quick Call Queue

① 小陈 → 办公室
② 小王、小张 → 办公室
③ 小李 → 实验室
```

按顺序：

```text
TTS 1
 ↓
播放完成
 ↓
TTS 2
 ↓
播放完成
 ↓
TTS 3
```

避免多条语音互相覆盖。

首个七天 MVP 可以只保留一个正在执行的呼叫，队列作为 P1 功能。

---

# 十八、实时 PTT 与 Quick Call 的关系

最终模块：

```text
ClassFabric Remote Broadcast
│
├── 🎙 Voice Broadcast
│
└── ⚡ Quick Call
      │
      └── EdgeTTS
```

二者共用：

```text
Room
Device Token
Teacher
Connection
CF Overlay
Audio Output
```

但实现方式不同：

### Voice Broadcast

```text
手机
 ↓
实时语音
 ↓
WebRTC
 ↓
CF
```

### Quick Call

```text
手机
 ↓
结构化事件
 ↓
CF
 ↓
EdgeTTS
 ↓
CF 音频输出
```

---

# 十九、网络通信技术建议

## 实时语音

优先采用：

```text
WebRTC + PTT
```

第一阶段只做：

```text
One Phone
   ↓
One Classroom
```

不做：

```text
多人会议
双向通话
视频
群聊
```

---

## 信令

服务器负责：

```text
房间发现
设备认证
WebRTC Signaling
广播锁定
连接状态
```

正常语音流尽可能采用 WebRTC P2P。

必要时提供：

```text
STUN
TURN
```

以提高不同网络环境下的可用性。

---

# 二十、广播锁

同一个教室第一版建议：

> 同一时间只允许一个教师发言。

例如：

```text
302 教室

刘老师    🟢 正在广播
王老师    🔴 请稍候
```

服务器进行 Room Lock。

广播结束释放锁。

这样可以避免两个教师同时讲话导致声音混杂。

---

# 二十一、安全设计

第一版至少需要：

### 1. Room Token

不能把：

```text
302
```

本身当作凭证。

应采用：

```text
Room ID
+
随机 Token
```

例如：

```text
/r/302/long-random-token
```

### 2. Device Token

设备首次登记后获得随机 Device Token。

### 3. HTTPS

手机端麦克风需要浏览器安全上下文，因此生产环境使用 HTTPS。

### 4. 不保存语音

默认不保存：

```text
录音文件
历史音频
```

Quick Call 也不需要保存音频历史。

---

# 二十二、隐私设计

Remote Broadcast 应坚持最小化数据收集原则。

服务器原则上只需要：

```text
Room ID
Device Token
Teacher Display Name
Online State
Connection Metadata
```

不需要收集：

```text
个人文件
浏览记录
聊天内容
音频历史
不必要的身份信息
```

教师姓名是广播显示所需数据，可以由教师自行设置。

---

# 二十三、服务器方案

BlueServers 公益服务器目前尚未获得，不能将其作为首版生产环境的前置条件。

因此：

```text
临时 VPS
     ↓
Remote Broadcast MVP
     ↓
BlueServers 获批
     ↓
迁移正式基础设施
```

系统应从第一天开始做到：

> Server 与客户端/CF 插件解耦。

这样以后迁移只需要修改服务端地址，而不需要重新设计客户端。

---

# 二十四、BlueServers 的正式用途

获得公益服务器以后，可以统一承载：

```text
ClassFabric Infrastructure

├── 官方网站
├── Docs
├── Releases / Distribution
├── Forum
├── APIs
├── Remote Broadcast
├── Signaling
└── TURN
```

Remote Broadcast 只是 CF 官方基础设施中的一个服务，不与 GitHub 形成替代关系。

GitHub 依旧是：

```text
Source Code
Issues
Discussions
Releases
Development History
```

而自有服务器负责用户侧服务。

---

# 二十五、七天生产 MVP 排期

## Day 1：通信原型

目标：

```text
手机浏览器
 ↓
PTT
 ↓
另一个浏览器
 ↓
听到语音
```

优先验证：

- HTTPS
- WebSocket
- WebRTC
- 麦克风权限
- 音频播放

---

## Day 2：CF 接收端

建立：

```text
ClassFabric.RemoteBroadcast
```

实现：

```text
Connect
Disconnect
Start
Stop
State
RoomId
```

先完成 CF 与外围 Client 的通信。

---

## Day 3：固定入口与身份持久化

实现：

```text
Room URL
Device Token
Teacher Name
Cookie
```

验证：

```text
第一次登记
 ↓
关闭网页
 ↓
重新打开
 ↓
身份仍然存在
```

同时验证 iPhone 主屏 Web App 使用流程。Apple 当前官方流程支持将网站添加到主屏幕并作为 Web App 打开。

---

## Day 4：网络稳定性

重点测试：

```text
Wi-Fi → Wi-Fi
5G → Wi-Fi
Wi-Fi → 5G
弱网
断网
重连
服务器重启
CF 重启
手机页面重新进入
```

必要时部署：

```text
STUN + TURN
```

---

## Day 5：CF UI 联动 + Quick Call

完成：

```text
“刘老师正在准备广播……”
“📢 刘老师正在讲话”
```

然后实现 Quick Call：

```text
学生选择
 ↓
模板生成
 ↓
CF EdgeTTS
 ↓
播放
```

---

## Day 6：真机测试

测试设备：

```text
iPhone
Android
CF Windows
不同 Wi-Fi
5G
```

重点测试：

```text
按住
松开
再次按
连续广播
TTS
重连
错误 Token
```

---

## Day 7：封版

完成：

```text
生产部署
默认配置
安装说明
手机使用说明
CF 插件打包
版本发布
```

建议首版使用：

```text
ClassFabric Remote Broadcast 0.1.0
```

而不是把完整能力直接绑定为 CF 2.2 正式核心功能。

---

# 二十六、七天内明确延期的功能

以下全部暂缓：

```text
❌ 双向对讲
❌ 多人语音会议
❌ 视频
❌ 聊天
❌ 录音历史
❌ 联系人系统
❌ 原生 iOS App
❌ 原生 Android App
❌ 复杂账号体系
❌ 年级/全校广播
❌ 多教室同时广播
❌ AI 语音识别
❌ TTS 自定义脚本编辑器
❌ 完整管理后台
```

这些功能不是不存在价值，而是不属于当前七天交付范围。

---

# 二十七、第一版验收标准

MVP 不以“功能很多”为验收标准，而以一条完整业务链路为标准：

```text
教师第一次打开网页
        ↓
登记“刘老师”
        ↓
保存网页到 iPhone 主屏幕
        ↓
以后直接点图标
        ↓
自动进入指定教室
        ↓
按住麦克风
        ↓
CF 大屏显示
“刘老师正在讲话”
        ↓
教室音响实时播放
        ↓
松开麦克风
        ↓
广播结束
        ↓
CF 恢复正常界面
```

以及：

```text
教师
 ↓
点击“小陈”
 ↓
点击“办公室”
 ↓
快速呼叫
 ↓
CF EdgeTTS
 ↓
“请学生小陈到办公室找刘老师。”
 ↓
教室音响播放
```

完成这两条链路，即可认为：

> **ClassFabric Remote Broadcast MVP 已具备生产使用价值。**

---

# 二十八、最终架构决策

本方案最终采用：

```text
                 ClassFabric Remote Broadcast

                         ┌─────────────┐
                         │ Mobile PWA  │
                         └──────┬──────┘
                                │
                       HTTPS / WebSocket
                                │
                                ▼
                    ┌────────────────────┐
                    │ Broadcast Server   │
                    │                    │
                    │ Room / Auth /      │
                    │ Signaling / TURN   │
                    └─────────┬──────────┘
                              │
                         WebRTC
                              │
                              ▼
                   ┌─────────────────────┐
                   │ CF RemoteBroadcast  │
                   │       Plugin        │
                   └───────┬─────────────┘
                           │
              ┌────────────┴─────────────┐
              │                          │
         Voice Broadcast             Quick Call
              │                          │
           WebRTC                     CF EdgeTTS
              │                          │
              └────────────┬─────────────┘
                           │
                      CF Audio Output
                           │
                           ▼
                        教室音响
```

其中：

```text
127.0.0.1:5212
```

定位为：

> **CF 本机 Remote Broadcast Client / Receiver 的内部接口**

而不是手机访问地址。

手机端定位为：

> **固定教室 URL + Cookie 身份 + PWA 主屏入口**

教室选择定位为：

> **不提供日常选择，由 URL 固化教室身份**

TTS 定位为：

> **直接复用 ClassFabric 已有 EdgeTTS**

CF 定位为：

> **插件提供服务、UI 联动和音频终端，而不是承担通信基础设施实现**

---

# 二十九、项目状态建议

按照 ClassFabric 现有项目管理方式，这个项目目前建议记录为：

**状态：建议立项 / CF 2.2 外围项目**

### 已确认

- 外挂形式
- CF 插件接入
- 手机 Web/PWA
- 固定教室入口
- 不需要每次选择教室
- Cookie 保存教师身份
- CF 内 Overlay / 状态联动
- 使用已有 EdgeTTS
- 单教室单发言人
- Quick Call
- 首期不做双向教室→教师通信

### 建议采用

- WebRTC 作为实时语音底层
- 成熟开源 PTT 项目作为参考/通信底座
- 独立 Remote Broadcast Server
- STUN/TURN
- `127.0.0.1:5212` 作为本地接口
- PWA 而非原生 App

### 暂缓

- 双向对讲
- 多人会议
- 全校广播
- 原生移动 App
- 复杂用户管理
- 录音与历史
- 高级自动化

---

# 三十、结论

ClassFabric Remote Broadcast 最终不应成为一个“南山对讲替代品”的简单复制项目。

它的核心价值在于：

> **把手机远程广播能力与 ClassFabric 已有的教室、学生、教师、课表、TTS、UI 和插件生态连接起来。**

其最小且完整的产品模型是：

```text
手机 = 遥控器
服务器 = 通信基础设施
CF 插件 = 集成层
CF = 教室广播终端
EdgeTTS = 快速呼叫语音引擎
```

七天内最应该完成的不是“大而全的网络对讲系统”，而是一个老师真正愿意长期使用的闭环：

> **点手机桌面图标 → 按住麦克风喊话；或者点学生 → 一键 TTS 快速呼叫；CF 大屏即时反馈并通过教室音响播放。**

这条链路一旦跑通，Remote Broadcast 就已经成为 ClassFabric 一个具有明确产品价值的外围能力，而后续的文字广播、TTS 模板、自动化、年级/全校广播等功能都可以在不污染 CF Core 的情况下继续演进。