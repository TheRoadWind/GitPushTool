Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Text
Imports System.Threading.Tasks
Imports System.Windows.Forms
Imports System.Net.Http
Imports System.Net.Http.Headers

Public Class MainForm

    ' ========== 控件字段 ==========
    Private lblProject As Label
    Private txtProject As TextBox
    Private btnBrowse As Button
    Private lblRepo As Label
    Private txtRepo As TextBox
    Private lblUser As Label
    Private txtUser As TextBox
    Private lblEmail As Label
    Private txtEmail As TextBox
    Private lblToken As Label
    Private txtToken As TextBox
    Private lblMsg As Label
    Private txtMsg As TextBox
    Private lblGit As Label
    Private txtGit As TextBox
    Private chkInit As CheckBox
    Private chkIgnore As CheckBox
    Private chkReadme As CheckBox
    Private btnPush As Button
    Private btnManage As Button
    Private txtLog As TextBox
    Private chkCreateRepo As CheckBox

    ' 状态
    Private detectedGit As String = ""
    Private gitHubUser As String = "TheRoadWind"
    Private repoBase As String = "https://github.com/TheRoadWind"

    ' 配置文件路径：%APPDATA%\GitPushTool\config.ini
    Private ReadOnly configDir As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitPushTool")
    Private ReadOnly configFile As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitPushTool", "config.ini")

    ' ========== 构造函数：动态生成全部控件 ==========
    Public Sub New()
        Me.Text = "Git 一键推送工具"
        Me.ClientSize = New Size(600, 590)
        Me.AllowDrop = True
        Me.StartPosition = FormStartPosition.CenterScreen

        ' ---- 工厂 ----
        Dim NewLabel = Function(parent As Control, name As String, text As String, x As Integer, y As Integer) As Label
                           Dim l As New Label() With {.Text = text, .Location = New Point(x, y), .AutoSize = True}
                           If Not String.IsNullOrEmpty(name) Then l.Name = name
                           parent.Controls.Add(l)
                           Return l
                       End Function

        Dim NewTextBox = Function(parent As Control, name As String, text As String, x As Integer, y As Integer, w As Integer) As TextBox
                             Dim t As New TextBox() With {.Text = text, .Location = New Point(x, y), .Size = New Size(w, 23)}
                             If Not String.IsNullOrEmpty(name) Then t.Name = name
                             parent.Controls.Add(t)
                             Return t
                         End Function

        Dim NewButton = Function(parent As Control, name As String, text As String, x As Integer, y As Integer, w As Integer, h As Integer) As Button
                            Dim b As New Button() With {.Text = text, .Location = New Point(x, y), .Size = New Size(w, h), .UseVisualStyleBackColor = True}
                            If Not String.IsNullOrEmpty(name) Then b.Name = name
                            parent.Controls.Add(b)
                            Return b
                        End Function

        Dim NewCheck = Function(parent As Control, name As String, text As String, x As Integer, y As Integer, checked As Boolean) As CheckBox
                           Dim c As New CheckBox() With {.Text = text, .Location = New Point(x, y), .AutoSize = True, .Checked = checked}
                           If Not String.IsNullOrEmpty(name) Then c.Name = name
                           parent.Controls.Add(c)
                           Return c
                       End Function

        ' ---- 创建控件 ----
        lblProject = NewLabel(Me, "lblProject", "项目路径：", 12, 15)
        txtProject = NewTextBox(Me, "txtProject", "", 90, 12, 380)
        btnBrowse = NewButton(Me, "btnBrowse", "浏览...", 480, 11, 75, 25)

        lblRepo = NewLabel(Me, "lblRepo", "仓库地址：", 12, 50)
        txtRepo = NewTextBox(Me, "txtRepo", "", 90, 47, 465)

        lblUser = NewLabel(Me, "lblUser", "用户名：", 12, 85)
        txtUser = NewTextBox(Me, "txtUser", gitHubUser, 90, 82, 180)

        lblEmail = NewLabel(Me, "lblEmail", "邮箱：", 290, 85)
        txtEmail = NewTextBox(Me, "txtEmail", "", 340, 82, 215)

        lblToken = NewLabel(Me, "lblToken", "PAT令牌：", 12, 120)
        txtToken = NewTextBox(Me, "txtToken", "", 90, 117, 465)
        txtToken.UseSystemPasswordChar = True

        lblMsg = NewLabel(Me, "lblMsg", "提交信息：", 12, 155)
        txtMsg = NewTextBox(Me, "txtMsg", "Initial commit", 90, 152, 465)

        lblGit = NewLabel(Me, "lblGit", "Git 路径：", 12, 190)
        txtGit = NewTextBox(Me, "txtGit", "", 90, 187, 465)
        txtGit.ReadOnly = True
        txtGit.BackColor = Color.WhiteSmoke

        chkInit = NewCheck(Me, "chkInit", "需要时自动 git init", 90, 220, True)
        chkIgnore = NewCheck(Me, "chkIgnore", "自动生成 .gitignore", 230, 220, True)
        chkReadme = NewCheck(Me, "chkReadme", "自动生成 README.md", 380, 220, True)
        chkCreateRepo = NewCheck(Me, "chkCreateRepo", "自动创建远程仓库", 90, 245, True)

        btnPush = NewButton(Me, "btnPush", "创建并推送", 400, 245, 155, 30)
        btnManage = NewButton(Me, "btnManage", "管理仓库", 250, 250, 140, 30)
        txtLog = New TextBox() With {
            .Location = New Point(12, 285),
            .Size = New Size(543, 230),
            .Multiline = True,
            .ScrollBars = ScrollBars.Vertical,
            .Font = New Font("Consolas", 9.0F),
            .ReadOnly = True,
            .BackColor = Color.Black,
            .ForeColor = Color.LightGreen
        }
        txtLog.Name = "txtLog"
        Me.Controls.Add(txtLog)

        ' ---- 事件 ----
        AddHandler btnBrowse.Click, AddressOf btnBrowse_Click
        AddHandler btnPush.Click, AddressOf btnPush_Click
        AddHandler txtProject.TextChanged, AddressOf txtProject_TextChanged
        AddHandler Me.DragEnter, AddressOf MainForm_DragEnter
        AddHandler Me.DragDrop, AddressOf MainForm_DragDrop
        AddHandler Me.FormClosing, AddressOf MainForm_FormClosing
        AddHandler btnManage.Click, AddressOf btnManage_Click
        ' 初始化：读配置 + 找 git
        LoadConfig()
        detectedGit = DetectGit("")
        txtGit.Text = If(detectedGit <> "", detectedGit, "(未找到 git.exe)")
    End Sub

    ' ========== 拖拽 ==========
    Private Sub MainForm_DragEnter(sender As Object, e As DragEventArgs)
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim paths = CType(e.Data.GetData(DataFormats.FileDrop), String())
            If paths IsNot Nothing AndAlso paths.Length > 0 AndAlso Directory.Exists(paths(0)) Then
                e.Effect = DragDropEffects.Copy
                Return
            End If
        End If
        e.Effect = DragDropEffects.None
    End Sub

    Private Sub MainForm_DragDrop(sender As Object, e As DragEventArgs)
        Dim paths = CType(e.Data.GetData(DataFormats.FileDrop), String())
        If paths Is Nothing OrElse paths.Length = 0 Then Return
        If Not Directory.Exists(paths(0)) Then Return
        txtProject.Text = paths(0)
    End Sub

    ' 项目路径变化 -> 自动拼仓库地址
    Private Sub txtProject_TextChanged(sender As Object, e As EventArgs)
        Dim dir = txtProject.Text.Trim()
        If dir = "" Then Return
        Try
            Dim name = New DirectoryInfo(dir).Name
            If name <> "" Then
                txtRepo.Text = $"{repoBase}/{name}.git"
            End If
        Catch
        End Try
    End Sub

    ' ========== 浏览 ==========
    Private Sub btnBrowse_Click(sender As Object, e As EventArgs)
        Using fbd As New FolderBrowserDialog()
            fbd.Description = "选择项目目录"
            If fbd.ShowDialog() = DialogResult.OK Then
                txtProject.Text = fbd.SelectedPath
            End If
        End Using
    End Sub

    ' ========== 自动探测 git.exe（不依赖项目路径） ==========
    Private Function DetectGit(projectDir As String) As String
        ' 1) 项目目录及其向上的 PortableGit
        If projectDir <> "" AndAlso Directory.Exists(projectDir) Then
            Dim candidates As New List(Of String)
            candidates.Add(Path.Combine(projectDir, "git.exe"))
            candidates.Add(Path.Combine(projectDir, "PortableGit", "bin", "git.exe"))
            candidates.Add(Path.Combine(projectDir, "PortableGit", "cmd", "git.exe"))

            Dim dir As New DirectoryInfo(projectDir)
            For i = 0 To 3
                If dir Is Nothing Then Exit For
                candidates.Add(Path.Combine(dir.FullName, "PortableGit", "bin", "git.exe"))
                candidates.Add(Path.Combine(dir.FullName, "PortableGit", "cmd", "git.exe"))
                dir = dir.Parent
            Next
            For Each c In candidates
                If File.Exists(c) Then Return c
            Next
        End If

        ' 2) 系统常见安装位置
        Dim sysPaths = {
            "C:\Program Files\Git\bin\git.exe",
            "C:\Program Files\Git\cmd\git.exe",
            "C:\Program Files (x86)\Git\bin\git.exe",
            "C:\Program Files (x86)\Git\cmd\git.exe"
        }
        For Each p In sysPaths
            If File.Exists(p) Then Return p
        Next

        ' 3) PATH 中查找
        Try
            Dim psi As New ProcessStartInfo("where", "git") With {.RedirectStandardOutput = True, .UseShellExecute = False, .CreateNoWindow = True}
            Using p As Process = Process.Start(psi)
                Dim output = p.StandardOutput.ReadToEnd()
                p.WaitForExit()
                Dim first = output.Split({ControlChars.Cr, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()
                If Not String.IsNullOrWhiteSpace(first) AndAlso File.Exists(first.Trim()) Then
                    Return first.Trim()
                End If
            End Using
        Catch
        End Try

        Return ""
    End Function

    ' ========== 配置读写 ==========
    Private Sub LoadConfig()
        Try
            If Not File.Exists(configFile) Then Return
            For Each line In File.ReadAllLines(configFile, Encoding.UTF8)
                If String.IsNullOrWhiteSpace(line) OrElse line.StartsWith("#") Then Continue For
                Dim idx = line.IndexOf("="c)
                If idx <= 0 Then Continue For
                Dim k = line.Substring(0, idx).Trim()
                Dim v = line.Substring(idx + 1).Trim()
                Select Case k
                    Case "user" : txtUser.Text = v
                    Case "email" : txtEmail.Text = v
                    Case "token" : txtToken.Text = v
                    Case "git" : If File.Exists(v) Then detectedGit = v : txtGit.Text = v
                End Select
            Next
        Catch ex As Exception
            Log("读取配置失败：" & ex.Message)
        End Try
    End Sub

    Private Sub SaveConfig()
        Try
            If Not Directory.Exists(configDir) Then Directory.CreateDirectory(configDir) '| Out-Null
            Dim sb As New StringBuilder()
            sb.AppendLine("# GitPushTool 配置（自动生成，可手动编辑）")
            sb.AppendLine("user=" & txtUser.Text.Trim())
            sb.AppendLine("email=" & txtEmail.Text.Trim())
            sb.AppendLine("token=" & txtToken.Text.Trim())
            If detectedGit <> "" Then sb.AppendLine("git=" & detectedGit)
            File.WriteAllText(configFile, sb.ToString(), New UTF8Encoding(False))
        Catch ex As Exception
            Log("保存配置失败：" & ex.Message)
        End Try
    End Sub

    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs)
        SaveConfig()
    End Sub

    ' ========== 主流程 ==========
    Private Async Sub btnPush_Click(sender As Object, e As EventArgs)
        Dim projectPath = txtProject.Text.Trim()
        Dim repoUrl = txtRepo.Text.Trim()
        Dim userName = txtUser.Text.Trim()
        Dim userEmail = txtEmail.Text.Trim()
        Dim token = txtToken.Text.Trim()
        Dim commitMsg = txtMsg.Text.Trim()
        Dim gitExe = If(detectedGit <> "", detectedGit, "git")

        If projectPath = "" OrElse Not Directory.Exists(projectPath) Then
            MessageBox.Show("项目路径无效！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If
        If repoUrl = "" Then
            MessageBox.Show("仓库地址为空！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If
        If commitMsg = "" Then commitMsg = "Initial commit"

        ' 保存一次配置
        SaveConfig()

        btnPush.Enabled = False
        txtLog.Clear()
        Log("==> 使用 git：" & gitExe)

        Try
            Await RunGitAsync(gitExe, projectPath, "--version")

            If chkInit.Checked AndAlso Not Directory.Exists(Path.Combine(projectPath, ".git")) Then
                Log("==> git init")
                Await RunGitAsync(gitExe, projectPath, "init")
            Else
                Log("==> 已存在 .git，跳过 init")
            End If

            If userName <> "" Then Await RunGitAsync(gitExe, projectPath, $"config user.name ""{userName}""")
            If userEmail <> "" Then Await RunGitAsync(gitExe, projectPath, $"config user.email ""{userEmail}""")

            If chkIgnore.Checked Then
                Dim ignorePath = Path.Combine(projectPath, ".gitignore")
                If Not File.Exists(ignorePath) Then
                    Log("==> 生成 .gitignore")
                    File.WriteAllText(ignorePath, DefaultGitIgnore(), New UTF8Encoding(False))
                Else
                    Log("==> .gitignore 已存在，跳过")
                End If
            End If

            If chkReadme.Checked Then
                Dim readmePath = Path.Combine(projectPath, "README.md")
                If Not File.Exists(readmePath) Then
                    Log("==> 生成 README.md")
                    Dim projName = New DirectoryInfo(projectPath).Name
                    File.WriteAllText(readmePath, DefaultReadme(projName), New UTF8Encoding(False))
                Else
                    Log("==> README.md 已存在，跳过")
                End If
            End If
            ' === 自动创建远程仓库 ===
            If chkCreateRepo.Checked Then
                If token = "" Then
                    Log("==> [跳过建仓库] 未填写 PAT")
                Else
                    Dim repoName = GetRepoNameFromUrl(repoUrl)
                    If repoName = "" Then
                        Log("==> [跳过建仓库] 无法解析仓库名")
                    Else
                        Log($"==> 检查/创建远程仓库：{gitHubUser}/{repoName}")
                        Dim r = Await EnsureGitHubRepoAsync(gitHubUser, repoName, token)
                        Select Case r
                            Case RepoResult.Created : Log("    已创建")
                            Case RepoResult.Exists : Log("    已存在，跳过")
                            Case Else : Log("    创建失败（继续尝试推送）")
                        End Select
                    End If
                End If
            End If
            ' 远程：带 token 的 URL（如果填了 token）
            Dim pushUrl = repoUrl
            If token <> "" Then
                pushUrl = InjectToken(repoUrl, userName, token)
                Log("==> 使用带 PAT 的远程地址（日志中已隐藏）")
            End If

            Log("==> 配置远程 origin")
            Await RunGitAsync(gitExe, projectPath, "remote remove origin", ignoreError:=True)
            Await RunGitAsync(gitExe, projectPath, $"remote add origin ""{pushUrl}""")

            Log("==> git add .")
            Await RunGitAsync(gitExe, projectPath, "add .")

            Log("==> git commit")
            Dim finalMsg = If(commitMsg = "", "Initial commit", commitMsg)
            ' 如果项目已存在 .git（说明不是首次），自动追加时间戳
            If Directory.Exists(Path.Combine(projectPath, ".git")) AndAlso File.Exists(Path.Combine(projectPath, ".git", "HEAD")) Then
                ' 判断是否已有提交：用 git log 判断（略），简单做法是直接加时间
                ' 这里保持原样，只把用户没改过的 "Initial commit" 变一下
                If finalMsg = "Initial commit" Then
                    finalMsg = "Update " & DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                End If
            End If
            Await RunGitAsync(gitExe, projectPath, $"commit -m ""{finalMsg}""", ignoreError:=True)

            Log("==> git branch -M main")
            Await RunGitAsync(gitExe, projectPath, "branch -M main")

            Log("==> git push -u origin main")
            Await RunGitAsync(gitExe, projectPath, "push -u origin main")

            ' 推送完把远程地址恢复成干净版，避免 token 留在 .git/config
            If token <> "" Then
                Await RunGitAsync(gitExe, projectPath, $"remote set-url origin ""{repoUrl}""")
            End If

            Log(vbCrLf & "===== 推送完成 =====")
            MessageBox.Show("推送成功！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            Log(vbCrLf & "[错误] " & ex.Message)
            MessageBox.Show("发生错误：" & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            btnPush.Enabled = True
        End Try
    End Sub
    ' ========== 从 URL 解析仓库名 ==========
    Private Function GetRepoNameFromUrl(url As String) As String
        Try
            ' https://github.com/TheRoadWind/xxx.git
            Dim u = url.TrimEnd("/"c)
            If u.EndsWith(".git", StringComparison.OrdinalIgnoreCase) Then
                u = u.Substring(0, u.Length - 4)
            End If
            Dim parts = u.Split("/"c)
            If parts.Length >= 2 Then
                Return parts(parts.Length - 1)
            End If
        Catch
        End Try
        Return ""
    End Function

    ' ========== 仓库创建结果 ==========
    Private Enum RepoResult
        Created
        Exists
        Failed
    End Enum

    ' ========== 调 GitHub API 创建仓库 ==========
    Private Async Function EnsureGitHubRepoAsync(owner As String, repoName As String, token As String,
                                              Optional userName As String = "",
                                              Optional userEmail As String = "") As Task(Of RepoResult)
        Try
            Using client As New HttpClient()
                client.Timeout = TimeSpan.FromSeconds(20)
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GitPushTool/1.0")
                client.DefaultRequestHeaders.Authorization =
                New AuthenticationHeaderValue("token", token)
                client.DefaultRequestHeaders.Accept.Add(
                New MediaTypeWithQualityHeaderValue("application/vnd.github+json"))

                ' 先查仓库是否已存在
                Dim checkResp = Await client.GetAsync($"https://api.github.com/repos/{owner}/{repoName}")
                If checkResp.IsSuccessStatusCode Then
                    Return RepoResult.Exists
                End If

                ' 组装 JSON（不引 Newtonsoft，手写就行）
                Dim json As New StringBuilder()
                json.Append("{")
                json.Append("""name"":""" & EscapeJson(repoName) & """,")
                json.Append("""private"":false,")
                json.Append("""auto_init"":false,")
                json.Append("""has_issues"":true,")
                json.Append("""has_wiki"":false,")
                json.Append("""has_projects"":false")
                json.Append("}")

                Dim content = New StringContent(json.ToString(), Encoding.UTF8, "application/json")
                Dim resp = Await client.PostAsync("https://api.github.com/user/repos", content)

                If resp.IsSuccessStatusCode Then
                    Return RepoResult.Created
                ElseIf CInt(resp.StatusCode) = 422 Then
                    ' 422 = 已存在或名字非法
                    Return RepoResult.Exists
                Else
                    Dim body = Await resp.Content.ReadAsStringAsync()
                    Log("    API 返回：" & CInt(resp.StatusCode).ToString() & " " & body)
                    Return RepoResult.Failed
                End If
            End Using
        Catch ex As Exception
            Log("    建仓库异常：" & ex.Message)
            Return RepoResult.Failed
        End Try
    End Function

    ' ========== JSON 字符串转义 ==========
    Private Function EscapeJson(s As String) As String
        If s Is Nothing Then Return ""
        Return s.Replace("\", "\\").Replace("""", "\""").Replace(vbCr, "\r").Replace(vbLf, "\n").Replace(vbTab, "\t")
    End Function
    ' 把 https://github.com/user/repo.git 变成 https://user:token@github.com/user/repo.git
    Private Function InjectToken(repoUrl As String, user As String, token As String) As String
        If Not repoUrl.StartsWith("https://") Then Return repoUrl
        Dim rest = repoUrl.Substring("https://".Length)
        Return $"https://{user}:{token}@{rest}"
    End Function

    ' ========== 执行 git ==========
    Private Function RunGitAsync(gitExe As String, workDir As String, args As String, Optional ignoreError As Boolean = False) As Task
        Return Task.Run(Sub()
                            Dim psi As New ProcessStartInfo() With {
                                .FileName = gitExe,
                                .Arguments = args,
                                .WorkingDirectory = workDir,
                                .RedirectStandardOutput = True,
                                .RedirectStandardError = True,
                                .UseShellExecute = False,
                                .CreateNoWindow = True,
                                .StandardOutputEncoding = Encoding.UTF8,
                                .StandardErrorEncoding = Encoding.UTF8
                            }
                            Using p As Process = Process.Start(psi)
                                Dim outText = p.StandardOutput.ReadToEnd()
                                Dim errText = p.StandardError.ReadToEnd()
                                p.WaitForExit()
                                If Not String.IsNullOrWhiteSpace(outText) Then Log(outText.TrimEnd())
                                If Not String.IsNullOrWhiteSpace(errText) Then Log("[stderr] " & errText.TrimEnd())
                                If p.ExitCode <> 0 AndAlso Not ignoreError Then
                                    Throw New Exception($"git {args} 退出码 {p.ExitCode}")
                                End If
                            End Using
                        End Sub)
    End Function

    ' ========== 日志 ==========
    Private Sub Log(text As String)
        If txtLog.InvokeRequired Then
            txtLog.Invoke(Sub() Log(text))
            Return
        End If
        txtLog.AppendText(text & Environment.NewLine)
    End Sub

    ' ========== 模板 ==========
    Private Function DefaultGitIgnore() As String
        Return "*.vbproj.user" & vbCrLf &
               "bin/" & vbCrLf &
               "obj/" & vbCrLf &
               "Debug/" & vbCrLf &
               "Release/" & vbCrLf &
               ".vs/" & vbCrLf &
               "*.suo" & vbCrLf &
               "*.user" & vbCrLf &
               "Thumbs.db" & vbCrLf &
               "Desktop.ini" & vbCrLf
    End Function

    Private Function DefaultReadme(projName As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("# " & projName)
        sb.AppendLine()
        sb.AppendLine("> 由 [GitPushTool](https://github.com/TheRoadWind) 自动生成")
        sb.AppendLine()
        sb.AppendLine("---")
        sb.AppendLine()
        sb.AppendLine("## 📖 简介")
        sb.AppendLine()
        sb.AppendLine("（在此补充项目的功能简介、用途、背景）")
        sb.AppendLine()
        sb.AppendLine("## ✨ 特性")
        sb.AppendLine()
        sb.AppendLine("- 特性一")
        sb.AppendLine("- 特性二")
        sb.AppendLine("- 特性三")
        sb.AppendLine()
        sb.AppendLine("## 🛠️ 环境要求")
        sb.AppendLine()
        sb.AppendLine("- 操作系统：Windows 10 / 11")
        sb.AppendLine("- 运行时：.NET Framework 4.8 / .NET 6+ / Python 3.x（按需修改）")
        sb.AppendLine("- 其他依赖：见 `requirements.txt` 或 `packages.config`")
        sb.AppendLine()
        sb.AppendLine("## 🚀 快速开始")
        sb.AppendLine()
        sb.AppendLine("### 1. 克隆仓库")
        sb.AppendLine()
        sb.AppendLine("```bash")
        sb.AppendLine("git clone https://github.com/TheRoadWind/" & projName & ".git")
        sb.AppendLine("cd " & projName)
        sb.AppendLine("```")
        sb.AppendLine()
        sb.AppendLine("### 2. 安装依赖")
        sb.AppendLine()
        sb.AppendLine("```bash")
        sb.AppendLine("# Python 示例")
        sb.AppendLine("pip install -r requirements.txt")
        sb.AppendLine()
        sb.AppendLine("# .NET 示例")
        sb.AppendLine("nuget restore " & projName & ".sln")
        sb.AppendLine("```")
        sb.AppendLine()
        sb.AppendLine("### 3. 运行")
        sb.AppendLine()
        sb.AppendLine("```bash")
        sb.AppendLine("# Python 示例")
        sb.AppendLine("python main.py")
        sb.AppendLine()
        sb.AppendLine("# .NET 示例")
        sb.AppendLine("dotnet run  # 或直接双击 " & projName & ".sln 用 VS 打开")
        sb.AppendLine("```")
        sb.AppendLine()
        sb.AppendLine("## 📁 目录结构")
        sb.AppendLine()
        sb.AppendLine("```")
        sb.AppendLine(projName & "/")
        sb.AppendLine("├── .vb/                # 源代码")
        sb.AppendLine("├── .sln/               # 解决方案")
        sb.AppendLine("├── .vbproj,.csproj/              # 项目")
        sb.AppendLine("├── .gitignore")
        sb.AppendLine("└── README.md")
        sb.AppendLine("```")
        sb.AppendLine()
        sb.AppendLine("## 📸 截图")
        sb.AppendLine()
        sb.AppendLine("（在此粘贴运行截图或演示 GIF）")
        sb.AppendLine()
        sb.AppendLine("## 📝 更新日志")
        sb.AppendLine()
        sb.AppendLine("### v1.0.0 (" & DateTime.Now.ToString("yyyy-MM-dd") & ")")
        sb.AppendLine()
        sb.AppendLine("- 初始版本")
        sb.AppendLine()
        sb.AppendLine("## 🤝 贡献")
        sb.AppendLine()
        sb.AppendLine("欢迎提交 Issue 和 Pull Request。")
        sb.AppendLine()
        sb.AppendLine("1. Fork 本仓库")
        sb.AppendLine("2. 新建分支 `git checkout -b feature/xxx`")
        sb.AppendLine("3. 提交改动 `git commit -m 'feat: xxx'`")
        sb.AppendLine("4. 推送分支 `git push origin feature/xxx`")
        sb.AppendLine("5. 提交 Pull Request")
        sb.AppendLine()
        sb.AppendLine("## 📄 License")
        sb.AppendLine()
        sb.AppendLine("本项目基于 [MIT](LICENSE) 协议开源。")
        sb.AppendLine()
        sb.AppendLine("---")
        sb.AppendLine()
        sb.AppendLine("_Generated by GitPushTool on " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & "_")
        Return sb.ToString()
    End Function

    ' ====================================================================
    ' ==================== 仓库管理弹窗（内嵌，不新建文件） ================
    ' ====================================================================
    Private Sub btnManage_Click(sender As Object, e As EventArgs)
        Dim token = txtToken.Text.Trim()
        Dim userName = txtUser.Text.Trim()
        If token = "" Then
            MessageBox.Show("请先填写 PAT 令牌！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If userName = "" Then
            MessageBox.Show("请先填写用户名！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        ShowRepoManagerDialog(token, userName)
    End Sub

    Private Sub ShowRepoManagerDialog(token As String, owner As String)
        Dim dlg As New Form() With {
            .Text = "仓库管理器 - " & owner,
            .ClientSize = New Size(640, 480),
            .StartPosition = FormStartPosition.CenterParent,
            .MinimumSize = New Size(560, 400),
            .ShowIcon = False,
            .MaximizeBox = False
        }

        ' ---- 顶部 ----
        Dim lblSearch As New Label() With {.Text = "搜索：", .Location = New Point(12, 15), .AutoSize = True}
        dlg.Controls.Add(lblSearch)

        Dim txtSearch As New TextBox() With {.Location = New Point(60, 12), .Size = New Size(360, 23)}
        dlg.Controls.Add(txtSearch)

        Dim btnRefresh As New Button() With {
            .Text = "刷新",
            .Location = New Point(430, 11),
            .Size = New Size(80, 25),
            .UseVisualStyleBackColor = True
        }
        dlg.Controls.Add(btnRefresh)

        Dim lblStatus As New Label() With {
            .Text = "加载中...",
            .Location = New Point(520, 15),
            .AutoSize = True,
            .ForeColor = Color.Gray
        }
        dlg.Controls.Add(lblStatus)

        ' ---- 列表 ----
        Dim lstRepos As New CheckedListBox() With {
            .Location = New Point(12, 45),
            .Size = New Size(616, 350),
            .CheckOnClick = True,
            .Font = New Font("Consolas", 9.5F),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right Or AnchorStyles.Bottom
        }
        dlg.Controls.Add(lstRepos)

        ' ---- 底部按钮 ----
        Dim btnSelectAll As New Button() With {
            .Text = "全选",
            .Location = New Point(12, 408),
            .Size = New Size(80, 30),
            .UseVisualStyleBackColor = True,
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        }
        dlg.Controls.Add(btnSelectAll)

        Dim btnInvert As New Button() With {
            .Text = "反选",
            .Location = New Point(100, 408),
            .Size = New Size(80, 30),
            .UseVisualStyleBackColor = True,
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left
        }
        dlg.Controls.Add(btnInvert)

        Dim btnDelete As New Button() With {
            .Text = "删除选中",
            .Location = New Point(430, 408),
            .Size = New Size(100, 30),
            .BackColor = Color.IndianRed,
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        }
        btnDelete.FlatAppearance.BorderSize = 0
        dlg.Controls.Add(btnDelete)

        Dim btnClose As New Button() With {
            .Text = "关闭",
            .Location = New Point(540, 408),
            .Size = New Size(88, 30),
            .UseVisualStyleBackColor = True,
            .Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        }
        dlg.Controls.Add(btnClose)

        ' ---- 数据 ----
        Dim allRepos As New List(Of RepoInfo)()

        ' ---- 内部：过滤 ----
        Dim ApplyFilter As Action =
            Sub()
                Dim kw = txtSearch.Text.Trim().ToLowerInvariant()
                lstRepos.Items.Clear()
                For Each r In allRepos
                    If kw = "" OrElse r.Name.ToLowerInvariant().Contains(kw) Then
                        lstRepos.Items.Add(r, False)
                    End If
                Next
            End Sub

        ' ---- 内部：加载 ----
        Dim LoadRepos As Func(Of Task) =
            Async Function() As Task
                btnRefresh.Enabled = False
                btnDelete.Enabled = False
                lblStatus.Text = "加载中..."
                lstRepos.Items.Clear()
                allRepos.Clear()

                Try
                    Using client As New HttpClient()
                        client.Timeout = TimeSpan.FromSeconds(30)
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("GitPushTool/1.0")
                        client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("token", token)
                        client.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/vnd.github+json"))

                        Dim page = 1
                        Do
                            Dim url = $"https://api.github.com/user/repos?per_page=100&page={page}&sort=updated&affiliation=owner"
                            Dim resp = Await client.GetAsync(url)
                            If Not resp.IsSuccessStatusCode Then
                                Dim body = Await resp.Content.ReadAsStringAsync()
                                MessageBox.Show(dlg, "获取仓库失败：" & CInt(resp.StatusCode) & vbCrLf & body,
                                                "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                                Exit Do
                            End If

                            Dim json = Await resp.Content.ReadAsStringAsync()
                            Dim arr = SimpleJsonParser.ParseArray(json)
                            If arr.Count = 0 Then Exit Do

                            For Each obj In arr
                                Dim info As New RepoInfo()
                                info.Name = SimpleJsonParser.GetString(obj, "name")
                                info.FullName = SimpleJsonParser.GetString(obj, "full_name")
                                info.IsPrivate = SimpleJsonParser.GetBool(obj, "private")
                                info.UpdatedAt = SimpleJsonParser.GetString(obj, "updated_at")
                                If info.UpdatedAt.Length >= 10 Then info.UpdatedAt = info.UpdatedAt.Substring(0, 10)
                                allRepos.Add(info)
                            Next

                            If arr.Count < 100 Then Exit Do
                            page += 1
                        Loop

                        ApplyFilter()
                        lblStatus.Text = $"共 {allRepos.Count} 个仓库"
                    End Using
                Catch ex As Exception
                    MessageBox.Show(dlg, "加载异常：" & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Finally
                    btnRefresh.Enabled = True
                    btnDelete.Enabled = True
                End Try
            End Function

        ' ---- 内部：删除 ----
        Dim DoDelete As Func(Of Task) =
            Async Function() As Task
                Dim selected As New List(Of RepoInfo)
                For i = 0 To lstRepos.Items.Count - 1
                    If lstRepos.GetItemChecked(i) Then selected.Add(CType(lstRepos.Items(i), RepoInfo))
                Next

                If selected.Count = 0 Then
                    MessageBox.Show(dlg, "请先勾选要删除的仓库。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If

                Dim names = String.Join(vbCrLf, selected.Select(Function(r) "  • " & r.Name))
                Dim confirm As String = InputBox(
                    "即将删除以下 " & selected.Count & " 个仓库（不可恢复）：" & vbCrLf & vbCrLf &
                    names & vbCrLf & vbCrLf &
                    "请输入 DELETE 以确认：",
                    "危险操作确认", "")
                If confirm <> "DELETE" Then
                    MessageBox.Show(dlg, "已取消删除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
                    Return
                End If

                btnDelete.Enabled = False
                lblStatus.Text = "删除中..."

                Dim okCount = 0
                Dim failList As New List(Of String)

                Try
                    Using client As New HttpClient()
                        client.Timeout = TimeSpan.FromSeconds(30)
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("GitPushTool/1.0")
                        client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("token", token)
                        client.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/vnd.github+json"))

                        For Each r In selected
                            Try
                                Dim resp = Await client.DeleteAsync($"https://api.github.com/repos/{owner}/{r.Name}")
                                If resp.IsSuccessStatusCode Then
                                    okCount += 1
                                    allRepos.RemoveAll(Function(x) x.Name = r.Name)
                                Else
                                    Dim body = Await resp.Content.ReadAsStringAsync()
                                    failList.Add($"{r.Name} (HTTP {CInt(resp.StatusCode)})")
                                End If
                            Catch ex As Exception
                                failList.Add($"{r.Name} ({ex.Message})")
                            End Try
                        Next
                    End Using

                    ApplyFilter()
                    lblStatus.Text = $"删除完成，成功 {okCount} 个"

                    Dim msg = $"成功删除 {okCount} 个仓库。"
                    If failList.Count > 0 Then
                        msg &= vbCrLf & vbCrLf & "失败：" & vbCrLf & String.Join(vbCrLf, failList)
                    End If
                    MessageBox.Show(dlg, msg, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Catch ex As Exception
                    MessageBox.Show(dlg, "删除异常：" & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Finally
                    btnDelete.Enabled = True
                End Try
            End Function

        ' ---- 事件绑定 ----
        AddHandler btnRefresh.Click, Sub() LoadRepos()
        AddHandler txtSearch.TextChanged, Sub() ApplyFilter()
        AddHandler btnSelectAll.Click,
            Sub()
                For i = 0 To lstRepos.Items.Count - 1
                    lstRepos.SetItemChecked(i, True)
                Next
            End Sub
        AddHandler btnInvert.Click,
            Sub()
                For i = 0 To lstRepos.Items.Count - 1
                    lstRepos.SetItemChecked(i, Not lstRepos.GetItemChecked(i))
                Next
            End Sub
        AddHandler btnDelete.Click, Sub() DoDelete()
        AddHandler btnClose.Click, Sub() dlg.Close()
        AddHandler dlg.Shown, Sub() LoadRepos()

        dlg.ShowDialog(Me)
    End Sub

    ' ==================== 仓库信息载体 ====================
    Private Class RepoInfo
        Public Property Name As String
        Public Property IsPrivate As Boolean
        Public Property UpdatedAt As String
        Public Property FullName As String
        Public Overrides Function ToString() As String
            Dim vis = If(IsPrivate, "私有", "公开")
            Return $"{Name}    [{vis}]    {UpdatedAt}"
        End Function
    End Class
End Class
' ==================== 极简 JSON 解析器 ================================
' ====================================================================
Public Module SimpleJsonParser

    ''' <summary>把顶层 JSON 数组切成每个元素的原始字符串</summary>
    Public Function ParseArray(json As String) As List(Of String)
        Dim result As New List(Of String)
        If String.IsNullOrWhiteSpace(json) Then Return result
        Dim s = json.Trim()
        If Not s.StartsWith("[") Then Return result

        Dim depth = 0
        Dim inStr = False
        Dim esc = False
        Dim startIdx = -1

        For i = 0 To s.Length - 1
            Dim c = s(i)
            If inStr Then
                If esc Then
                    esc = False
                ElseIf c = "\"c Then
                    esc = True
                ElseIf c = """"c Then
                    inStr = False
                End If
                Continue For
            End If

            Select Case c
                Case """"c
                    inStr = True
                Case "{"c
                    If depth = 0 Then startIdx = i
                    depth += 1
                Case "}"c
                    depth -= 1
                    If depth = 0 AndAlso startIdx >= 0 Then
                        result.Add(s.Substring(startIdx, i - startIdx + 1))
                        startIdx = -1
                    End If
            End Select
        Next

        Return result
    End Function

    Public Function GetString(objJson As String, key As String) As String
        Dim raw = GetRawValue(objJson, key)
        If raw Is Nothing Then Return ""
        raw = raw.Trim()
        If raw.StartsWith("""") AndAlso raw.EndsWith("""") AndAlso raw.Length >= 2 Then
            Return Unescape(raw.Substring(1, raw.Length - 2))
        End If
        If raw = "null" Then Return ""
        Return raw
    End Function

    Public Function GetBool(objJson As String, key As String) As Boolean
        Dim raw = GetRawValue(objJson, key)
        If raw Is Nothing Then Return False
        Return raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Function GetRawValue(objJson As String, key As String) As String
        If String.IsNullOrWhiteSpace(objJson) Then Return Nothing
        Dim s = objJson.Trim()
        If Not s.StartsWith("{") Then Return Nothing

        Dim depth = 0
        Dim inStr = False
        Dim esc = False
        Dim i = 0

        While i < s.Length
            Dim c = s(i)
            If inStr Then
                If esc Then
                    esc = False
                ElseIf c = "\"c Then
                    esc = True
                ElseIf c = """"c Then
                    inStr = False
                End If
                i += 1
                Continue While
            End If

            Select Case c
                Case """"c
                    If depth = 1 Then
                        Dim keyEnd = FindStringEnd(s, i)
                        If keyEnd > i Then
                            Dim keyStr = Unescape(s.Substring(i + 1, keyEnd - i - 1))
                            Dim j = keyEnd + 1
                            While j < s.Length AndAlso Char.IsWhiteSpace(s(j))
                                j += 1
                            End While
                            If j < s.Length AndAlso s(j) = ":"c Then
                                j += 1
                                While j < s.Length AndAlso Char.IsWhiteSpace(s(j))
                                    j += 1
                                End While
                                If keyStr = key Then
                                    If j < s.Length AndAlso s(j) = """"c Then
                                        Dim vEnd = FindStringEnd(s, j)
                                        Return s.Substring(j, vEnd - j + 1)
                                    ElseIf j < s.Length AndAlso (s(j) = "{"c OrElse s(j) = "["c) Then
                                        Dim closeCh = If(s(j) = "{"c, "}"c, "]"c)
                                        Dim d = 1
                                        Dim k = j + 1
                                        Dim inS2 = False
                                        Dim e2 = False
                                        While k < s.Length AndAlso d > 0
                                            Dim cc = s(k)
                                            If inS2 Then
                                                If e2 Then
                                                    e2 = False
                                                ElseIf cc = "\"c Then
                                                    e2 = True
                                                ElseIf cc = """"c Then
                                                    inS2 = False
                                                End If
                                            Else
                                                If cc = """"c Then
                                                    inS2 = True
                                                ElseIf cc = s(j) Then
                                                    d += 1
                                                ElseIf cc = closeCh Then
                                                    d -= 1
                                                End If
                                            End If
                                            k += 1
                                        End While
                                        Return s.Substring(j, k - j)
                                    Else
                                        Dim k = j
                                        While k < s.Length AndAlso s(k) <> ","c AndAlso s(k) <> "}"c
                                            k += 1
                                        End While
                                        Return s.Substring(j, k - j)
                                    End If
                                End If
                                i = j
                                Continue While
                            End If
                        End If
                    End If
                Case "{"c
                    depth += 1
                Case "}"c
                    depth -= 1
            End Select
            i += 1
        End While
        Return Nothing
    End Function

    Private Function FindStringEnd(s As String, startQuote As Integer) As Integer
        Dim esc = False
        For i = startQuote + 1 To s.Length - 1
            Dim c = s(i)
            If esc Then
                esc = False
            ElseIf c = "\"c Then
                esc = True
            ElseIf c = """"c Then
                Return i
            End If
        Next
        Return -1
    End Function

    Private Function Unescape(s As String) As String
        If s.IndexOf("\"c) < 0 Then Return s
        Dim sb As New StringBuilder()
        Dim i = 0
        While i < s.Length
            Dim c = s(i)
            If c = "\"c AndAlso i + 1 < s.Length Then
                Dim n = s(i + 1)
                Select Case n
                    Case "n"c : sb.Append(vbLf) : i += 2
                    Case "r"c : sb.Append(vbCr) : i += 2
                    Case "t"c : sb.Append(vbTab) : i += 2
                    Case """"c : sb.Append("""") : i += 2
                    Case "\"c : sb.Append("\") : i += 2
                    Case "/"c : sb.Append("/") : i += 2
                    Case "u"c
                        If i + 5 < s.Length Then
                            Dim hex = s.Substring(i + 2, 4)
                            Dim code As Integer
                            If Integer.TryParse(hex, Globalization.NumberStyles.HexNumber, Nothing, code) Then
                                sb.Append(ChrW(code))
                            End If
                            i += 6
                        Else
                            sb.Append(n) : i += 2
                        End If
                    Case Else : sb.Append(n) : i += 2
                End Select
            Else
                sb.Append(c)
                i += 1
            End If
        End While
        Return sb.ToString()
    End Function

End Module