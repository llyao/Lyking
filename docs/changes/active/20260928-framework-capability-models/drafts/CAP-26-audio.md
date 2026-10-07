# CAP-26 音频 · 设计草稿

> 状态：**待审核** | 能力域：F 客户端核心模块 | 目量工程：合同进 `GameFramework.Contracts`（最小面），实现进 `GameFramework.Adapters.Client.*` | 前置：CAP-14、CAP-24
> 审核方式：逐条回答"审核决策点"；批准后本行改为"已审核（2026-XX-XX）"。

## 1. 解决什么问题

音频的通用需求收敛到一个薄合同：分组混音、播放生命周期、3D 音源关联、设置持久化。没有这层：每个功能自己 `Play()`，音量设置散落、音频资源泄漏（最常被忘记释放的资源类别）、实体销毁后音源还响。

**MMO 驱动力**：
- 同屏大量实体的 3D 音效需要距离裁剪与并发上限（不然百人混战 = 音频地狱）。
- 音量设置是跨会话持久化的玩家偏好（走存档命令，不是音频模块私藏配置）。

**通用性**：单机/联机无差异；回合制只是触发频率低。刻意保持最小合同——音频是表现细节，不是框架重点。

## 2. 术语与核心对象

| 对象 | 含义 |
|---|---|
| `IAudioService` | 播放门面（capability 名 `audio`） |
| `AudioBus` | 混音分组：Music / Sfx / Ambient / Voice / UI，各自音量 |
| `IAudioHandle` | 播放句柄（租约语义，可停可调） |
| 3D 音源 | 挂在表现对象（CAP-24 view）上的空间音频 |

## 3. 合同形状

```csharp
public enum AudioBus { Music, Sfx, Ambient, Voice, Ui }

public interface IAudioService
{
    /// <summary>2D 播放（UI 音、BGM）。返回句柄；忘记释放由 scope 兜底。</summary>
    ValueTask<IAudioHandle> PlayAsync(AudioClipKey clip, AudioPlayOptions options, AssetScope scope, CancellationToken ct);

    /// <summary>3D 播放：音源跟随表现对象（CAP-24 EntityId）。</summary>
    ValueTask<IAudioHandle> PlayAtAsync(AudioClipKey clip, EntityId entity, AudioPlayOptions options, AssetScope scope, CancellationToken ct);

    void SetBusVolume(AudioBus bus, float volume01);   // 立即生效；持久化由设置层发存档命令
    float GetBusVolume(AudioBus bus);
    /// <summary>全局静音（切后台/失焦时宿主调用，CAP-21）。</summary>
    void SetMasterMute(bool muted);
}

public readonly record struct AudioClipKey(AssetKey Asset);
public sealed record AudioPlayOptions
{
    public AudioBus Bus { get; init; } = AudioBus.Sfx;
    public bool Loop { get; init; }
    public float Volume01 { get; init; } = 1f;
    public TimeSpan? FadeIn { get; init; }
    public int Priority { get; init; } = 0;            // 并发上限裁剪依据（低优先级先裁）
}

public interface IAudioHandle : IDisposable
{
    bool IsPlaying { get; }
    void Stop(TimeSpan? fadeOut = null);
}
```

## 4. 关键设计决策（含推荐）

**D1｜句柄 + scope 双释放（推荐）**：句柄 Dispose 停止单个播放；scope 释放兜底回收其全部播放（同 CAP-14 模型）。3D 音源的生命周期默认跟随实体 view（实体归还池 → 音源停）。

**D2｜并发裁剪按优先级（推荐）**：同 Bus 并发超上限（实现配置，Sfx 默认 16）时裁剪低优先级/最旧音源——MMO 混战的自动降载。裁剪是正常路径（计数不报错）。

**D3｜设置持久化走存档命令（推荐）**：音量偏好 = 玩家设置状态，经普通命令进模拟存档（或客户端本地配置文件，Profile 决定）——音频模块不私藏持久化。避免"设置系统"出现第二存储。

**D4｜合同刻意薄（推荐）**：不做音频路由图、不做 DSP 链、不做音乐播放列表——那些是表现层实现细节。合同只保证：分组音量、句柄生命周期、3D 关联三件事全框架一致。

## 5. 不变量

1. 音频资源全部经 `AssetLease` 加载；无裸路径播放。
2. 3D 音源生命周期 ≤ 实体 view 生命周期（CAP-24 归还时自动停）。
3. `SetMasterMute` 与宿主前后台策略联动（后台静音是默认义务）。
4. 音频不阻塞主线程（加载异步；播放控制同步 O(1)）。

## 6. 失败语义

| 场景 | 行为 |
|---|---|
| clip 加载失败 | 句柄失败返回 + 诊断；不阻塞调用方（音效缺失不致命） |
| 设备无输出/独占占用 | 服务整体 Degraded（CAP-19）；游戏继续 |
| 并发超限 | 裁剪（D2），诊断计数 |

## 7. 使用方案

- **U1**：假音频后端记录调用序列（分组/裁剪/生命周期断言）。
- **U2/U3/U4**：无差异（音频与网络无关）；U4 下 World scope 切换自动清空旧 World 音源。

## 8. 与其他能力的关系

- 依赖：CAP-14（资源）、CAP-24（3D 挂点）、CAP-21（失焦静音）。
- 下游：CAP-22（UI 音效触发）、设置存档（D3）。

## 9. 非目标

- 不做语音聊天（VoIP 属独立专项）。
- 不做音频内容工具（脚步声/音乐制作与打包属内容管线产物）。

## 10. 验收证据

- 假后端全行为测试：分组音量、裁剪、句柄/scope/实体三路释放。
- 后台静音联动测试。

## 11. 审核决策点

- **Q1**：D3 音量设置走存档/配置统一路径（不私藏存储）——同意？
- **Q2**：D2 并发裁剪按优先级、裁剪不报错——同意？
- **Q3**：合同保持最薄（不做路由图/DSP）——确认？
