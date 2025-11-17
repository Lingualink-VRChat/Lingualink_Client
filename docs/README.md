# Documentation Index

LinguaLink Client 的文档按“主文档 + 参考文档”划分，方便不同角色快速找到入口。

## 主文档（面向本客户端项目）

- `Summary.md`  
  项目总览与架构说明：功能概述、技术栈、MVVM 分层、服务/事件体系等，是开发者理解客户端整体设计的首选入口。

- `ReleaseGuide.md`  
  发布与交付指南：本地构建、打包、上传到 rains3 以及发布前后检查清单，面向负责发版的同学。

> 结合仓库根目录下的 `README.md` 与 `AGENTS.md`，可以快速了解项目结构、代码规范和基本开发流程。

## 参考文档（跨项目 / 外部系统）

放在 `docs/reference/` 下，作为客户端依赖的“外部系统”或“周边”文档：

- `reference/API_Documentation.md`  
  **Lingualink Core API Documentation (v2.0)**：后端核心服务 HTTP API 的说明，适合需要同时了解服务端能力或调试请求/响应的开发者。

- `reference/WebDownloadLinkGuide.md`  
  Web 前端下载页链接维护指南，面向维护官网/下载页的前端或运营同学，与客户端发布脚本协同工作。

未来若增加 Lingualink Core 相关说明、脚本细节、运维手册等，也建议统一放在 `docs/reference/`（或其子目录）中，避免与客户端自身文档混在一起。

