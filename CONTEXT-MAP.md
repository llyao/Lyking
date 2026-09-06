# Lyking 上下文路由

| 领域 | 目录 | 上下文 |
|---|---|---|
| Art | `Art/` | `Art/CONTEXT.md` |
| Client | `Client/` | `Client/CONTEXT.md` |
| Server | `Server/` | `Server/CONTEXT.md` |
| Share | `Share/` | `Share/CONTEXT.md` |
| Tool | `Tool/` | `Tool/CONTEXT.md` |

开始任务时先读取本文件和受影响目录的 `CONTEXT.md`，再使用 `route` 确认领域与风险。

## 路由规则

- 显式影响路径确定领域，自由文本只返回候选，不猜测所有权。
- 多领域修改读取全部相关上下文；无法确定时停留根上下文。
- `Share`、存档、协议、安全、至少三个领域使用受控目标。
- `.git`、`.worktrees`、`Library`、`Temp`、`Logs`、`bin`、`obj` 默认排除。
- 稳定规则见 [工作流](docs/ai/workflow.md)、[权威顺序](docs/ai/authority.md)和[验证边界](docs/ai/verification.md)。
