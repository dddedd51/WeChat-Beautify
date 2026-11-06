# 来源与外部组件

## 0.6 当前构建

默认 `build.ps1` 只编译 `native/independent/engine-entry.c`、`semantic-engine.cpp`、本仓库客户端与相应头文件。没有下载、编译或打包上游 runtime.c、旧混合引擎、Frida 或 Ghidra。

自研引擎采用按版本系列选择的共享虚表接入、原生背景绘制、GUI 线程配置轮询和请求回执。0.6 允许同系列未经实测的构建通过结构筛查后尝试加载，目标哈希用于识别实测文件；自研模块本身仍校验完整哈希。布局/ABI事实从基准 Weixin.dll 独立静态检查与 REA Frida 运行观察核对，不等于其他小版本兼容保证。旧引擎的背景、文字分层设计曾被阅读和研究；这不是法律意义上经过隔离的 clean-room 项目，不作无条件的无侵权或再许可保证。

客户端、图片裁剪/处理、配置协议、可读性检查、青屿渐变背景和界面图形由本项目生成或编写。个人导入图片不打包、不上传。Windows、.NET 和微信相关权利属于各自权利人。

## 历史原生引擎参考

[Windows-WeChat-Theme-Studio](https://github.com/starfalll521/Windows-WeChat-Theme-Studio)，作者 starfalll521，固定提交 `37d8188502ed4365ddcde928c7b2e50fda5e7226`。

旧版使用 `native/runtime.c`，SHA-256 `4473abd121825524676ce6dcea7fb310bd6266541d10d4039380423ff7be3a06`。历史脚本 `fetch-upstream.ps1` 和 `native/prepare-runtime.cjs` 保留以解释旧构建，但默认构建不再调用。不要把旧 dist、research 中的混合 DLL 加入新发布包。

截至 2026-11-06 检查，上游未提供明确 LICENSE；引用致谢不能代替授权。旧上游源码、生成混合源码和二进制均不纳入本仓库再分发。

用户提供的 `-1.0.zip` 曾用于结构调查；当前构建不提取或加载其中的 DLL。

## 视觉参考

[qqnt-liuli-theme](https://github.com/LilycleHeart/qqnt-liuli-theme)，作者 LilycleHeart，参考提交 `dda858b7f5366b489773b9b08dac0cac3105e5f4`。参考半透明材质、分区与配色思路，未移植插件、CSS 或素材，不依赖 QQ 插件环境。

## 适配与许可边界

适配基准为 Windows 微信 4.1.15.56 和 4.2.0.13 x64；文件哈希、对应系列、实测范围及试用限制见 README 和版本适配清单。不打包或修改微信安装文件。分析工具仅用于开发，分析证据保留在忽略的 research 目录；公开研究摘要见 `native/independent/RESEARCH.md`。

当前仓库没有统一开源许可证。公开源码不等于向所有第三方内容授予许可。项目与腾讯、微信和参考作者均无隶属关系。
