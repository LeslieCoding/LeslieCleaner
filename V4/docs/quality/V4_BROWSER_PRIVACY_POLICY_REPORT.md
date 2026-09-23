# V4 浏览器精细隐私策略报告

日期：2026-08-17  
需求基线：`20_Privacy_Protection_Architecture.md`

## 实现

- Cookie 模式：Keep All、Tracking Only、Selected Domains、All Except Whitelist。
- Tracking Only 使用本地可审计域名规则和常见跟踪 Cookie 名称；域名匹配按 DNS 边界处理，不使用任意子串。
- 历史支持全部或任意 UTC 起止范围，可由 UI 映射为最近一小时、一天、一周、四周和自定义区间。
- Chromium 使用 1601 epoch 微秒，Firefox 使用 Unix epoch 微秒；删除条件参数化并在单一 SQLite 事务中执行。
- 任何写入前使用统一备份服务建立 Database 备份并验证完整性；操作失败按逆序恢复。
- 必须显式确认；安全门处于紧急只读时拒绝。
- Session Data 不会被静默删除；传入 IncludeSessionData 会明确拒绝，要求进入独立人工会话清理流程。
- 分析结果给出总 Cookie、跟踪 Cookie、历史数量、0–100 隐私分和基于真实计数的建议。
- 不读取或修改 Login Data、密码库、个人文档或消息内容。

## 验证

- Browser 模块 Release：0 warning / 0 error。
- 测试项目自身 Release：0 warning / 0 error。
- 聚焦测试：4/4 通过，TRX `v4-browser-privacy-policy-focused.trx`。
- 覆盖：域名/名称跟踪分类、登录 Cookie 保留、备份验证、Chromium 自定义时间范围、无确认拒绝、会话数据拒绝。

## 边界

- 本地跟踪规则不是在线广告情报服务；规则更新需后续通过签名静态数据包完成。
- 服务层已经支持时间范围和模式，UI 控件由 UI 验收循环接入。
- 本报告是聚焦验证；总解决方案双轮测试将在并行 P1 模块完成后重跑。
