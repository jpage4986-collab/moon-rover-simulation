# 月球车仿真平台协作说明

## 开始工作

1. 安装 Unity `2022.3.62f3c1`，用 Unity Hub 打开 `Assets (2)`。
2. 保留所有 `Assets`、`Packages`、`ProjectSettings` 和 `.meta` 文件。
3. 首次导入时等待 Unity 完成编译，再打开 `Assets/Scenes/SampleScene.unity`。
4. 先在无实体底座模式下验证场景和按钮，再连接中间件与硬件。

## 分支与提交

- `main`：可运行、已验证的版本。
- `feature/<简短主题>`：功能开发分支。
- 提交信息建议使用 `feat:`、`fix:`、`docs:`、`test:` 前缀。
- Unity 场景改动请同时提交相关 `.meta` 文件，并在 PR 中说明测试场景。

## 硬件与授权文件

仓库默认忽略 `Library`、运行日志和汇鼎/SafeNet 运行时二进制。请依据
`MoonBase/middleware/MIDDLEWARE_BINARIES.md` 在本机配置，不要把许可证、加密狗驱动或未确认可再分发的 DLL 推送到公共仓库。

## 提交前检查

- [ ] Unity Console 无新的编译错误。
- [ ] `SampleScene` 中 1/2/3 键与三个驾驶模式按钮均能切换状态。
- [ ] 无实体硬件时，平台通信状态符合预期，车辆仍可在禁用底座模式运行。
- [ ] 若做了实体平台测试，已执行回零并记录硬件、配置和结果。
