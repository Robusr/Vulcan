<p align="center">
  <img src="docs/assets/logo.svg" alt="Vulcan 标志" width="96" height="96">
</p>

<h1 align="center">Vulcan</h1>

<p align="center">
  <strong>自然语言输入，参数化 SolidWorks 几何输出。</strong><br>
  一款把工程师的自然语言需求直接转化为可编辑 SolidWorks 特征的 AI 插件。
</p>

<p align="center">
  <a href="https://github.com/Robusr/Vulcan/stargazers"><img src="https://img.shields.io/github/stars/Robusr/Vulcan?style=flat-square&amp;color=f97316" alt="GitHub 星标"></a>
  <a href="https://github.com/Robusr/Vulcan/network/members"><img src="https://img.shields.io/github/forks/Robusr/Vulcan?style=flat-square&amp;color=2563eb" alt="GitHub Fork"></a>
  <img src="https://img.shields.io/badge/SolidWorks-2025-e63946?style=flat-square" alt="SolidWorks 2025">
  <img src="https://img.shields.io/badge/.NET_Framework-4.8-512BD4?style=flat-square&amp;logo=dotnet&amp;logoColor=white" alt=".NET Framework 4.8">
  <img src="https://img.shields.io/badge/Python-3.9+-3776AB?style=flat-square&amp;logo=python&amp;logoColor=white" alt="Python 3.9+">
  <img src="https://img.shields.io/badge/Flask-3.0-000000?style=flat-square&amp;logo=flask&amp;logoColor=white" alt="Flask 3.0">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-GPLv3-blue?style=flat-square" alt="GPLv3"></a>
</p>

<p align="center">
  <a href="README.md">English</a> ·
  <a href="README.zh-CN.md"><strong>简体中文</strong></a> ·
  <a href="README.fr.md">Français</a>
</p>

<p align="center">
  <a href="#名称由来">名称由来</a> ·
  <a href="#它能做什么">它能做什么</a> ·
  <a href="#能力一览">能力一览</a> ·
  <a href="#系统架构">系统架构</a> ·
  <a href="#json-契约">JSON 契约</a> ·
  <a href="#快速开始">快速开始</a> ·
  <a href="#路线图">路线图</a>
</p>

<p align="center">
  <img src="docs/assets/banner.zh.svg" alt="Vulcan：自然语言输入，参数化几何输出" width="100%">
</p>

## 名称由来

SolidWorks 精确、强大，也以点击繁多著称。一块带四个孔的安装板，往往要经历十几次操作：选择基准面、绘制矩形、标注尺寸、退出草图、拉伸、选择表面、绘制圆、做阵列、完全贯穿切除。

Vulcan 把这一串操作压缩成一句话。名字来自罗马神话中的火神与锻冶之神伏尔甘（Vulcan）——熔炉是原材料与刻意意图相遇的地方。在这里，原材料是语言，意图是一棵参数化特征树。

仓库保留了两代实现：

- **`beta/`**——最初的概念验证。大模型生成 Python 代码，客户端用 `exec()` 执行。
- **`release/`**——当前维护的架构。大模型返回类型化的 JSON 特征树，由 C# 插件确定性执行。

本 README 以 `release/` 为主；`beta/` 作为参考与设计演进的记录保留。

## 它能做什么

<p align="center">
  <img src="docs/assets/demo.svg" alt="一句中文需求生成 200 × 100 × 20 毫米、带四个通孔的安装板" width="100%">
</p>

<p align="center">
  <sub>示意图。同一份契约可以承载一个基础拉伸和一组有序切除。</sub>
</p>

1. **描述零件。** 在插件面板中输入需求。多语言模型可以理解中文、英文和法文；系统提示词本身以中文编写。
2. **模型填充模式。** 服务端拼接版本化的 SolidWorks 领域提示词，调用任意 OpenAI 兼容端点。模型返回 JSON，而不是代码。
3. **插件完成建模。** C# 客户端反序列化特征树，绘制草图并调用 SolidWorks COM API。结果是原生、可编辑的特征树。

## 能力一览

| 能力 | 说明 |
|---|---|
| 自然语言建模 | 一句需求变成有序特征树：先基础特征，后续特征依次叠加。 |
| 原生输出 | 生成可编辑的 SolidWorks 特征，而不是网格、宏或脚本。 |
| 结构化契约 | JSON 信封承载 `extrude` 与 `cut`；JSON 用毫米，COM 边界转换为米。 |
| 草图形状库 | 圆形、矩形、正多边形、椭圆、直槽口。 |
| 孔位控制 | 显式 `center_x`、`center_y`、`through_all`，以及四角孔启发式。 |
| 提供商自由 | OpenAI、DeepSeek、通义千问、七牛云、vLLM、Ollama 或任意兼容端点。 |
| 服务端可移植 | 本地、Docker 或隧道后运行；只有客户端需要 Windows。 |
| 可调试管线 | release 版本没有 `exec()`；每个阶段都有日志和类型。 |
| API 优先执行 | 先调用 COM 特征 API，失败时使用键盘驱动的原生面板兜底。 |

## 系统架构

<p align="center">
  <img src="docs/assets/architecture.svg" alt="Vulcan 系统架构：Windows 客户端、Python 服务端、大模型提供商" width="100%">
</p>

| 层次 | 技术栈 | 职责 |
|---|---|---|
| 客户端 | C# / WPF / .NET Framework 4.8 | `Vulcan AI` 功能区标签页、提示面板、JSON 信封处理、COM 执行 |
| 传输 | HTTP REST / JSON | 单一端点：`POST /api/v1/generate` |
| 服务端 | Python 3.9+ / Flask 3 | 提示词组装、上游大模型调用、信封校验、错误映射 |
| 模型 | OpenAI 兼容 API | JSON 特征树生成 |
| 部署 | Docker、反向代理或裸机 | 服务端任意部署；客户端与 SolidWorks 同机 |

插件会在零件、装配和工程图中注册 `Vulcan AI` 功能区标签页，同时提供工具栏和菜单命令。按钮打开一个模态 WPF 面板，而不是独立的 CAD 应用。

<p align="center">
  <img src="docs/assets/panel.svg" alt="Vulcan 插件提示面板布局示意" width="76%">
</p>

<p align="center">
  <sub>500 × 300 提示面板的布局示意。实际交付界面为中文；面板接受自然语言需求。</sub>
</p>

## 请求生命周期

<p align="center">
  <img src="docs/assets/pipeline.svg" alt="六阶段请求生命周期，包含延迟与失败处理" width="100%">
</p>

1. **请求。** 插件把 `{ "prompt": "..." }` 发送到 Flask 服务端。
2. **提示词组装。** 服务端在用户文本前拼接版本化的 SolidWorks 领域提示词。
3. **模型调用。** 任意 OpenAI 兼容端点返回 JSON 特征树。默认参数为 `temperature 0.05`、`top_p 0.95`、`max_tokens 2048`，超时 30 秒。
4. **恢复。** 服务端去除 Markdown 代码围栏，再截取第一个 `{` 到最后一个 `}` 后解析。
5. **校验。** Flask 检查响应是单特征还是多特征信封；C# 客户端把信封反序列化为类型化模型类，再从参数字典读取特征参数并应用建模器默认值。
6. **执行。** `SwModeler` 绘制草图、选中草图，并调用 `FeatureExtrusion3` 或 `FeatureCut4`。若 COM 调用失败，键盘驱动的兜底方案会打开 SolidWorks 原生特征面板。

模型调用占据绝大部分延迟；其余步骤都在本地，通常在毫秒级。

## JSON 契约

<p align="center">
  <img src="docs/assets/contract.svg" alt="带注释的 JSON 特征树：信封、特征类型、形状参数、单位与放置" width="100%">
</p>

### 信封

响应可以是单个特征：

```json
{
  "feature_type": "extrude",
  "name": "Cylinder",
  "params": {
    "plane": "前视基准面",
    "shape": "circle",
    "diameter": 50,
    "depth": 100
  }
}
```

也可以是有序的多特征模型：

```json
{
  "model_name": "Mounting plate",
  "features": [
    {
      "feature_type": "extrude",
      "name": "Base",
      "params": {
        "plane": "前视基准面",
        "shape": "rectangle",
        "length": 200,
        "width": 100,
        "depth": 20
      }
    },
    {
      "feature_type": "cut",
      "name": "Hole 1",
      "params": {
        "plane": "前视基准面",
        "shape": "circle",
        "diameter": 10,
        "depth": 20,
        "through_all": true,
        "center_x": 90,
        "center_y": 40
      }
    }
  ]
}
```

这里只展示四个孔中的一个；完整响应会重复 `cut` 特征，填入其余三个角的坐标。

### 特征模式

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `feature_type` | string | 是 | `extrude` 或 `cut` |
| `name` | string | 否 | 用于日志的可读名称 |
| `params.plane` | string | 是 | 请求的基准面；见下方实现说明 |
| `params.shape` | string | 是 | `circle`、`rectangle`、`polygon`、`ellipse` 或 `slot` |
| `params.depth` | number | 是 | 盲拉伸/盲切除深度，毫米；`through_all` 为 true 时忽略 |
| `params.through_all` | boolean | 否 | 仅切除；默认 `false`；为 true 时双向切除 |
| `params.center_x` | number | 多孔模型的切除特征 | 草图中心在第一个草图轴上的坐标，毫米 |
| `params.center_y` | number | 多孔模型的切除特征 | 草图中心在第二个草图轴上的坐标，毫米 |

### 草图形状参数

| 形状 | 必填字段 | 说明 |
|---|---|---|
| `circle` | `diameter` | 毫米 |
| `rectangle` | `length`、`width` | 以 `center_x`、`center_y` 为中心 |
| `polygon` | `diameter`、`sides` | `sides` 限制在 3–100 |
| `ellipse` | `major_axis`、`minor_axis` | 毫米 |
| `slot` | `length`、`width` | 直槽口；宽度即两端圆弧直径 |

### 坐标与单位

- **前视基准面：** 草图轴为 X、Y；`center_x` 对应 X，`center_y` 对应 Y。
- **上视基准面：** 草图轴为 X、Z；`center_y` 对应 Z，且必须落在基体厚度内。
- **右视基准面：** 草图轴为 Y、Z；`center_x` 对应 Y，`center_y` 对应 Z。
- **多特征模型：** 先读取基体的 `length`、`width`、`depth`，再计算后续坐标。不要把多个相同特征堆在 `(0, 0)`。
- **单位：** JSON 中所有尺寸均为毫米；建模器在 COM 边界转换为米。

### 实现说明

当前 release 建模器会把草图统一归一化到前视基准面。`plane` 字段仍保留在契约中，并被提示词使用，但通用基准面路由仍在路线图上。对于四角孔，当用户用中文明确要求“四角”“4个”“四个”或“4通孔”时，客户端会按 `±(length / 2 − 10)` 与 `±(width / 2 − 10)` 自动分配孔位。

## 设计取舍

<p align="center">
  <img src="docs/assets/approach.svg" alt="Beta 代码生成路线与 Rebuild JSON 契约路线的对比" width="100%">
</p>

### 用 JSON 参数，而不是生成代码

Beta 版本让大模型直接输出 Python 代码，由客户端用 `exec()` 执行。这种方式在演示中很灵活，也很惊艳，但工程上很脆弱：幻觉生成的 API 调用可能让 SolidWorks 崩溃，错误恢复几乎不可能，调试意味着阅读一段没人写过的代码。

Rebuild 版本把大模型当作**由模式驱动的参数填充器**。模型只负责填充一个小而类型化的模板，C# 客户端掌握执行权。JSON 合法，特征调用就是良构的；JSON 不合法，信封校验会在触及 SolidWorks 之前将其拦截。release 客户端中没有 `exec()`，也没有代码注入面。

### 薄客户端，可移植服务端

1. **密钥留在服务端。** 把大模型 API 密钥嵌入客户端宏不可接受。
2. **提示词可以快速迭代。** 更新服务端提示词只需几秒；发布新插件则需要重新构建和安装。
3. **服务端可以无头运行。** 这为批处理、CI 检查和未来的 Web 前端保留了空间。

### 领域提示词，而不是通用提示词

通用模型在 CAD 任务上表现不佳，不是因为不够聪明，而是因为缺少上下文。它们不知道前视基准面和上视基准面的区别，也不知道切除特征的 Z 坐标必须落在基体厚度内。

Vulcan 通过 [`system_prompt.py`](release/server/prompts/system_prompt.py) 提供这些上下文：基准面与坐标轴的映射、特征模式、形状专属字段、输出纪律，以及多特征模型的防重叠规则。

### API 优先，UI 兜底

SolidWorks 互操作接口对版本很敏感。建模器直接调用 `FeatureExtrusion3` 和 `FeatureCut4`。如果调用失败，兜底方案会激活 `RunCommand`，用模拟按键填写原生特征面板。

兜底方案是务实的，不是优雅的。生成模型时请保持 SolidWorks 在前台；如果兜底频繁触发，应视为需要更新互操作参数的信号。

## 提示词工程

系统提示词位于 [`release/server/prompts/system_prompt.py`](release/server/prompts/system_prompt.py)，版本为 `PROMPT_VERSION = "2.0.0"`，约 140 行。它编码了五件事：

1. **输出纪律。** 只输出 JSON——没有 Markdown、没有解释、没有代码围栏。
2. **坐标规则。** 逐基准面的轴映射，并给出 200 × 100 × 20 基体与四个孔的完整示例。
3. **特征模式。** `extrude` 和 `cut` 的必填/可选字段，以及形状专属参数。
4. **防重叠约束。** 多个相同特征必须使用不同坐标，绝不能堆在原点。
5. **基准面词汇。** 模型必须输出 `前视基准面`、`上视基准面`、`右视基准面` 等中文名称。

提示词与业务逻辑刻意分离，因此可以独立迭代，不必改动服务端。模型调用使用较低的温度，因为这项任务奖励一致性，而不是创造力。

## 快速开始

### 环境要求

| 组件 | 要求 |
|---|---|
| 服务端 | Python 3.9–3.11，以及一个 OpenAI 兼容 API 密钥 |
| 客户端 | Windows 10/11、SolidWorks 2025（已测试）、.NET Framework 4.8 |
| 构建工具 | Visual Studio 2022，含 .NET 桌面开发工作负载 |
| 网络 | 客户端能访问服务端；服务端能访问大模型提供商 |

### 1. 克隆仓库

```bash
git clone https://github.com/Robusr/Vulcan.git
cd Vulcan
```

### 2. 启动服务端

```bash
cd release/server

python -m venv venv
source venv/bin/activate   # Windows: venv\Scripts\activate

pip install -r requirements.txt
cp .env.example .env
# 编辑 .env：填入提供商密钥、Base URL 和模型名称

python app.py
# → http://0.0.0.0:5000
```

当前 `app.py` 显式绑定 5000 端口并开启 Flask 调试模式。在把服务端暴露到可信网络之外前，请同时修改这两项。

### 3. 或使用 Docker

```bash
cd release/server
docker build -t vulcan-server .
docker run --rm -p 5000:5000 --env-file .env vulcan-server
```

### 4. 验证 API

```bash
curl -s http://127.0.0.1:5000/api/v1/generate \
  -H "Content-Type: application/json" \
  -d '{"prompt":"前视基准面拉伸200x100x20底座，四角各打一个直径10mm通孔"}'
```

响应就是特征树本身，没有额外包装。第一次请求可能需要几秒钟等待模型返回。

### 5. 安装或构建客户端

**方案 A——预编译安装包。** 在 Windows 上运行 `release/client/Vulcan.SolidWorksClient/Vulcan Setup/Output/Vulcan.exe`。安装包会调用 `RegAsm` 注册插件。已编译的客户端使用 `Services/ApiClient.cs` 中硬编码的端点；如果要连接自己的服务端，请从源码构建。

**方案 B——从源码构建。**

1. 在 Visual Studio 2022 中打开 `release/client/Vulcan.SolidWorksClient/Vulcan.SolidWorksClient.sln`。
2. 还原 NuGet 包（客户端使用 `Newtonsoft.Json`）。
3. 把 SolidWorks 互操作引用指向本机安装位置。项目中含有 `D:\Program Files\SOLIDWORKS Corp\...` 下的绝对 `HintPath`；请更新这些路径，或使用 `ReferenceDLL/` 中版本匹配的程序集。
4. 在 [`ApiClient.cs`](release/client/Vulcan.SolidWorksClient/Services/ApiClient.cs) 中把 `_baseUrl` 改为你的服务端地址。
5. 构建 `Release / x64`。SolidWorks 是 64 位程序，不要发布 x86 构建。
6. 在输出目录中，以管理员身份打开命令提示符，运行 `install.bat`，或执行 `RegAsm.exe Vulcan.SolidWorksClient.dll /codebase`。
7. 重启 SolidWorks，打开一个零件文档。`Vulcan AI` 功能区标签页应当出现。

### 6. 生成第一个模型

打开 Vulcan 面板，尝试以下任一提示词：

```text
前视基准面拉伸200x100x20底座，四角各打一个直径10mm通孔
```

```text
Create a 200 x 100 x 20 mm base on the front plane, with a 10 mm through hole at each corner.
```

```text
Créez une base de 200 x 100 x 20 mm sur le plan de face, avec un trou traversant de 10 mm à chaque coin.
```

点击**开始生成**。插件完成建模、自动缩放到合适视图，并留下可编辑的特征树。

## 配置说明

### 服务端环境变量

```env
QINIU_API_KEY="sk-xxxxxxxxxxxxxxxx"
QINIU_BASE_URL="https://api.openai.com/v1"
QINIU_MODEL="gpt-4o-mini"

FLASK_ENV="development"
FLASK_RUN_PORT="5000"
```

`QINIU_*` 是历史命名：第一次部署使用七牛云。客户端本身兼容 OpenAI 接口，因此这些名称只对 `config.py` 有意义。

| 变量 | 默认值 | 用途 |
|---|---|---|
| `QINIU_API_KEY` | 无 | 提供商 API 密钥 |
| `QINIU_BASE_URL` | 无 | OpenAI 兼容 Base URL |
| `QINIU_MODEL` | `gpt-oss-120b` | 模型名称 |
| `FLASK_ENV` | 无 | 由 `Config.DEBUG` 读取；入口仍显式设置 `debug=True` |
| `FLASK_RUN_PORT` | `5000` | 由 `config.py` 读取；当前 `app.py` 直接绑定 5000 |

### 提供商示例

| 提供商 | `QINIU_BASE_URL` | 模型示例 |
|---|---|---|
| OpenAI | `https://api.openai.com/v1` | `gpt-4o-mini` |
| DeepSeek | `https://api.deepseek.com/v1` | `deepseek-chat` |
| 通义千问（DashScope 兼容模式） | `https://dashscope.aliyuncs.com/compatible-mode/v1` | `qwen-plus` |
| Ollama | `http://localhost:11434/v1` | 本地部署的模型 |
| vLLM | `http://localhost:8000/v1` | 你部署的模型 |

建议选择指令遵循能力强、JSON 输出稳定的模型。较弱的模型通常会出现这些问题：Markdown 包裹、JSON 前后夹杂解释，或坐标重叠。

### 客户端端点

```csharp
private readonly string _baseUrl = "http://127.0.0.1:5000";
```

源码中的默认值指向一个硬编码主机。部署自己的服务端前，请先修改。

### 参数调节

- [`llm_client.py`](release/server/services/llm_client.py) 中的 `temperature 0.05`、`top_p 0.95`、`max_tokens 2048` 控制模型调用。
- [`config.py`](release/server/config.py) 中的 `LLM_TIMEOUT = 30` 控制服务端请求超时。
- `ApiClient` 使用 30 秒客户端超时。如果提高其中一个，请同步提高另一个。

## API 参考

### `POST /api/v1/generate`

请求：

```json
{
  "prompt": "前视基准面拉伸200x100x20底座，四角各打一个直径10mm通孔"
}
```

成功——`200 OK`，返回特征树：

```json
{
  "model_name": "Mounting plate",
  "features": [
    {
      "feature_type": "extrude",
      "name": "Base",
      "params": {
        "plane": "前视基准面",
        "shape": "rectangle",
        "length": 200,
        "width": 100,
        "depth": 20
      }
    }
  ]
}
```

错误使用统一信封：

```json
{
  "message": "缺少必要字段: prompt",
  "status": "error"
}
```

| 状态码 | 触发条件 |
|---|---|
| `400` | 缺少或为空的 `prompt`，或没有有效的特征信封 |
| `503` | 大模型提供商不可达，或请求超时 |
| `500` | 大模型响应无法解析，或其他服务端未知错误 |

**安全提示。** 服务端没有内置鉴权、授权、限流或 CORS 策略。不要把服务端直接暴露到公网。请放在反向代理之后，增加鉴权和配额，并先关闭 Flask 调试模式。

## 项目结构

```text
Vulcan/
├── README.md                     # 英文（默认）
├── README.zh-CN.md               # 简体中文
├── README.fr.md                  # 法文
├── docs/
│   └── assets/                   # 三份 README 共用的矢量配图
│
├── beta/                         # 旧版代码生成路线
│   ├── client-csharp-beta/       #   早期命令组/功能区原型
│   ├── client-python-beta/       #   使用 exec() 的 PyQt5 客户端
│   └── server-python-beta/       #   FastAPI + AsyncOpenAI 服务端
│
└── release/                      # 当前 JSON 契约路线
    ├── client/
    │   └── Vulcan.SolidWorksClient/
    │       ├── Core/             #   SwAddIn——COM 注册与生命周期
    │       ├── Models/           #   ModelData——请求/响应类型
    │       ├── Services/         #   ApiClient、SwModeler、Logger
    │       ├── UI/               #   MainWindow（WPF，500 × 300）
    │       ├── ReferenceDLL/     #   SolidWorks 互操作程序集
    │       └── Vulcan Setup/     #   Inno Setup 安装包
    └── server/
        ├── app.py                #   Flask 入口
        ├── config.py             #   环境配置
        ├── .env.example          #   提供商与 Flask 配置模板
        ├── Dockerfile            #   python:3.9-slim 镜像
        ├── prompts/              #   版本化系统提示词
        ├── services/             #   大模型客户端
        └── utils/                #   日志与自定义异常
```

**仓库说明。** 项目文件实际编译 `Core/SwAddIn.cs`；根目录的 `VulcanApp.cs` 是较早的重复文件，不参与构建。`services/param_parser.py` 是遗留校验器，未接入 release 的 `app.py`；字段级默认值位于 `SwModeler`。

## 兼容性与当前范围

| 领域 | 状态 |
|---|---|
| SolidWorks | 基于 2025 互操作接口构建（`FeatureExtrusion3`、`FeatureCut4`）。旧版本可能需要调整参数或使用 UI 兜底，不在已测试矩阵内。 |
| 客户端平台 | 仅 Windows，x64，.NET Framework 4.8。 |
| 服务端平台 | Python 3.9–3.11；本地、Docker 或无头运行。 |
| 特征类型 | 仅 `extrude` 和 `cut`。release 客户端未实现旋转、扫描、放样、圆角、倒角。 |
| 基准面 | 建模器当前把草图归一化到前视基准面；基准面路由在路线图上。 |
| 校验 | 服务端检查响应信封；信封反序列化、参数转换和字段默认值位于 C# 客户端。 |
| 鉴权 | 未内置。请部署在可信网络内，或增加带鉴权和限流的代理。 |
| 大模型质量 | 延迟和正确性取决于提供商；建议使用指令遵循能力强的模型。 |

## 故障排除

| 现象 | 可能原因 | 处理方式 |
|---|---|---|
| `Vulcan AI` 标签页不出现 | 插件未注册，或构建为 x86 | 构建 x64 Release 配置，以管理员身份运行 `install.bat` 或 `RegAsm`，重启 SolidWorks |
| “目标计算机积极拒绝连接” | 服务端未运行，或 `_baseUrl` 错误 | 启动 `python app.py`；检查主机和端口；开放防火墙 |
| 没有活动文档 | SolidWorks 未打开零件 | 新建或打开零件文档后重试 |
| 草图出现但拉伸失败 | 互操作版本不匹配或 SolidWorks 状态异常 | 查看桌面 `Vulcan_Logs`；更新 `FeatureExtrusion3` 参数；生成时保持 SolidWorks 前台 |
| 大模型返回空或畸形响应 | 模型未遵守 JSON 契约 | 换用更强的模型，降低 `temperature`，检查 `vulcan_server.log` |
| 请求超时 | 模型耗时超过 30 秒 | 同时提高 `LLM_TIMEOUT` 和 `ApiClient` 超时 |
| 中文或法文需求理解偏差 | 模型缺乏多语言工程词汇 | 使用多语言模型；系统提示词为中文 |
| 构建时报 SolidWorks 引用错误 | `HintPath` 指向其他安装位置 | 更新引用，或使用 `ReferenceDLL/` 中版本匹配的程序集 |
| 安装包注册失败 | SolidWorks 正在运行，或终端未提权 | 关闭 SolidWorks，以管理员身份重新运行安装包 |
| 模型生成但孔位不对 | 基准面或坐标约定与需求不一致 | 检查返回的 JSON；注意建模器会归一化到前视基准面 |

服务端日志写入工作目录下的 `vulcan_server.log`。客户端日志写入桌面上的 `Vulcan_Logs/log_YYYYMMDD.txt`。

## 路线图

- [ ] 通用基准面路由，取代前视基准面归一化。
- [ ] 旋转、扫描、放样、圆角、倒角特征。
- [ ] 原生孔阵列、镜像、线性/圆周阵列。
- [ ] 原生英文和法文系统提示词，而不只依赖模型的多语言能力。
- [ ] 提供商适配器与流式响应。
- [ ] 鉴权、限流和按用户配额。
- [ ] 提示词回归测试与可在 CI 中运行的 COM 抽象层。
- [ ] 用任务窗格替代模态面板。
- [ ] 装配体级操作与工程图标注。

## 参与贡献

欢迎提交 Pull Request。重大改动请先开 Issue 讨论方案。

适合切入的方向：

- **特征：** 在 `SwModeler` 中增加新的特征类型和形状处理器。
- **提供商：** 进一步抽象 LLM 客户端，增加提供商适配器。
- **测试：** 构建提示词回归测试和可在 CI 中运行的 COM 抽象。
- **文档：** 完善三份 README，或补充本地化截图与示例。

提交 PR 前，请启动服务端、至少完整跑通一条提示词，并确保 `.env`、日志和构建产物没有进入提交。项目采用 GPLv3，贡献按同一许可证接受。

## 许可证

本项目遵循 **GNU General Public License v3.0** 分发。衍生作品必须同样以 GPLv3 开源。完整条款见 [LICENSE](LICENSE)。

---

<p align="center">
  <sub>由 <a href="https://github.com/Robusr">Robusr</a> 构建——因为学 SolidWorks 不应该需要一本 200 页的手册。</sub>
</p>
