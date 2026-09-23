# V4 回收站数据准确性与清空复验报告

## 问题判定

旧实现只使用 `SHQueryRecycleBinW` 的载荷逻辑字节。例如一个逻辑大小为 2 KB 的文件，
其 `$R` 载荷和 `$I` 元数据可能分别占用一个 4 KB NTFS 簇，因此 Windows 系统界面显示
约 8 KB 磁盘占用。把两种口径都标成“垃圾大小”属于数据错误，不作为允许误差处理。

## V4 实现

- Windows Shell：权威项目数与载荷逻辑字节。
- NTFS：使用 `GetCompressedFileSizeW` 统计 `$R` 载荷及 `$I` 元数据的实际分配字节。
- 仅枚举当前用户 SID 的固定磁盘回收站；拒绝重解析点，逐位置记录不可访问数量。
- Smart Scan 的“预计释放”采用实际分配字节；同时保留逻辑大小、元数据、来源和不可访问数量。
- Shell 为零但仍存在孤留占用或不可访问位置时仍产生结果，不再静默遗漏。
- 清空后最多三次重新查询；项目数、逻辑字节和完整 NTFS 占用均通过才判定成功。
- 文件系统测量不完整时不伪报“双口径已验证”。

## 自动化证据

- 第一轮：5/5 通过，`artifacts/test-results/v4-recycle-accuracy-round1.trx`。
- 第二轮：7/7 通过，`artifacts/test-results/v4-recycle-accuracy-round2.trx`。
- 第二轮包含当前 Windows 实机的只读 Shell + NTFS 快照测试。
- Cleaner Release 构建：0 警告、0 错误；第二轮测试构建的全部产品项目亦成功。

## 仍需总体验收

最终全量两轮回归、UI 双口径展示以及管理员权限清理闭环由总控在所有并行模块收束后统一验证。
