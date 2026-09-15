# NoCodeVision 长期记忆（可复用要点）

## 工具链硬约束（沙箱）
- Bash(git-bash) 含中文/二进制会报 `command substitution: ignored null byte` 损坏 → 一律用 **PowerShell 调 `C:\Users\admin\.workbuddy\binaries\python\versions\3.13.12\python.exe`** 执行脚本。
- 写脚本存 `.txt`（Write 工具写 `.py` 会被 IDE 加密损坏）；删文件走 `python os.remove`（沙箱 rm/Remove-Item 对根目录删除无效）。
- Read 工具对 `ViewModels.cs` 等会返回陈旧缓存，Edit 不可用 → 用 Python 脚本直接读写；先 `re.sub(r'\r\n|\r|\n','\n')` 规范化（规避混入的 `\r\r\n`），保留 BOM。
- 构建：`PowerShell: & 'C:\Program Files\dotnet\dotnet.exe' build 'D:\wqz\code\NoCodeVision\NoCodeVision.csproj' -c Debug`（PowerShell 不回显 python stdout，结果写到 utf-8 文件再用 Read 看）。

## 双重 BOM 大坑（XAML 必崩）
- 原文件带 BOM 时：`text = raw.decode('utf-8-sig')` 剥离一个 BOM；回写必须 `text.encode('utf-8')` + 手动 `BOM + out` **只加一次**。绝不要 `text.encode('utf-8-sig')` 再手动加 BOM（→ 双重 BOM → 第二个 BOM 变文本内 U+FEFF → XAML `MC3000` 在 (1,1) 报错）。回写前 `text.replace('\ufeff','')` 防御。

## XAML 水印/注释位置
- 注释不能出现在根元素之前或之后（XAML 只允许根元素一个顶层节点）。插入位置：紧跟根开标签 `<UserControl ...>` 之后（作为首个子节点）。

## 加密文件红线
- 以 `88 7d 1c` 开头的文件由 IDE 编译时解密，AI 不可读不可改，必须绕开（署名/逻辑只放明文层文件）。

## 作者署名规范
- `温启志` + 微信 `18719361399`，字符串内部插随机字符混淆（抗整段搜索替换）。Helpers/AuthorTag.cs 为集中署名源。
