# HLSDK Native/Humanizer 分文件规则草案

目标：Raw generated 仍集中在 `Native/Generated/HlsdkNative.generated.cs` 作为机器事实源；正式 Native API 由 Humanizer 模块生成到 `src/GoldsrcFramework.Engine/Native/`，按稳定规则拆成多个源码文件，方便 git 审查和人工维护。

## 总原则

1. **显式映射优先**：已经确认有正式文件名的类型，不再参与自动归类。
2. **模块化 header 优先**：如果原始 HLSDK `.h` 本身就是明确模块，则该 header 里的相关类型合并到一个模块文件。
3. **杂乱 header 不直接映射文件**：例如 `const.h`、`eiface.h` 这种包含多类概念的 header，需要按类型用途或显式规则拆。
4. **一类一个文件 vs 一模块一个文件**：函数表这类 public API 类型适合一个类一个文件；紧密相关的小结构/枚举适合一个模块文件。
5. **Raw trace 保留**：正式 Native 文件里继续通过 XML remarks 保留 `Original` 和 typedef alias 原始声明。
6. **避免与旧手写类型冲突**：替换旧文件时直接覆盖；新增数据结构文件前，需要先从旧大文件中移除或停止生成同名类型。

## 已确认：一个类型一个正式文件

这些已经是正式 Humanizer function table，应继续一类一个文件：

| Raw 类型 | 正式类型 | 文件 |
|---|---|---|
| `cl_enginefuncs_s` / `cl_enginefunc_t` | `ClientEngineFuncs` | `Native/ClientEngineFuncs.cs` |
| `cldll_func_t` | `ClientExportFuncs` | `Native/ClientExportFuncs.cs` |
| `enginefuncs_s` / `enginefuncs_t` | `ServerEngineFuncs` | `Native/ServerEngineFuncs.cs` |
| `DLL_FUNCTIONS` | `ServerExportFuncs` | `Native/ServerExportFuncs.cs` |
| `NEW_DLL_FUNCTIONS` | `ServerNewExportFuncs` | `Native/ServerNewExportFuncs.cs` |

理由：这些是 ABI 顶层函数表，是插件/引擎交互的主要 public API，独立文件最清楚。

## 建议：按模块 header 合并

以下 header 模块语义较强，适合映射成模块文件：

| HLSDK header | 建议文件 | 内容类型 |
|---|---|---|
| `engine/studio.h` | `Native/Studio.cs` | Studio model header、bone、sequence、texture、attachment 等 studio 格式结构 |
| `common/r_studioint.h` | `Native/StudioApi.cs` | `r_studio_interface_t`、`engine_studio_api_t` 等 studio renderer API |
| `common/cl_entity.h` | `Native/ClientEntity.cs` | `cl_entity_t`、latched vars、mouth、position history 等 client entity 类型 |
| `common/entity_state.h` | `Native/EntityState.cs` | `entity_state_t`、`entityType` 相关网络实体状态 |
| `common/event_args.h` / `event_api.h` | `Native/Event.cs` | event args、event api、event flags |
| `common/netadr.h` / `net_api.h` | `Native/Net.cs` | network address、net api、net status/response |
| `common/pmtrace.h` | `Native/PlayerMoveTrace.cs` | pmtrace、plane、trace 相关类型 |
| `common/ref_params.h` | `Native/RefParams.cs` | `ref_params_t`，含 `float[3] -> Vector3` 规整 |
| `common/usercmd.h` | `Native/UserCmd.cs` | `usercmd_t`，含 `Vector viewangles` 等 |
| `pm_shared/pm_movevars.h` | `Native/PlayerMoveVars.cs` | `movevars_t` |
| `cl_dll/kbutton.h` | `Native/Input.cs` | `kbutton_t` |

## 建议：杂乱/基础 header 按用途拆

这些 header 不建议简单一对一映射：

### `common/const.h`

内容偏基础常量和基础小类型，建议拆到：

- `Native/EngineConstants.cs`：常量。
- `Native/ColorTypes.cs`：`color24`、`colorVec` 等颜色类型。
- `Native/EngineHandles.cs`：`HSPRITE`、`qboolean`、`string_t` 等 primitive typedef wrapper 或 handle。

### `engine/eiface.h`

这个文件很大且混合 server engine funcs、server exports、save/restore、entvars、resource 等概念。建议拆：

- `Native/ServerEngineFuncs.cs`：已确认。
- `Native/ServerExportFuncs.cs`：已确认。
- `Native/ServerNewExportFuncs.cs`：已确认。
- `Native/EntVars.cs`：`entvars_t`。
- `Native/Edict.cs`：`edict_t`、`edict` 相关。
- `Native/SaveRestore.cs`：`SAVERESTOREDATA`、`TYPEDESCRIPTION`、field types。
- `Native/Resource.cs`：`resource_t`、resource type、customization。
- `Native/KeyValue.cs`：`KeyValueData`。

### `common/com_model.h`

内容是 BSP/model/cache/hull/texture 混合，建议拆：

- `Native/Model.cs`：`model_t`、model type/sync type。
- `Native/BspModel.cs`：`mnode_t`、`mleaf_t`、`mplane_t`、`msurface_t`、`hull_t`、`dclipnode_t` 等 BSP collision/render structures。
- `Native/Texture.cs`：`texture_t`、`mtexinfo_t`、`decal_t`。
- `Native/Cache.cs`：`cache_user_t`、`surfcache_t` opaque/related wrappers。

## 命名前后缀辅助规则

当 header 不足以判定时，用以下命名规则辅助：

| 模式 | 目标文件 |
|---|---|
| `mstudio*`、`studiohdr_t`、`mstudiobodyparts_t` | `Studio.cs` |
| `cl_*`、`client_*`、`hud_*` | `Client*.cs` 或对应 client 模块 |
| `pm*`、`playermove*`、`physent_t` | `PlayerMove*.cs` |
| `net*`、`netadr*` | `Net.cs` |
| `event_*`、`event_args_t` | `Event.cs` |
| `edict_t`、`entvars_t`、`entity_state_t` | 对应 `Edict.cs`、`EntVars.cs`、`EntityState.cs` |
| `resource*`、`customization*` | `Resource.cs` |
| `sequence*`、`sentence*` | `TextMessage.cs` 或 `Sequence.cs` |

## 生成器实现建议

新增一个分文件决策层：

1. `HumanizerFileMapping.txt`：显式 `type -> file` 映射，最高优先级。
2. `HeaderModuleMapping.txt`：`header path suffix -> file` 映射，例如 `engine/studio.h -> Studio.cs`。
3. `TypeNameRule`：按前后缀和已知类型名归类。
4. fallback：`NativeTypes.cs` 或 `Misc.cs`，并在 manifest/report 里列出来供人工审查。

决策顺序：

```text
ExplicitTypeMapping
  -> HeaderModuleMapping if header is known-module
  -> TypeNameRule
  -> Fallback/Misc
```

## 需要生成的审查报告

为方便 git 审查，建议每次 CodeGen 同时输出：

- `Native/Generated/HlsdkNative.filemap.generated.md`

内容包括：

```text
TypeName -> TargetFile -> Reason -> SourceHeader
```

例如：

```text
ClientEngineFuncs -> ClientEngineFuncs.cs -> explicit -> engine/cdll_int.h
mstudioseqdesc_t -> Studio.cs -> header-module -> engine/studio.h
entvars_t -> EntVars.cs -> explicit/type-rule -> engine/eiface.h
```

## 迁移步骤建议

1. 已完成：五个 function table 独立正式文件。
2. 下一步：从 `EngineTypes.cs` 中拆出独立高价值类型：`UserCmd.cs`、`RefParams.cs`、`EntityState.cs`、`ClientData.cs`、`EntVars.cs`。
3. 然后：按模块拆 `Studio.cs` / `StudioApi.cs`，替换 `StudioTypes.cs`。
4. 最后：处理 `Model.cs` / `BspModel.cs` / `Resource.cs` 等杂项模块。
