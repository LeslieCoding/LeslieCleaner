# 安装 LeslieCleaner V3

## 推荐方式：安装包

1. 打开 `installer\output\LeslieCleanerSetup-3.0.0.exe`。
2. 在安装向导中选择安装位置并完成安装。
3. 从开始菜单的 **LeslieCleaner** 启动程序。
4. 在需要清空受保护回收站、修改受保护注册表键或管理系统服务时，以管理员身份启动程序。

安装包为 Windows x64 自包含版本，不需要另行安装 .NET Desktop Runtime。

安装包 SHA-256：

```text
B9217D137382B0BE3C3C7847BC19E3A44ED7B6430EE2A63EA0C6240D514A7C15
```

## 免安装方式

保留 `installer\publish` 下的所有文件和子目录，然后运行：

```text
installer\publish\LeslieCleaner.App.exe
```

不要只复制单个 `.exe` 文件；该目录包含应用程序集和自包含运行时。

## 卸载

在 Windows 的“已安装的应用”中卸载 **LeslieCleaner 3.0.0**，或运行安装目录内的 `unins000.exe`。
