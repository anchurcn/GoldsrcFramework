ClientExportFuncs - Struct that has the same layout of cldll_func_t
IClientExportFuncs - Just a collection of client export funcs declared in a interface.
FrameworkClientExports : IClientExportFuncs - Impl in virtual methods for framework.

ServerExportFuncs - DLL_FUNCTIONS
ServerNewExportFuncs - NEW_DLL_FUNCTIONS
IServerExportFuncs - Combine two
FrameworkServerExports

ClientEngineFuncs - Struct cl_enginefunc_t
ServerEngineFuncs - Struct enginefuncs_t

// Game dll based on GoldsrcFramework usage hint
GameClientExports : FrameworkClientExports - Partial or Fully overrides export funcs of framework(eg DemoClientExports).

按照我的设计参照 HLSDK 给我生成代码吧。先把这些与引擎交互的接口类生成出来，函数签名保留原来的命名，包括类型。
签名举例：(int ent_num, struct hud_player_info_s* pinfo);，而不是(int ent_num, IntPtr pinfo);
这些交互接口 API 不需要带上 pfn 这类前缀。
后续再递归地为函数签名上的未定义类型参考 HLSDK 的结构去生成定义。
在 CustomTypeMapping.txt 中的类型需要重命名，并且不用生成它们的定义了


typedef 到一个已有结构的，直接使用这个结构；
typedef 到一个 primitive 的，需要封装一下，拿 HSPRITE 举例：
[...]
public struct HSPRITE { public int Value; }

Step2:

递归地生成 engine types in EngineTypes.cs

Step3：
按照我标注了的 -fn 的函数去 HLSDK 抄它的实现到 FrameworkExports
