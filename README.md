# 衣物回收管理 PC 版

`衣物回收管理` 是一个面向 Windows 的本地桌面应用，聚焦衣物回收业务的日常记账、库存管理和经营统计。

如果你是技术小白，建议先看这份更容易上手的说明：

- [README_BEGINNER.md](./README_BEGINNER.md)
- [ClothingRecycler_QuickStart_v1.0.0.pdf](./output/pdf/ClothingRecycler_QuickStart_v1.0.0.pdf)

当前版本：`1.0.0`

技术栈：
- `WinUI 3`
- `C# / .NET 10`
- `SQLite` 本地数据库
- `Windows 本地桌面版（用户级安装 / 文件夹发布）`

## 应用截图

主界面示意：

![衣物回收管理主界面](./docs/screenshots/main-window.png)

如果需要直接发给最终使用者，也可以使用已经生成好的 PDF 手册：

- [ClothingRecycler_QuickStart_v1.0.0.pdf](./output/pdf/ClothingRecycler_QuickStart_v1.0.0.pdf)

## 一句话介绍

这是一套纯本地的衣物回收记账工具，核心目标是把 `入库`、`出库`、`库存`、`订单`、`客户`、`统计` 这些日常经营动作放到一台 Windows 电脑里完成，不依赖服务器，也不要求联网。

## 主要功能

### 1. 仪表盘
- 查看今日入库、今日出库、当前库存、预计收入等核心指标
- 快速了解分类数量、经营概览和重点提醒

### 2. 入库工作台
- 录入入库分类、数量、单价和客户
- 支持已有客户选择和新客户自动建档
- 支持按“客户 + 分类”带出最近成交价
- 入库后自动生成订单并更新库存

### 3. 出库工作台
- 录入出库分类、数量、单价和客户
- 支持库存校验，避免超量出库
- 支持客户价格记忆
- 出库后自动生成订单并扣减库存

### 4. 库存中心
- 查看全部分类库存
- 支持搜索、筛选、低库存提醒
- 支持手动库存调整
- 支持库存盘点和差异记录

### 5. 订单中心
- 查看最近订单和全部订单
- 支持入库 / 出库筛选
- 支持查看订单详情
- 支持删除订单、编辑订单并自动回滚相关派生数据

### 6. 客户中心
- 查看客户总览、交易次数和最近交易时间
- 支持新增、编辑客户资料
- 查看客户价格记忆和关联订单

### 7. 经营分析
- 查看累计入库、累计出库、估算毛利、客户贡献等统计
- 查看近 7 天经营趋势和分类排行

### 8. 预计收入
- 查看库存预计收入、库存成本和估算毛利
- 查看待补货分类和价格空间

### 9. 设置与维护
- 分类管理：新增、编辑、归档、删除风险拦截
- 本地数据库备份与恢复
- 经营数据导出 CSV
- 账本一致性检查
- 崩溃日志与运行日志查看

## 业务特点

- 支持多单位：`公斤 / 斤 / 件`
- 客户价格记忆
- 库存随入库、出库、盘点、调整自动联动
- 订单、库存、客户标记、价格记忆之间保持一致
- 纯本地运行，不依赖服务器

## 适合谁使用

- 需要在电脑上做本地记账的衣物回收从业者
- 不想依赖网页后台或云服务的小团队
- 希望把库存、订单、客户记录放在一台固定电脑中管理的场景

## 安装方式

### 方式一：安装版（推荐）

当前打包好的安装文件：

`D:\Desktop\ClothingRecycler_PC\artifacts\release\ClothingRecycler_PC_Install_v1.0.0.exe`

安装步骤：
1. 双击运行 `ClothingRecycler_PC_Install_v1.0.0.exe`
2. 如果 Windows 弹出安全提示，选择继续运行
3. 安装器会先让你选择安装目录
4. 默认推荐目录是：
   `C:\Users\<你的用户名>\AppData\Local\Programs\ClothingRecycler`
5. 你也可以改到其他磁盘或自定义文件夹
6. 安装完成后会自动创建：
   - 桌面快捷方式
   - 开始菜单快捷方式
   - 卸载入口

安装完成后可通过以下方式启动：
- 桌面快捷方式：`衣物回收管理`
- 开始菜单：`衣物回收管理`
- 安装目录中的 `ClothingRecycler.Desktop.exe`

### 方式二：文件夹便携版

如果不想安装，也可以直接使用发布目录：

`D:\Desktop\ClothingRecycler_PC\artifacts\release\ClothingRecycler_PC_v1.0.0_win-x64`

运行方式：
- 双击 `Run-ClothingRecycler.bat`
- 或直接运行 `ClothingRecycler.Desktop.exe`

如果你在发布目录里执行安装：
- 双击 `Install-ClothingRecycler.bat`
- 安装脚本同样支持你选择安装目录

## 卸载方式

可以通过以下任一方式卸载：
- 开始菜单中的 `卸载衣物回收管理`
- 安装目录中的 `Uninstall-ClothingRecycler.bat`

卸载行为：
- 删除程序文件
- 删除桌面快捷方式和开始菜单快捷方式
- 删除卸载注册信息

注意：
- 卸载不会删除你的业务数据
- 本地数据库、备份、导出和日志仍会保留在用户目录中

## 数据安全说明

### 1. 纯本地存储

应用当前不依赖云端服务。你的业务数据默认保存在当前 Windows 用户目录下，不会主动上传到外部服务器。

### 2. 卸载不会删账本

程序文件和业务数据是分开的。即使你卸载应用，数据库、备份、导出文件、日志仍然保留。

### 3. 支持手动备份与恢复

设置页已经提供：
- 一键创建本地备份
- 从备份恢复
- 打开备份目录

建议养成这个习惯：
1. 大批量改数据前先备份一次
2. 每周至少手动备份一次
3. 可以把 `backups` 目录额外复制到 U 盘或网盘

### 4. 内置一致性检查

设置页支持账本一致性检查，用来核对：
- 库存汇总是否正常
- 客户标记是否一致
- 价格记忆是否可追溯
- 订单汇总是否匹配

### 5. 日志可用于排查问题

如果应用异常退出或某些数据表现不正常，可以查看日志目录，便于后续诊断和恢复。

## 数据存储位置

应用数据默认保存在：

`%LOCALAPPDATA%\ClothingRecycler`

主要目录包括：
- 数据库：`%LOCALAPPDATA%\ClothingRecycler\clothingrecycler.db`
- 备份目录：`%LOCALAPPDATA%\ClothingRecycler\backups`
- 导出目录：`%LOCALAPPDATA%\ClothingRecycler\exports`
- 日志目录：`%LOCALAPPDATA%\ClothingRecycler\logs`

这意味着：
- 卸载程序后数据仍然保留
- 重装程序不会覆盖已有本地账本

## 常见问题

### 1. 安装后打不开怎么办？

先确认系统是 `Windows 10/11 64 位`。如果双击没有反应，可以尝试：
- 右键“以管理员身份运行”安装包
- 检查是否被杀毒软件拦截
- 去 `%LOCALAPPDATA%\ClothingRecycler\logs` 看最近日志

### 2. 卸载后数据会不会没了？

不会。卸载只删除程序文件，不删除你的数据库、备份和导出文件。

### 3. 换电脑能不能继续用？

可以，但当前是纯本地版。你需要把下面这些内容一起带走：
- 数据库文件
- 备份目录
- 导出目录

更稳妥的方式是先在旧电脑做一次备份，再把备份文件复制到新电脑恢复。

### 4. 这版能不能和 Android 实时同步？

目前还不支持。当前版本优先保证 `Windows 本地版` 稳定可用，后续再考虑 PC 和 Android 的互通方案。

### 5. 升级到新版本会不会覆盖我的数据？

正常不会。程序文件和数据目录是分开的，但升级前仍建议先手动备份一次。

### 6. 如果录错订单怎么办？

可以到 `订单中心` 找到对应订单，然后执行编辑或删除。系统会同步回滚相关库存和派生数据。

## 给使用者的建议流程

如果你是第一次使用，建议按这个顺序：
1. 先去 `设置` 建好衣物分类
2. 再录入第一批 `入库`
3. 有销售或流出时再录 `出库`
4. 每周做一次 `备份`
5. 定期看一下 `库存中心` 和 `经营分析`

## 开发与调试

项目目录：

`D:\Desktop\ClothingRecycler_PC`

常用命令：

```powershell
dotnet build D:\Desktop\ClothingRecycler_PC\ClothingRecycler.Desktop.csproj -p:Platform=x64
dotnet test D:\Desktop\ClothingRecycler_PC\ClothingRecycler.Desktop.Tests\ClothingRecycler.Desktop.Tests.csproj
```

调试运行入口：

`D:\Desktop\ClothingRecycler_PC\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\ClothingRecycler.Desktop.exe`

## 发布方式

发布脚本：

`D:\Desktop\ClothingRecycler_PC\scripts\publish-release.ps1`

示例命令：

```powershell
powershell -ExecutionPolicy Bypass -File D:\Desktop\ClothingRecycler_PC\scripts\publish-release.ps1 -Runtime win-x64 -Configuration Release -Version 1.0.0
```

运行后会生成：
- 文件夹发布包：
  `D:\Desktop\ClothingRecycler_PC\artifacts\release\ClothingRecycler_PC_v1.0.0_win-x64`
- 压缩包：
  `D:\Desktop\ClothingRecycler_PC\artifacts\release\ClothingRecycler_PC_v1.0.0_win-x64.zip`
- 单文件安装器：
  `D:\Desktop\ClothingRecycler_PC\artifacts\release\ClothingRecycler_PC_Install_v1.0.0.exe`

## 当前状态

当前版本已经完成：
- 主业务链路
- 本地数据库迁移
- 崩溃日志
- 账本一致性检查
- 备份恢复
- CSV 导出
- 用户级安装、卸载和快捷方式创建

当前更适合的定位是：
- 单机本地使用
- Windows 桌面端交付
- 不依赖联网的日常经营管理工具

## 后续方向

未来如果要继续扩展，比较合理的方向是：
- Android 与 PC 的数据互通
- 更正式的签名和安装器品牌化
- 多端同步或局域网共享
