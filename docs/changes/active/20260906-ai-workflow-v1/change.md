<!-- lyking-ai
{"schemaVersion":1,"id":"20260906-ai-workflow-v1","profile":"controlled","status":"active","areas":["Art","Client","Server","Share","Tool"],"paths":["global.json",".gitattributes",".gitignore",".lyking-ai.json",".lyking-ai-adapters.json","AGENTS.md","CLAUDE.md","GEMINI.md","CONTEXT-MAP.md",".agents/",".codex/",".claude/",".gemini/","docs/ai/","docs/changes/","docs/superpowers/plans/2026-09-06-人工智能工作流第一版.md","Art/CONTEXT.md","Client/CONTEXT.md","Server/CONTEXT.md","Share/CONTEXT.md","Tool/CONTEXT.md","Tool/AiWorkflow/"],"riskSignals":["multi-area"]}
-->

# 人工智能工作流第一版

## 解决什么问题

为小团队建立三平台共享工作流和确定性工具门禁。

## 完成后应看到什么

四个命令可运行，六个共享技能与三个角色可发现，平台适配可重复生成。

## 验收方式

运行 .NET 构建、测试、命令验收和适配幂等检查。

## 范围与非目标

只实现第一版设计，不实现看板、租约、控制器、工作树池或外部平台集成。

## 风险与回滚

能力位于文档、代理配置和独立工具项目中，可整体回滚分支。
