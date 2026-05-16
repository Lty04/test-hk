# Hollow Knight Seamless Multiplayer (HK-SMP)

一个旨在为《空洞骑士》(Hollow Knight) 提供类似《艾尔登法环》无缝联机体验的模组。支持多人在线合作探索、实时战斗同步、敌人血量共享及位置同步。

> **⚠️ 警告**: 本项目处于早期开发阶段 (Alpha)。请勿在重要存档上使用。联机功能可能会破坏游戏脚本事件或导致崩溃。请务必备份您的存档！

## ✨ 主要特性

- **无缝联机**: 玩家之间可以自由移动，无加载界面阻隔（同场景内）。
- **位置同步**: 实时同步玩家的位置、跳跃、冲刺和方向。
- **战斗同步**: 
  - 同步普通攻击骨刺效果。
  - 同步法术释放（火球、暗影之魂等）。
  - **敌人血量共享**: 一名玩家对敌人造成的伤害会实时同步给所有玩家。
- **P2P 架构**: 采用 LiteNetLib 实现的低延迟 UDP 通信。
- **樱花内网穿透 (Sakura Frp) 支持**: 
  - 推荐使用 [樱花穿透](https://www.natfrp.com/) 进行远程联机，无需公网 IP。
  - 支持 TCP/UDP 隧道转发，专为国内网络优化，延迟低、稳定性高。
  - 配置简单：主机只需在樱花客户端映射本地 UDP 端口（默认 7777），将生成的远程地址发给好友即可。
- **动态插值**: 使用客户端预测和实体插值算法，减少网络抖动带来的卡顿感。

## 🛠️ 技术栈

- **Game**: Hollow Knight (v1.5.x+)
- **Loader**: [BepInEx 5.x](https://github.com/BepInEx/BepInEx)
- **Language**: C# (.NET Framework 4.7.2)
- **Networking**: [LiteNetLib](https://github.com/FailCe/liteNetLib)
- **Serialization**: BinaryWriter/Reader (Custom lightweight protocol)

## 📦 安装指南

### 前置要求

1. 确保你拥有正版的《空洞骑士》游戏。
2. 下载并安装 **BepInEx 5.x (Unity Mono version)**。
   - 将 BepInEx 文件夹解压到游戏根目录（与 `hollow_knight.exe` 同级）。
   - 运行一次游戏以生成配置文件。

### 安装模组

1. 编译本项目的源代码（见下方开发部分），或将发布的 `HollowKnightSeamlessMP.dll` 放入：
   ```text
   <GameRoot>/BepInEx/plugins/
   ```

2. **配置樱花内网穿透 (推荐远程联机)**:
   - 访问 [樱花穿透官网](https://www.natfrp.com/) 注册账号并下载客户端。
   - 登录客户端，点击 **"创建隧道"**。
   - 选择 **UDP** 协议类型（重要：必须选 UDP）。
   - 本地端口填写游戏联机端口（默认 `7777`）。
   - 保存后启动隧道，复制生成的 **远程地址** 和 **远程端口**（例如：`tcp://xxx.xxx.xxx.xxx:yyyy` 中的 IP 和端口）。
   - 将远程 IP 和端口告知好友用于连接。

3. (可选) 如果使用局域网直连，请确保防火墙已开放 UDP 端口 7777。

### 启动游戏

1. 运行 `hollow_knight.exe`。
2. 在主菜单会出现一个新的 **"Multiplayer"** 按钮。
3. **主机 (Host)**: 
   - 点击 "Host Game"。
   - 确保樱花穿透隧道已启动（如果使用远程联机）。
   - 等待好友加入。
4. **客户端 (Client)**: 
   - 点击 "Join Game"。
   - 输入主机提供的 **樱花穿透远程 IP** 和 **远程端口**。
   - 如果是局域网联机，直接输入主机的局域网 IP 即可。

## 🎮 游戏说明

- **同步范围**: 目前仅在同一场景内实现完美同步。当主机切换场景时，所有客户端将被强制传送至新场景的入口处。
- **敌人行为**: 敌人的 AI 仍然由本地计算，但血量和死亡状态由主机权威同步。这意味着敌人攻击你的时机可能因网络延迟略有不同，但血量条是一致的。
- **物品拾取**: 目前版本**未同步**物品拾取（吉欧、护符等），以防存档冲突。建议联机时专注于探索和 Boss 战。

## 💻 开发指南

如果你想为本项目贡献代码，请参考以下架构。

### 项目结构

```text
src/
├── Core/               # 网络核心，连接管理
├── Sync/               # 玩家、敌人、世界状态同步逻辑
├── Hooks/              # 对游戏原生方法的 Harmony/BepInEx 钩子
└── UI/                 # 联机菜单和 NameTag 渲染
```

### 构建步骤

1. 安装 [.NET SDK](https://dotnet.microsoft.com/download) 和 Visual Studio / VS Code。
2. 引用必要的 DLL：
   - `Assembly-CSharp.dll` (来自游戏 `hollow_knight_Data/Managed/`)
   - `UnityEngine*.dll` (来自游戏 `hollow_knight_Data/Managed/`)
   - `BepInEx.dll`
   - `LiteNetLib.dll`
3. 使用 `dotnet build` 编译项目。
4. 将输出的 DLL 复制到 BepInEx 插件目录。

### 关键同步逻辑

#### 1. 玩家移动同步
我们不在网络上发送每一帧的输入，而是发送**状态快照**（位置、速度、动画状态）。客户端收到后使用 `Vector3.Lerp` 进行平滑插值。

```csharp
// 伪代码示例
void ApplyRemoteData(Packet p) {
    targetPos = new Vector3(p.x, p.y);
    // 简单的插值
    transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
}
```

#### 2. 敌人血量同步
通过 Hook `HealthManager.TakeDamage` 拦截伤害事件。
- **主机**: 计算伤害 -> 应用伤害 -> 广播 `{EnemyID, NewHP}`。
- **客户端**: 接收 `{EnemyID, NewHP}` -> 查找本地敌人 -> 直接设置 `hp` 字段 -> 如果 HP<=0 则播放死亡动画。

*注意：需要实现一个 `EnemyRegistry` 来通过场景坐标和名称生成全局唯一的 EnemyID，因为 Unity 的 InstanceID 跨机器无效。*

## ⚠️ 已知问题 (Known Issues)

- **Boss 战脚本**: 部分 Boss 的战利品掉落或过场动画可能在联机时不同步。
- **梦境 Boss**: 进入梦境可能导致客户端脱节。
- **快速旅行**: 使用鹿角虫站快速旅行可能会导致位置不同步。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！
特别是以下领域急需帮助：
- 优化网络序列化以减少带宽。
- 完善法术（Spells）和充能（Charge）的同步逻辑。
- 测试不同场景下的稳定性。

## 📄 许可证

本项目遵循 **MIT License**。
*注意：《空洞骑士》的所有游戏资产版权归 Team Cherry 所有。本模组仅为粉丝创作，非官方产品。*

---

**Made with ❤️ by the Hollow Knight Community**