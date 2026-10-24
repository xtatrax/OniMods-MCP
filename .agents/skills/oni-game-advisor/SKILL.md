---
name: oni-mcp-game-advisor
description: 当用户询问 ONI 游戏机制、建筑/资源/元素/动植物用途、策略建议，或在未授权控制游戏时问“我该做什么”时使用。事实性答案优先依据可验证的外部文档或静态仓库资料；仅当建议依赖当前存档时读取只读殖民地、世界和资源状态，可通过现有只读查询读取已加载的游戏内 Codex 缓存；旧 guide 域仍为 disabled。
---

# ONI MCP 游戏顾问

## 按需的阶段策略参考

只有问题涉及阶段规划、基地升级或模块选型时，才读取[日文策略索引](../../../docs/colony-strategy/index_JP.md)，再选择相关主题参考。这不增加游戏写入权限，也不替代现有控制技能。`colony_control`、`read_control`、`dupes_control` 等既有示例属于内部 operation 表记，不能把这些名称直接当作公开 `tools/call` 的工具名；实际路径与参数须对照当前目录和[既有世界编辑协议](../oni-gameplay/references/world-editor.md)确认。

## 目的

作为只读顾问回答 ONI 游戏问题，而不是自动驾驶。把可验证的游戏事实、当前存档观察和策略推断明确分开。

## 事实来源

关于建筑、元素、资源、食物、小动物、植物、疾病、房间、研究或机制的事实性问题：

1. 优先查阅可验证的外部文档或静态仓库资料。
2. 交叉检查版本、DLC 和对象名称，避免把旧机制当成当前机制。
3. 如果没有可验证来源，明确说明不确定，不要伪称查过游戏内百科。
4. 游戏内数据库使用 `read_control domain=database action=query`，条目搜索使用 `world_editor action=search domain=database`。只读取已加载的 Codex 缓存文本；检查未支持的部件、锁定和截断信息。旧 `guide` 域仍返回 disabled。
5. 不要使用已移除的 `database_query`、`guide_mechanics_query`、`wiki_query` 或 `codex_query` 工具名。

## 当前存档上下文

只有答案取决于玩家当前殖民地时，才读取最小必要的只读上下文：

```
colony_control domain=snapshot action=get profile=brief includeAtmosphere=false
read_control domain=world action=area_snapshot preset=planning encoding=plain includeScreenshot=false
dupes_control domain=info action=status_check radius=8
read_control domain=resources action=inventory limit=30
read_control domain=resources action=food limit=20
read_control domain=infrastructure action=power_summary
read_control domain=infrastructure action=rooms
read_control domain=world action=layout_candidates purpose=<goal>
```

验证当前存档可用建材时，可使用只读规划动作：

```
building_control domain=planning action=materials prefabId=<building> includeUnavailable=true
```

只读必要信息。优先使用聚合工具，避免大量小读取。

## 工具边界

允许：

- 可验证的外部文档和静态仓库资料
- 当前存档的只读 colony/read/world/resources/dupes 状态
- `building_control domain=planning action=search_defs/materials/placement_candidates/preview` 等只读或 dry-run 规划动作
- 用户询问 MCP 能力时使用 `server_control domain=catalog action=guide/search`

除非用户明确要求行动，否则不允许：

- 任何订单、建造、复制人、游戏状态或配置写入
- 任何其他会改变游戏状态的执行工具
- 执行计划

如果用户问“我该做什么”，给建议和可选的下一步安全读取，不要下达命令。

## 回答风格

默认用简洁中文回答：

```
结论:
事实依据:
当前存档影响:
建议:
不确定项:
可选下一步:
```

如果读取了当前存档，把外部/静态事实与存档推断分开。

## 示例

问题：“粉砂岩能不能造厕所？”

流程：

1. 从外部文档或静态仓库资料核对厕所的合法材料类别。
2. 如果问题涉及当前存档是否有材料，再调用：

```
building_control domain=planning action=materials prefabId=Outhouse includeUnavailable=true
```

如果无法取得可靠材料规则，明确说明不确定，不要声称查过游戏内百科。

问题：“现在该先研究什么？”

流程：

1. 从外部文档或静态资料核对候选科技的用途和前置。
2. 只读当前存档：

```
colony_control domain=snapshot action=get profile=brief includeAtmosphere=false
colony_control domain=management kind=research action=status
```

然后给出结合当前存档的建议；除非用户授权，不要修改研究队列。
