# 来源与外部组件

原生绘制基础：[Windows-WeChat-Theme-Studio](https://github.com/starfalll521/Windows-WeChat-Theme-Studio)
固定提交：37d8188502ed4365ddcde928c7b2e50fda5e7226，作者 starfalll521。

`fetch-upstream.ps1` 从该固定提交下载 `native/runtime.c`，文件 SHA-256 为 `4473abd121825524676ce6dcea7fb310bd6266541d10d4039380423ff7be3a06`。`native/prepare-runtime.cjs` 校验源码后生成本地研究构建，增加 `glass-extension.c` 的 Qt 透明背景、DWM 材质、回执及文件校验，并调整分区绘制。DLL 由 MSVC 编译，控制器嵌入其 SHA-256。

用户提供的 -1.0.zip 曾用于检查清单与结构；当前构建不提取该 ZIP 的 DLL。原模块 SHA-256：9ee2f5c8aa9cd4e120507f516d3eb15656e3d2a7b7ee49cd0786a62e43002433。

截至 2026-11-06 检查，上游未提供 LICENSE。公开可读取不等于可以任意再分发。本仓库不附带上游源码、生成的混合源码或编译后的原生 DLL；构建时由用户从上游直接获取固定版本。构建产物包含上游代码，再分发前需获得所需许可或替换相关代码。下载和本地构建步骤不构成上游作者的授权或背书。

视觉参考：[qqnt-liuli-theme](https://github.com/LilycleHeart/qqnt-liuli-theme)
作者 LilycleHeart，参考提交：dda858b7f5366b489773b9b08dac0cac3105e5f4。参考半透明材质、分区与配色思路，未移植 QQ 插件、CSS 或图片素材。该项目检查时同样未声明许可证，本项目不依赖其插件环境。

WPF 控制器、图片处理与裁剪、分区配色编辑器、可读性检查、设置、启动与连接检查、配置回执处理为本项目新写。当前仓库未授予统一开源许可证。第三方代码与素材的权利属于各自权利人；引用与致谢不代替许可。普通 PE、反汇编和 SHA-256 检查用于限定接口假设，不能等同于实机验证。

微信目标：4.1.15.56 x64；Weixin.dll SHA-256：e0428a7b691853de496532523c2c09aa095f90941c37c9f4cfb3f137f3c66556。
