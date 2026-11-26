# GeLanCup2026 项目全局记忆

更新于 2026-11-26。接手任务的 Agent 先读完本文件，再按下列流程开工。

## 开工前配置 Unity MCP

1. 用 [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt) 指定的 Unity `2022.3.62f3` 打开当前仓库，等待包导入完成。[Packages/manifest.json](Packages/manifest.json) 已声明 MCP for Unity `v10.0.0`。
2. 在 Unity 打开 **Window → MCP for Unity → Toggle MCP Window**。在 **Server** 区启动当前使用的连接，本地 HTTP 模式下使用 **Start Server**。在 **Client Configuration → Per-client setup** 选实际使用的客户端并点 **Configure**。需要手动配置时，从 **Manual Configuration** 复制该客户端所需配置，保存后重新加载客户端。
3. 用当前 Agent 的 MCP 客户端执行一次只读检查，例如读取 Unity Editor 状态或当前场景，核对工程路径确实指向正在协作的 GeLanCup2026 工作副本。若有多个 Unity 实例，先选对实例。连接失败时先排查 Unity、服务和客户端的连接状态，再开始 Unity 开发。

## 故事背景

这是一个 2D 俯视探索解谜游戏。主角从漂流瓶中发现格兰岛藏宝图，希望用宝藏筹备与青梅的婚礼。他与青梅、兄弟结伴出海，在暴雨中继续前往格兰岛，沿图找到遗迹石门。三人进入迷宫后，寻宝变成了寻找同伴和逃离迷宫。青梅在一层被掳走，兄弟在二层牺牲，第三层由主角独自面对圣女像和最后的选择。

## 序章到迷宫入口

| 阶段 | 玩家经历与分支 |
| --- | --- |
| 漂流瓶 | 主角发现藏宝图，想到婚礼，也怀疑地图的真假。选择「留下来攒钱」会显示「恭喜你，回家种地去吧。」并回主菜单。选择「约青梅和兄弟一起出海」则由三人约定次日码头见，进入出航。 |
| 暴雨出航 | 雨势加大，青梅问要不要掉头。「立即返航」显示相同的提示并回主菜单。「继续前进」接登岛剧情。 |
| 格兰岛 | 三人冒雨登岛，循藏宝图前进。兄弟先自动走到北侧遗迹石门，发现门后叫两人过来。接着玩家控制主角走过去，青梅仅在这段临时跟随，到门前结束跟随。后续只有剧情明确要求时才启用跟随。 |
| 进入遗迹 | 三人在门口汇合并对话，谈到寻宝和婚礼。门前对话与开门演出结束后恢复自由移动。玩家与石门交互，进入迷宫入口。 |

序章台词和选项见 [序章对话](Assets/Story/Prologue/Dialogue.json)，登岛及石门对话见 [登岛对话](Assets/Story/Island/Dialogue.json)，登岛行走演出见 [IslandArrival.cs](Assets/Story/Island/IslandArrival.cs)。

## 三层迷宫

以下主线依据《GeLanCup2026-迷宫总图与房间设计.pdf》及历史确认记录整理。原策划截图和一层地图手稿见「生成游戏地图概念稿」的协作记录，机关和演出细节尚未逐项核对。落实具体谜题、通道机关与结局演出前，回查原策划或团队更新过的定稿。

| 关卡 | 玩家经历与分流 | 剧情落点 |
| --- | --- | --- |
| 一层 | 三人从遗迹入口进安全房，入口随后关上。安全房有圣女像和纸条，两扇分流门分别通往解谜路线与献祭路线。解谜路线从暗处的刻痕入手，借手电取得线索、收集密码并开门。献祭路线沿通道推进。两条路都接到二层。 | 解谜路能发现墙上的「快逃」和疑似青梅的影子。青梅在一层离队，被掳走。主角和兄弟继续向上找她。 |
| 二层 | 主角与兄弟进入相似的安全房。圣女像变为伸手姿态，纸条问「想救她吗」。两人可走探索路或献祭路，两条路都通向三层。 | 探索路线通关时兄弟牺牲。献祭路遇到黑影时，兄弟为主角争取逃跑时间，主角沿原路逃跑时跌入三层。 |
| 三层 | 主角独自在终局房醒来，在圣女像前选择「献祭自己」或「献祭他人」，随后从同一扇北门触发各自的结局。 | 「献祭自己」进入团灭结局，主角仍被困在迷宫。「献祭他人」进入人格迷失结局，主角离开岛，但把真实的自己留在迷宫。 |

视觉和玩法中保留俯视、昏暗环境与手电探索，石门可以使用之前确认过的「轮回」印记。三名角色共享 SAN，与圣女像交互可恢复 SAN，遭怪物攻击会扣除 SAN。游戏失败回到上一段完整对话的结尾，恢复该检查点保存的分支状态。

## 主线场景和资源目录

| 路径 | 用途 |
| --- | --- |
| [`Assets/Scenes/MainMenu.unity`](Assets/Scenes/MainMenu.unity) | 新游戏由此进入序章。继续游戏恢复保存的主线进度。 |
| [`Assets/Scenes/GameScene/Scene0.unity`](Assets/Scenes/GameScene/Scene0.unity) | 漂流瓶、出海和暴雨中的两次选择。 |
| [`Assets/Scenes/GameScene/Scene1.unity`](Assets/Scenes/GameScene/Scene1.unity) | 登岛、兄弟先到石门、玩家带青梅会合及进门。 |
| [`Assets/Scenes/GameScene/Scene2.unity`](Assets/Scenes/GameScene/Scene2.unity) | 石门后的迷宫入口，北门通往一层安全房。 |
| [`Assets/Scenes/GameScene/Scene3.unity`](Assets/Scenes/GameScene/Scene3.unity) | 一层安全房、解谜和献祭分流。 |
| [`Assets/Scenes/GameScene/Scene4.unity`](Assets/Scenes/GameScene/Scene4.unity) | 二层安全房、探索迷宫和献祭通道。 |
| [`Assets/Story/`](Assets/Story/) | 剧情演出脚本、对话数据和配套资源。 |

所有新增的主线流程场景都在 `Assets/Scenes/GameScene/` 创建。该目录只放 Unity 场景文件及对应的 `.meta`。剧情数据、脚本、图片、预制体等按用途放在已有资源目录。接通新场景时核对 [构建场景列表](ProjectSettings/EditorBuildSettings.asset) 和前后场景入口。`DialogueTestRoom` 与 `SampleScene` 用于测试。

## 地图素材和审核

1. **先复用。** 地牢地图优先在 [`Assets/MapArt/GunnerH/`](Assets/MapArt/GunnerH/) 找现成素材。检查 [`Tilemap/Palettes/`](Assets/MapArt/GunnerH/Tilemap/Palettes/) 下的 `Dungeon1` 至 `Dungeon6`，以及 `Floor`、`Statues` 等画板。原图在 [`Textures/Tilemap/`](Assets/MapArt/GunnerH/Textures/Tilemap/)。
2. **缺件才生成。** 已有素材无法满足关卡所需的地块、墙壁、门或机关时，先确定缺件用途，再挑选将与它相邻的已有地牢素材作参考。生成图须贴合现有地牢的俯视角、像素密度、Tile 网格、石材轮廓、配色和明暗，能与相邻素材自然拼接。
3. **审核通过后入图。** 先交给团队看候选图和它与现有地牢素材并排的效果。收到明确通过后，再把生成素材加入正式地图。等待审核期间可用现有 Tile 或占位物接通关卡流程。
