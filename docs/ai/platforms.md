# 平台适配

| 平台 | 规则入口 | 技能发现 | 生成角色 |
|---|---|---|---|
| Codex | `AGENTS.md` | `.agents/skills` | `.codex/agents/*.toml` |
| Claude Code | `CLAUDE.md` | `.claude/skills` | `.claude/agents/*.md` |
| Gemini CLI | `GEMINI.md` | 原生别名 `.agents/skills` | `.gemini/agents/*.md` |

角色来自 `.agents/roles`。Codex 使用 `developer_instructions`；只读角色设为 `sandbox_mode = "read-only"`。Claude 只读角色限制为 `Read, Grep, Glob`；Gemini 只读角色限制为 `read_file, grep_search, glob`。写角色继承调用方权限。Gemini 子代理若需要实验能力开关，由当前平台能力检测说明，不能声称仅存在文件即已启用。

技能 SHA-256 根据原始字节计算；角色摘要使用 `role.json`、零字节及 `instructions.md`。生成正文统一换行，原始换行改变时摘要会变化；相同输入重复同步必须保持内容不变。禁止符号链接和目录联接。

仓库 `.gitattributes` 对共享真源和生成适配固定 LF，避免 Windows 自动换行在重新检出时导致摘要漂移。

平台角色和技能的本地格式检查不能代替实际发现验收。认证、版本、订阅或原生子代理能力不足时记录缺口，受控目标改由独立会话审查；不能启动另一平台的命令行冒充原生子代理。

- [Codex 技能](https://developers.openai.com/codex/skills)
- [Codex 自定义代理](https://developers.openai.com/codex/multi-agent)
- [Codex 项目规则](https://developers.openai.com/codex/guides/agents-md)
- [Claude Code 技能](https://code.claude.com/docs/en/skills)
- [Claude Code 子代理](https://code.claude.com/docs/en/sub-agents)
- [Gemini CLI 技能](https://geminicli.com/docs/cli/skills/)
- [Gemini CLI 子代理](https://geminicli.com/docs/core/subagents/)
- [Gemini CLI 项目上下文](https://geminicli.com/docs/cli/gemini-md/)
