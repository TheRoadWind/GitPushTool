Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Windows.Forms

''' <summary>
''' 主窗体：Git 一键推送工具
''' 布局：推送面板 | README 面板 | 仓库管理面板，底部日志区
''' </summary>
Public Class MainForm

    ' ==================== 主界面控件（由 CreateControls 动态创建） ====================
    ' ---- 左：推送面板 ----
    Private pnlPush As Panel             ' 推送面板容器
    Private lblPushTitle As Label        ' 面板标题
    Private lblProject As Label          ' 项目路径标签
    Private txtProject As TextBox        ' 项目路径输入框
    Private btnBrowse As Button          ' 浏览按钮
    Private lblRepo As Label             ' 仓库地址标签
    Private txtRepo As TextBox           ' 仓库地址输入框
    Private lblEmail As Label            ' 邮箱标签
    Private txtEmail As TextBox          ' 邮箱输入框
    Private lblToken As Label            ' PAT 令牌标签
    Private txtToken As TextBox          ' PAT 令牌输入框
    Private lblMsg As Label              ' 提交信息标签
    Private txtMsg As TextBox            ' 提交信息输入框
    Private lblVersion As Label          ' 版本号标签
    Private txtVersion As TextBox        ' 版本号输入框（写入 .vbproj）
    Private chkTag As CheckBox           ' 是否打 tag
    Private txtTag As TextBox            ' tag 名称
    Private btnUpgradeVersion As Button  ' 确认升级按钮
    Private chkInit As CheckBox          ' 自动 git init
    Private chkIgnore As CheckBox        ' 生成 .gitignore
    Private chkReadme As CheckBox        ' 生成 README.md
    Private chkCreateRepo As CheckBox    ' 自动建远程仓库
    Private chkForce As CheckBox         ' 强制覆盖推送
    Private btnSettings As Button        ' 设置按钮
    Private btnPush As Button            ' 创建并推送按钮
    Private lblHint As Label             ' 提示文字

    ' ---- 中：README 面板 ----
    Private pnlReadme As Panel           ' README 面板容器
    Private lblReadmeTitle As Label      ' 面板标题
    Private txtReadme As TextBox         ' README 多行编辑框

    ' ---- 右：仓库管理面板 ----
    Private pnlRepoMgr As Panel          ' 仓库管理面板容器
    Private lblRepoMgrTitle As Label     ' 面板标题
    Private lblSearch As Label           ' 搜索标签
    Private txtSearch As TextBox         ' 搜索框
    Private btnRefresh As Button         ' 刷新按钮
    Private lblStatus As Label           ' 状态标签
    Private lstRepos As CheckedListBox   ' 仓库列表
    Private btnSelectAll As Button       ' 全选
    Private btnInvert As Button          ' 反选
    Private btnDelete As Button          ' 删除选中

    ' ---- 底部：日志 ----
    Private txtLog As TextBox            ' 日志输出框

    ' ==================== 状态 ====================
    Private currentUser As String = ""        ' 当前 GitHub 用户名
    Private currentGit As String = ""         ' 当前 git.exe 完整路径
    Private allRepos As New List(Of RepoInfo)() ' 当前账号下所有仓库

    ' 配置文件目录与路径:C:\Users\Administrator\AppData\Roaming\GitPushTool
    Private ReadOnly configDir As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitPushTool")
    Private ReadOnly configFile As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitPushTool", "config.ini")

    ' ==================== 构造函数 ====================
    ''' <summary>构造函数：调用 InitializeComponent 后创建控件、绑定事件、加载配置</summary>
    Public Sub New()
        InitializeComponent()          ' 保留 Designer 生成的初始化
        CreateControls()               ' 动态创建所有控件
        LoadConfig()                   ' 加载配置
        If currentUser = "" Then currentUser = InputBox("GitHub 用户名：", "设置", currentUser)
        If currentGit = "" OrElse Not File.Exists(currentGit) Then
            currentGit = DetectGit("")
        End If
        If String.IsNullOrWhiteSpace(txtReadme.Text) Then
            txtReadme.Text = DefaultReadme("")
        End If
    End Sub

    ' ==================== 动态创建所有控件 ====================
    ''' <summary>创建所有控件并绑定事件</summary>
    Private Sub CreateControls()
        Me.Text = "Git 一键推送工具"
        Me.ClientSize = New Size(1250, 680)
        Me.AllowDrop = True
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.MinimumSize = New Size(1250, 680)

        ' ========== 左侧：推送面板 ==========
        pnlPush = New Panel() With {
            .Location = New Point(10, 10),
            .Size = New Size(500, 480),
            .BorderStyle = BorderStyle.FixedSingle
        }
        Me.Controls.Add(pnlPush)

        lblPushTitle = New Label() With {
            .Text = "推送面板",
            .Location = New Point(10, 8),
            .AutoSize = True,
            .Font = New Font("微软雅黑", 10.0F, FontStyle.Bold)
        }
        pnlPush.Controls.Add(lblPushTitle)

        ' 项目路径
        lblProject = New Label() With {.Text = "项目路径：", .Location = New Point(10, 45), .AutoSize = True}
        pnlPush.Controls.Add(lblProject)
        txtProject = New TextBox() With {.Location = New Point(90, 42), .Size = New Size(320, 23)}
        pnlPush.Controls.Add(txtProject)
        btnBrowse = New Button() With {.Text = "浏览...", .Location = New Point(415, 41), .Size = New Size(75, 25), .UseVisualStyleBackColor = True}
        pnlPush.Controls.Add(btnBrowse)

        ' 仓库地址
        lblRepo = New Label() With {.Text = "仓库地址：", .Location = New Point(10, 80), .AutoSize = True}
        pnlPush.Controls.Add(lblRepo)
        txtRepo = New TextBox() With {.Location = New Point(90, 77), .Size = New Size(400, 23)}
        pnlPush.Controls.Add(txtRepo)

        ' 邮箱
        lblEmail = New Label() With {.Text = "邮箱：", .Location = New Point(10, 115), .AutoSize = True}
        pnlPush.Controls.Add(lblEmail)
        txtEmail = New TextBox() With {.Location = New Point(90, 112), .Size = New Size(400, 23)}
        pnlPush.Controls.Add(txtEmail)

        ' PAT 令牌
        lblToken = New Label() With {.Text = "PAT令牌：", .Location = New Point(10, 150), .AutoSize = True}
        pnlPush.Controls.Add(lblToken)
        txtToken = New TextBox() With {.Location = New Point(90, 147), .Size = New Size(400, 23), .UseSystemPasswordChar = True}
        pnlPush.Controls.Add(txtToken)

        ' 提交信息
        lblMsg = New Label() With {.Text = "提交信息：", .Location = New Point(10, 185), .AutoSize = True}
        pnlPush.Controls.Add(lblMsg)
        txtMsg = New TextBox() With {.Text = "初始化提交", .Location = New Point(90, 182), .Size = New Size(400, 23)}
        pnlPush.Controls.Add(txtMsg)

        ' 版本号 + tag + 确认升级
        lblVersion = New Label() With {.Text = "版本号：", .Location = New Point(10, 220), .AutoSize = True}
        pnlPush.Controls.Add(lblVersion)
        txtVersion = New TextBox() With {.Text = "1.0.0.0", .Location = New Point(90, 217), .Size = New Size(130, 23)}
        pnlPush.Controls.Add(txtVersion)
        chkTag = New CheckBox() With {.Text = "同步打 tag", .Location = New Point(230, 220), .AutoSize = True, .Checked = False}
        pnlPush.Controls.Add(chkTag)
        txtTag = New TextBox() With {.Text = "v1.0.0", .Location = New Point(330, 217), .Size = New Size(160, 23)}
        pnlPush.Controls.Add(txtTag)

        btnUpgradeVersion = New Button() With {
            .Text = "确认升级",
            .Location = New Point(330, 247),
            .Size = New Size(160, 28),
            .BackColor = Color.LightSteelBlue,
            .FlatStyle = FlatStyle.Flat
        }
        btnUpgradeVersion.FlatAppearance.BorderSize = 0
        pnlPush.Controls.Add(btnUpgradeVersion)

        ' 选项
        chkInit = New CheckBox() With {.Text = "自动 git init", .Location = New Point(90, 285), .AutoSize = True, .Checked = True}
        pnlPush.Controls.Add(chkInit)
        chkIgnore = New CheckBox() With {.Text = "生成 .gitignore", .Location = New Point(210, 285), .AutoSize = True, .Checked = True}
        pnlPush.Controls.Add(chkIgnore)
        chkReadme = New CheckBox() With {.Text = "生成 README.md", .Location = New Point(340, 285), .AutoSize = True, .Checked = True}
        pnlPush.Controls.Add(chkReadme)
        chkCreateRepo = New CheckBox() With {.Text = "自动建远程仓库", .Location = New Point(90, 315), .AutoSize = True, .Checked = True}
        pnlPush.Controls.Add(chkCreateRepo)
        chkForce = New CheckBox() With {.Text = "强制覆盖推送（--force）", .Location = New Point(240, 315), .AutoSize = True, .Checked = False}
        pnlPush.Controls.Add(chkForce)

        ' 按钮
        btnSettings = New Button() With {.Text = "设置", .Location = New Point(90, 350), .Size = New Size(90, 32), .UseVisualStyleBackColor = True}
        pnlPush.Controls.Add(btnSettings)
        btnPush = New Button() With {.Text = "创建并推送", .Location = New Point(330, 350), .Size = New Size(160, 32), .UseVisualStyleBackColor = True}
        pnlPush.Controls.Add(btnPush)

        ' 提示
        lblHint = New Label() With {
            .Text = "提示：可把项目文件夹拖到窗口自动填入项目路径",
            .Location = New Point(10, 395),
            .AutoSize = True,
            .ForeColor = Color.Gray
        }
        pnlPush.Controls.Add(lblHint)

        ' ========== 中间：README 面板 ==========
        pnlReadme = New Panel() With {
            .Location = New Point(520, 10),
            .Size = New Size(330, 480),
            .BorderStyle = BorderStyle.FixedSingle
        }
        Me.Controls.Add(pnlReadme)

        lblReadmeTitle = New Label() With {
            .Text = "README 面板",
            .Location = New Point(10, 8),
            .AutoSize = True,
            .Font = New Font("微软雅黑", 10.0F, FontStyle.Bold)
        }
        pnlReadme.Controls.Add(lblReadmeTitle)

        txtReadme = New TextBox() With {
            .Location = New Point(10, 35),
            .Size = New Size(308, 435),
            .Multiline = True,
            .ScrollBars = ScrollBars.Both,
            .WordWrap = False,
            .AcceptsReturn = True,
            .Font = New Font("Consolas", 9.0F),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        }
        pnlReadme.Controls.Add(txtReadme)

        ' ========== 右侧：仓库管理面板 ==========
        pnlRepoMgr = New Panel() With {
            .Location = New Point(860, 10),
            .Size = New Size(370, 480),
            .BorderStyle = BorderStyle.FixedSingle
        }
        Me.Controls.Add(pnlRepoMgr)

        lblRepoMgrTitle = New Label() With {
            .Text = "仓库管理面板",
            .Location = New Point(10, 8),
            .AutoSize = True,
            .Font = New Font("微软雅黑", 10.0F, FontStyle.Bold)
        }
        pnlRepoMgr.Controls.Add(lblRepoMgrTitle)

        lblSearch = New Label() With {.Text = "搜索：", .Location = New Point(10, 45), .AutoSize = True}
        pnlRepoMgr.Controls.Add(lblSearch)
        txtSearch = New TextBox() With {.Location = New Point(55, 42), .Size = New Size(160, 23)}
        pnlRepoMgr.Controls.Add(txtSearch)
        btnRefresh = New Button() With {.Text = "刷新", .Location = New Point(220, 41), .Size = New Size(75, 25), .UseVisualStyleBackColor = True}
        pnlRepoMgr.Controls.Add(btnRefresh)

        lblStatus = New Label() With {.Text = "未加载", .Location = New Point(10, 72), .AutoSize = True, .ForeColor = Color.Gray}
        pnlRepoMgr.Controls.Add(lblStatus)

        lstRepos = New CheckedListBox() With {
            .Location = New Point(10, 95),
            .Size = New Size(350, 315),
            .CheckOnClick = True,
            .Font = New Font("Consolas", 9.0F),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        }
        pnlRepoMgr.Controls.Add(lstRepos)

        btnSelectAll = New Button() With {.Text = "全选", .Location = New Point(10, 420), .Size = New Size(70, 30), .UseVisualStyleBackColor = True}
        pnlRepoMgr.Controls.Add(btnSelectAll)
        btnInvert = New Button() With {.Text = "反选", .Location = New Point(85, 420), .Size = New Size(70, 30), .UseVisualStyleBackColor = True}
        pnlRepoMgr.Controls.Add(btnInvert)
        btnDelete = New Button() With {
            .Text = "删除选中",
            .Location = New Point(190, 420),
            .Size = New Size(105, 30),
            .BackColor = Color.IndianRed,
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat
        }
        btnDelete.FlatAppearance.BorderSize = 0
        pnlRepoMgr.Controls.Add(btnDelete)

        ' ========== 底部：日志 ==========
        txtLog = New TextBox() With {
            .Location = New Point(10, 500),
            .Size = New Size(1155, 170),
            .Multiline = True,
            .ScrollBars = ScrollBars.Vertical,
            .Font = New Font("Consolas", 9.0F),
            .ReadOnly = True,
            .BackColor = Color.Black,
            .ForeColor = Color.LightGreen,
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right
        }
        Me.Controls.Add(txtLog)

        ' ========== 事件绑定 ==========
        AddHandler btnBrowse.Click, AddressOf btnBrowse_Click
        AddHandler chkTag.CheckedChanged, Sub()
                                              If chkTag.Checked Then
                                                  txtTag.Text = txtVersion.Text.Trim()
                                              Else
                                                  txtTag.Text = ""
                                              End If
                                          End Sub
        AddHandler btnPush.Click, AddressOf btnPush_Click
        AddHandler btnSettings.Click, AddressOf btnSettings_Click
        AddHandler btnUpgradeVersion.Click, AddressOf btnUpgradeVersion_Click
        AddHandler txtProject.TextChanged, AddressOf txtProject_TextChanged
        AddHandler btnRefresh.Click, AddressOf btnRefresh_Click
        AddHandler txtSearch.TextChanged, AddressOf txtSearch_TextChanged
        AddHandler btnSelectAll.Click, AddressOf btnSelectAll_Click
        AddHandler btnInvert.Click, AddressOf btnInvert_Click
        AddHandler btnDelete.Click, AddressOf btnDelete_Click
        AddHandler Me.FormClosing, AddressOf MainForm_FormClosing
        AddHandler Me.Shown, AddressOf MainForm_Shown


    End Sub

    ' ==================== 窗体首次显示 ====================
    ''' <summary>窗体首次显示时自动加载仓库列表</summary>
    Private Async Sub MainForm_Shown(sender As Object, e As EventArgs)
        Await LoadRepoListAsync()
    End Sub

    ' ==================== 加载仓库列表 ====================
    ''' <summary>拉取当前账号的仓库列表并显示到右侧列表</summary>
    Private Async Function LoadRepoListAsync() As Task
        Dim token = txtToken.Text.Trim()
        allRepos.Clear()
        lstRepos.Items.Clear()

        If token = "" Then
            lblStatus.Text = "未填写 PAT"
            lstRepos.Items.Add("（未填写 PAT，无法加载仓库）", False)
            Return
        End If

        lblStatus.Text = "加载中..."
        btnRefresh.Enabled = False

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
                        lblStatus.Text = $"加载失败：HTTP {CInt(resp.StatusCode)}"
                        Exit Do
                    End If

                    Dim json = Await resp.Content.ReadAsStringAsync()
                    Dim arr = SimpleJsonParser.ParseArray(json)
                    If arr.Count = 0 Then Exit Do

                    For Each obj In arr
                        Dim info As New RepoInfo With {
                            .Name = SimpleJsonParser.GetString(obj, "name"),
                            .FullName = SimpleJsonParser.GetString(obj, "full_name"),
                            .IsPrivate = SimpleJsonParser.GetBool(obj, "private"),
                            .UpdatedAt = SimpleJsonParser.GetString(obj, "updated_at")
                        }
                        If info.UpdatedAt.Length >= 10 Then info.UpdatedAt = info.UpdatedAt.Substring(0, 10)
                        allRepos.Add(info)
                    Next
                    ' 批量拉每个仓库的最新 tag（放在同一 Using client 里，走同一个 client）
                    For Each info In allRepos
                        Try
                            Dim tagUrl = $"https://api.github.com/repos/{currentUser}/{info.Name}/tags?per_page=1"
                            Dim tagResp = Await client.GetAsync(tagUrl)
                            If tagResp.IsSuccessStatusCode Then
                                Dim tagJson = Await tagResp.Content.ReadAsStringAsync()
                                Dim tagArr = SimpleJsonParser.ParseArray(tagJson)
                                If tagArr.Count > 0 Then
                                    info.LatestTag = SimpleJsonParser.GetString(tagArr(0), "name")
                                End If
                            End If
                        Catch
                            ' 单个仓库失败不影响整体
                        End Try
                    Next
                    If arr.Count < 100 Then Exit Do
                    page += 1
                Loop

                ApplyFilter()
                lblStatus.Text = $"共 {allRepos.Count} 个仓库"
            End Using
        Catch ex As Exception
            lblStatus.Text = "加载异常：" & ex.Message
        Finally
            btnRefresh.Enabled = True
        End Try
    End Function

    ' ==================== 过滤仓库 ====================
    ''' <summary>根据搜索关键字过滤仓库列表</summary>
    Private Sub ApplyFilter()
        Dim kw = txtSearch.Text.Trim().ToLowerInvariant()
        lstRepos.Items.Clear()
        For Each r In allRepos
            If kw = "" OrElse r.Name.ToLowerInvariant().Contains(kw) Then
                lstRepos.Items.Add(r, False)
            End If
        Next
    End Sub
    ''' <summary>项目路径变化时自动拼仓库地址并读取 .vbproj 版本号</summary>
    Private Sub txtProject_TextChanged(sender As Object, e As EventArgs)
        Dim dir = txtProject.Text.Trim()
        If dir = "" Then Return
        Try
            Dim name = New DirectoryInfo(dir).Name
            If name <> "" Then
                txtRepo.Text = $"https://github.com/{currentUser}/{name}.git"
            End If

            ' 自动读取 .vbproj 版本号
            Dim v = ReadProjectVersion(dir)
            If v <> "" Then txtVersion.Text = v Else txtVersion.Text = "1.0.0.0"

            ' 自动读取 README.md 内容
            Dim readmePath = Path.Combine(dir, "README.md")
            If File.Exists(readmePath) Then
                txtReadme.Text = File.ReadAllText(readmePath, Encoding.UTF8)
            Else
                txtReadme.Text = DefaultReadme(name)
            End If
        Catch
        End Try
    End Sub

    ' ==================== 浏览 ====================
    ''' <summary>浏览按钮：选择项目文件夹</summary>
    Private Sub btnBrowse_Click(sender As Object, e As EventArgs)
        Using fbd As New FolderBrowserDialog()
            fbd.Description = "选择项目目录"
            If fbd.ShowDialog() = DialogResult.OK Then
                txtProject.Text = fbd.SelectedPath
            End If
        End Using
    End Sub

    ' ==================== 设置 ====================
    ''' <summary>设置按钮：修改用户名与 git 路径</summary>
    Private Sub btnSettings_Click(sender As Object, e As EventArgs)
        Dim newUser = InputBox("GitHub 用户名：", "设置", currentUser)
        If newUser = "" Then Return
        Dim newGit = InputBox("git.exe 路径（留空 = 自动探测）：", "设置", currentGit)

        currentUser = newUser.Trim()
        If currentUser = "" Then currentUser = InputBox("GitHub 用户名：", "设置", currentUser)

        Dim g = newGit.Trim()
        If g <> "" AndAlso Not File.Exists(g) Then
            MessageBox.Show("指定的 git 不存在，将自动探测。", "提示")
            g = DetectGit("")
        End If
        If g = "" Then g = DetectGit("")
        currentGit = g

        SaveConfig()
        MessageBox.Show("设置已保存。" & vbCrLf & vbCrLf &
                        "当前用户：" & currentUser & vbCrLf &
                        "git.exe：" & If(currentGit = "", "(未找到)", currentGit),
                        "完成", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ' ==================== 确认升级版本号 ====================
    ''' <summary>确认升级：写回 .vbproj → 重读版本号 → 同步 README 里的版本号</summary>
    Private Sub btnUpgradeVersion_Click(sender As Object, e As EventArgs)
        Dim projectPath = txtProject.Text.Trim()
        If projectPath = "" OrElse Not Directory.Exists(projectPath) Then
            MessageBox.Show("请先选择有效的项目路径！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim newVer = txtVersion.Text.Trim()
        If newVer = "" Then
            MessageBox.Show("版本号不能为空！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        ' 旧版本号（用于同步 README）
        Dim oldVer = ReadProjectVersion(projectPath)

        ' 写入 .vbproj
        WriteProjectVersion(projectPath, newVer)

        ' 重读，显示实际值
        Dim realVer = ReadProjectVersion(projectPath)
        If realVer <> "" Then txtVersion.Text = realVer

        ' 同步 README 里的版本号
        If oldVer <> "" AndAlso realVer <> "" AndAlso oldVer <> realVer Then
            SyncReadmeVersion(oldVer, realVer)
            Log($"==> [版本号] README 中的 {oldVer} 已同步为 {realVer}")
        ElseIf oldVer = "" AndAlso realVer <> "" Then
            SyncReadmeVersion("1.0.0", realVer)
            Log($"==> [版本号] README 中的 1.0.0 已同步为 {realVer}")
        End If

        SaveConfig()

        '重新读取 README.md 内容

        Dim readmePath = Path.Combine(projectPath, "README.md")
        If File.Exists(readmePath) Then
            txtReadme.Text = File.ReadAllText(readmePath, Encoding.UTF8)
        Else
            txtReadme.Text = DefaultReadme(New DirectoryInfo(projectPath).Name)
        End If
        MessageBox.Show($"版本号已升级到：{realVer}" & vbCrLf & "README 已同步。",
                        "完成", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    ''' <summary>把 README 文本里出现的旧版本号替换为新版本号</summary>
    Private Sub SyncReadmeVersion(oldVer As String, newVer As String)
        If oldVer = "" OrElse newVer = "" OrElse oldVer = newVer Then Return
        Dim text = txtReadme.Text
        If String.IsNullOrEmpty(text) Then Return

        ' 情况 A：`### vX.Y.Z` / `### X.Y.Z`
        text = Regex.Replace(text,
            "###\s*v?" & Regex.Escape(oldVer) & "(?=\s|\(|\r|\n|$)",
            "### v" & newVer,
            RegexOptions.IgnoreCase)

        ' 情况 B：其它裸版本号
        text = Regex.Replace(text,
            "(?<![\w\.])v?" & Regex.Escape(oldVer) & "(?![\w\.])",
            "v" & newVer,
            RegexOptions.IgnoreCase)

        txtReadme.Text = text
    End Sub

    ' ==================== 仓库管理按钮 ====================
    ''' <summary>刷新按钮</summary>
    Private Async Sub btnRefresh_Click(sender As Object, e As EventArgs)
        Await LoadRepoListAsync()
    End Sub

    ''' <summary>搜索内容变化</summary>
    Private Sub txtSearch_TextChanged(sender As Object, e As EventArgs)
        ApplyFilter()
    End Sub

    ''' <summary>全选</summary>
    Private Sub btnSelectAll_Click(sender As Object, e As EventArgs)
        For i = 0 To lstRepos.Items.Count - 1
            lstRepos.SetItemChecked(i, True)
        Next
    End Sub

    ''' <summary>反选</summary>
    Private Sub btnInvert_Click(sender As Object, e As EventArgs)
        For i = 0 To lstRepos.Items.Count - 1
            lstRepos.SetItemChecked(i, Not lstRepos.GetItemChecked(i))
        Next
    End Sub

    ''' <summary>删除选中仓库</summary>
    Private Async Sub btnDelete_Click(sender As Object, e As EventArgs)
        Dim token = txtToken.Text.Trim()
        If token = "" Then
            MessageBox.Show("请先填写 PAT 令牌！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        Dim selected As New List(Of RepoInfo)
        For i = 0 To lstRepos.Items.Count - 1
            If lstRepos.GetItemChecked(i) Then
                Dim r = TryCast(lstRepos.Items(i), RepoInfo)
                If r IsNot Nothing Then selected.Add(r)
            End If
        Next

        If selected.Count = 0 Then
            MessageBox.Show("请先勾选要删除的仓库。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Dim names = String.Join(vbCrLf, selected.Select(Function(r) "  • " & r.Name))
        Dim confirm As String = InputBox("即将删除以下 " & selected.Count & " 个仓库（不可恢复）：" & vbCrLf & vbCrLf & names & vbCrLf & vbCrLf & "请输入 DELETE 以确认：", "危险操作确认", "")
        If confirm <> "DELETE" Then
            MessageBox.Show("已取消删除。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information)
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
                        Dim resp = Await client.DeleteAsync($"https://api.github.com/repos/{currentUser}/{r.Name}")
                        If resp.IsSuccessStatusCode Then
                            okCount += 1
                            allRepos.RemoveAll(Function(x) x.Name = r.Name)
                        Else
                            Dim code = CInt(resp.StatusCode)
                            Dim hint = ""
                            If code = 403 Then
                                hint = " [权限不足：Classic需勾 delete_repo；Fine-grained需 Administration:write + All repositories]"
                            ElseIf code = 404 Then
                                hint = " [仓库不存在或PAT无权访问]"
                            End If
                            failList.Add($"{r.Name} (HTTP {code}){hint}")
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
            MessageBox.Show(msg, "完成", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show("删除异常：" & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            btnDelete.Enabled = True
        End Try
    End Sub

    ' ==================== git 自动探测 ====================
    ''' <summary>探测可用的 git.exe 路径</summary>
    Private Function DetectGit(projectDir As String) As String
        If projectDir <> "" AndAlso Directory.Exists(projectDir) Then
            Dim candidates As New List(Of String) From {
                Path.Combine(projectDir, "git.exe"),
                Path.Combine(projectDir, "PortableGit", "bin", "git.exe"),
                Path.Combine(projectDir, "PortableGit", "cmd", "git.exe")
            }
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

        Dim sysPaths = {
            "C:\Program Files\Git\bin\git.exe",
            "C:\Program Files\Git\cmd\git.exe",
            "C:\Program Files (x86)\Git\bin\git.exe",
            "C:\Program Files (x86)\Git\cmd\git.exe"
        }
        For Each p In sysPaths
            If File.Exists(p) Then Return p
        Next

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

    ' ==================== 项目版本号读写 ====================
    ''' <summary>在项目目录里找 .vbproj 并读取版本号（Version &gt; AssemblyVersion &gt; FileVersion）</summary>
    Private Function ReadProjectVersion(projectDir As String) As String
        Try
            Dim projFiles = Directory.GetFiles(projectDir, "*.vbproj", SearchOption.TopDirectoryOnly)
            If projFiles.Length = 0 Then Return ""
            Dim content = File.ReadAllText(projFiles(0), Encoding.UTF8)

            Dim v = GetXmlTagValue(content, "Version")
            If v <> "" Then Return v
            v = GetXmlTagValue(content, "AssemblyVersion")
            If v <> "" Then Return v
            v = GetXmlTagValue(content, "FileVersion")
            If v <> "" Then Return v
        Catch
        End Try
        Return ""
    End Function

    ''' <summary>取 &lt;tag&gt;...&lt;/tag&gt; 的值</summary>
    Private Function GetXmlTagValue(xml As String, tag As String) As String
        Dim m = Regex.Match(xml, "<" & tag & ">(.*?)</" & tag & ">", RegexOptions.IgnoreCase)
        If m.Success Then Return m.Groups(1).Value.Trim()
        Return ""
    End Function

    ''' <summary>把版本号写回 .vbproj（Version / AssemblyVersion / FileVersion；Version 不存在时自动插入）</summary>
    Private Sub WriteProjectVersion(projectDir As String, version As String)
        If version = "" Then Return
        Try
            Dim projFiles = Directory.GetFiles(projectDir, "*.vbproj", SearchOption.TopDirectoryOnly)
            If projFiles.Length = 0 Then
                Log("==> [版本号] 未找到 .vbproj，跳过")
                Return
            End If

            Dim path = projFiles(0)
            Dim content = File.ReadAllText(path, Encoding.UTF8)

            ' Version：不存在则插入
            content = EnsureXmlTag(content, "Version", version)
            ' AssemblyVersion / FileVersion：存在才替换
            content = ReplaceXmlTagIfExists(content, "AssemblyVersion", version)
            content = ReplaceXmlTagIfExists(content, "FileVersion", version)

            File.WriteAllText(path, content, New UTF8Encoding(False))
            Log($"==> [版本号] 已写入 {IO.Path.GetFileName(path)}：{version}")
        Catch ex As Exception
            Log("==> [版本号] 写入失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>确保 &lt;tag&gt; 存在；不存在则插入到第一个 PropertyGroup 内</summary>
    Private Function EnsureXmlTag(xml As String, tag As String, value As String) As String
        If Regex.IsMatch(xml, "<" & tag & ">", RegexOptions.IgnoreCase) Then
            Return Regex.Replace(xml, "(<" & tag & ">)(.*?)(</" & tag & ">)",
                                 "${1}" & value & "${3}", RegexOptions.IgnoreCase)
        End If
        ' 插入到第一个 <PropertyGroup> 之后
        Dim m = Regex.Match(xml, "<PropertyGroup[^>]*>", RegexOptions.IgnoreCase)
        If m.Success Then
            Dim insertPos = m.Index + m.Length
            Dim insertText = vbCrLf & "    <" & tag & ">" & value & "</" & tag & ">"
            Return xml.Substring(0, insertPos) & insertText & xml.Substring(insertPos)
        End If
        Return xml
    End Function

    ''' <summary>仅当 &lt;tag&gt; 存在时才替换</summary>
    Private Function ReplaceXmlTagIfExists(xml As String, tag As String, value As String) As String
        If Not Regex.IsMatch(xml, "<" & tag & ">", RegexOptions.IgnoreCase) Then Return xml
        Return Regex.Replace(xml, "(<" & tag & ">)(.*?)(</" & tag & ">)",
                             "${1}" & value & "${3}", RegexOptions.IgnoreCase)
    End Function

    ' ==================== 配置读写 ====================
    ''' <summary>读取配置</summary>
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
                    Case "user" : currentUser = If(v = "", InputBox("GitHub 用户名：", "设置", currentUser), v)
                    Case "email" : txtEmail.Text = v
                    Case "token" : txtToken.Text = v
                    Case "git" : If File.Exists(v) Then currentGit = v
                End Select
            Next
        Catch ex As Exception
            Log("读取配置失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>写入配置（不保存 README）</summary>
    Private Sub SaveConfig()
        Try
            If Not Directory.Exists(configDir) Then Directory.CreateDirectory(configDir)
            Dim sb As New StringBuilder()
            sb.AppendLine("# GitPushTool 配置（自动生成，可手动编辑）")
            sb.AppendLine("# user  : GitHub 用户名")
            sb.AppendLine("# email : git 提交邮箱")
            sb.AppendLine("# token : GitHub PAT")
            sb.AppendLine("# git   : git.exe 完整路径（留空则自动探测）")
            sb.AppendLine()
            sb.AppendLine("user=" & currentUser)
            sb.AppendLine("email=" & txtEmail.Text.Trim())
            sb.AppendLine("token=" & txtToken.Text.Trim())
            sb.AppendLine("git=" & currentGit)

            File.WriteAllText(configFile, sb.ToString(), New UTF8Encoding(False))

            '自动保存 README 内容到配置目录
            If txtProject.Text.Trim() <> "" Then
                Dim projectPath = txtProject.Text.Trim()
                Dim readmePath = Path.Combine(projectPath, "README.md")
                File.WriteAllText(readmePath, txtReadme.Text, New UTF8Encoding(False))
            End If
        Catch ex As Exception
            Log("保存配置失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>窗体关闭时保存配置</summary>
    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs)
        SaveConfig()
    End Sub

    ' ==================== 推送主流程 ====================
    ''' <summary>推送按钮：init → add → commit → tag → push（版本号由“确认升级”按钮控制）</summary>
    Private Async Sub btnPush_Click(sender As Object, e As EventArgs)
        Dim projectPath = txtProject.Text.Trim()
        Dim repoUrl = txtRepo.Text.Trim()
        Dim userName = currentUser
        Dim userEmail = txtEmail.Text.Trim()
        Dim token = txtToken.Text.Trim()
        Dim commitMsg = txtMsg.Text.Trim()
        Dim gitExe = If(currentGit <> "" AndAlso File.Exists(currentGit), currentGit, "git")

        ' ---- 校验 ----
        If projectPath = "" OrElse Not Directory.Exists(projectPath) Then
            MessageBox.Show("项目路径无效！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If
        If repoUrl = "" Then
            MessageBox.Show("仓库地址为空！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If
        If commitMsg = "" Then commitMsg = "初始化提交"

        ' 版本号一致性检查（可选增强）
        Dim projVer = ReadProjectVersion(projectPath)
        If projVer <> "" AndAlso projVer <> txtVersion.Text.Trim() Then
            Dim r = MessageBox.Show(
                $"当前 .vbproj 里的版本号是 {projVer}，" & vbCrLf &
                $"界面里填的是 {txtVersion.Text.Trim()}。" & vbCrLf & vbCrLf &
                "是否先执行「确认升级」？",
                "版本号不一致", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
            If r = DialogResult.Yes Then
                btnUpgradeVersion_Click(Nothing, Nothing)
            End If
        End If

        SaveConfig()
        btnPush.Enabled = False
        txtLog.Clear()
        Log("==> 使用 git：" & gitExe)

        Try
            Await RunGitAsync(gitExe, projectPath, "--version")

            Dim firstPush As Boolean = Not Directory.Exists(Path.Combine(projectPath, ".git"))
            If chkInit.Checked AndAlso firstPush Then
                Log("==> git init")
                Await RunGitAsync(gitExe, projectPath, "init")
            Else
                Log("==> 已存在 .git，跳过 init")
            End If

            If userName <> "" Then Await RunGitAsync(gitExe, projectPath, $"config user.name ""{userName}""")
            If userEmail <> "" Then Await RunGitAsync(gitExe, projectPath, $"config user.email ""{userEmail}""")

            ' .gitignore
            If chkIgnore.Checked Then
                Dim ignorePath = Path.Combine(projectPath, ".gitignore")
                If Not File.Exists(ignorePath) Then
                    Log("==> 生成 .gitignore")
                    File.WriteAllText(ignorePath, DefaultGitIgnore(), New UTF8Encoding(False))
                Else
                    Log("==> .gitignore 已存在，跳过")
                End If
            End If

            ' README.md
            If chkReadme.Checked Then
                Dim readmePath = Path.Combine(projectPath, "README.md")
                Dim projName = New DirectoryInfo(projectPath).Name
                Dim content = txtReadme.Text
                If String.IsNullOrWhiteSpace(content) Then content = DefaultReadme(projName)
                content = content.Replace("{项目名}", projName).Replace("{用户名}", currentUser)

                Log("==> 写入 README.md")
                File.WriteAllText(readmePath, content, New UTF8Encoding(False))
            End If

            ' 自动创建远程仓库
            If chkCreateRepo.Checked Then
                If token = "" Then
                    Log("==> [跳过建仓库] 未填写 PAT")
                Else
                    Dim repoName = GetRepoNameFromUrl(repoUrl)
                    If repoName = "" Then
                        Log("==> [跳过建仓库] 无法解析仓库名")
                    Else
                        Log($"==> 检查/创建远程仓库：{currentUser}/{repoName}")
                        Dim r = Await EnsureGitHubRepoAsync(currentUser, repoName, token)
                        Select Case r
                            Case RepoResult.Created : Log("    已创建")
                            Case RepoResult.Exists : Log("    已存在，跳过")
                            Case Else : Log("    创建失败（继续尝试推送）")
                        End Select
                    End If
                End If
            End If

            ' 远程地址
            Dim pushUrl = repoUrl
            If token <> "" Then
                pushUrl = InjectToken(repoUrl, userName, token)
                Log("==> 使用带 PAT 的远程地址（日志中已隐藏）")
            End If

            Log("==> 配置远程 origin")
            Dim hasOrigin = Await HasRemoteAsync(gitExe, projectPath, "origin")
            If hasOrigin Then
                Await RunGitAsync(gitExe, projectPath, "remote remove origin", ignoreError:=True)
            End If
            Await RunGitAsync(gitExe, projectPath, $"remote add origin ""{pushUrl}""")

            Log("==> git add .")
            Await RunGitAsync(gitExe, projectPath, "add .")

            ' 提交信息
            Dim finalMsg = If(commitMsg = "", "初始化提交", commitMsg)
            If Not firstPush AndAlso finalMsg = "初始化提交" Then
                finalMsg = "更新 " & DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            End If

            Log("==> git commit")
            Await RunGitAsync(gitExe, projectPath, $"commit -m ""{finalMsg}""", ignoreError:=True)

            Log("==> git branch -M main")
            Await RunGitAsync(gitExe, projectPath, "branch -M main")

            ' ---- 打 tag ----
            If chkTag.Checked Then
                Dim tagName = txtTag.Text.Trim()
                If tagName = "" Then
                    Dim segs = txtVersion.Text.Trim().Split("."c)
                    If segs.Length >= 3 Then
                        tagName = "v" & segs(0) & "." & segs(1) & "." & segs(2)
                    Else
                        tagName = "v" & txtVersion.Text.Trim()
                    End If
                    txtTag.Text = tagName
                End If

                ' 先删除远程可能存在的所有历史 tag（同名或不同名）
                Log("==> 清理远程历史 tag")
                Await DeleteAllRemoteTagsAsync(gitExe, projectPath)

                ' 删除本地同名 tag（如有）
                Await RunGitAsync(gitExe, projectPath, $"tag -d ""{tagName}""", ignoreError:=True)

                ' 重新打 tag
                Log($"==> git tag {tagName}")
                Await RunGitAsync(gitExe, projectPath, $"tag ""{tagName}""", ignoreError:=True)
            End If

            ' ---- 推代码 ----
            If chkForce.Checked Then
                Log("==> git push -u origin main --force（强制覆盖）")
                Await RunGitAsync(gitExe, projectPath, "push -u origin main --force")
            Else
                Log("==> git push -u origin main")
                Await RunGitAsync(gitExe, projectPath, "push -u origin main")
            End If

            ' ---- 推 tag ----
            If chkTag.Checked AndAlso txtTag.Text.Trim() <> "" Then
                Log($"==> git push origin {txtTag.Text.Trim()} --force")
                Await RunGitAsync(gitExe, projectPath, $"push origin ""{txtTag.Text.Trim()}"" --force", ignoreError:=True)
            End If

            ' 还原远程地址
            If token <> "" Then
                Await RunGitAsync(gitExe, projectPath, $"remote set-url origin ""{repoUrl}""")
            End If

            Log(vbCrLf & "===== 推送完成 =====")
            Await LoadRepoListAsync()
            MessageBox.Show("推送成功！", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            Log(vbCrLf & "[错误] " & ex.Message)
            MessageBox.Show("发生错误：" & ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error)
        Finally
            btnPush.Enabled = True
        End Try
    End Sub

    ' ==================== 辅助函数 ====================
    ''' <summary>判断 origin 是否存在</summary>
    Private Async Function HasRemoteAsync(gitExe As String, workDir As String, remoteName As String) As Task(Of Boolean)
        Dim result As Boolean = False
        Await Task.Run(Sub()
                           Try
                               Dim psi As New ProcessStartInfo() With {
                                   .FileName = gitExe,
                                   .Arguments = "remote",
                                   .WorkingDirectory = workDir,
                                   .RedirectStandardOutput = True,
                                   .UseShellExecute = False,
                                   .CreateNoWindow = True
                               }
                               Using p As Process = Process.Start(psi)
                                   Dim output = p.StandardOutput.ReadToEnd()
                                   p.WaitForExit()
                                   result = output.Split({ControlChars.Cr, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries).
                                                   Any(Function(s) s.Trim() = remoteName)
                               End Using
                           Catch
                           End Try
                       End Sub)
        Return result
    End Function
    ''' <summary>删除远程仓库里的所有 tag（逐个删除；本地保留）</summary>
    Private Async Function DeleteAllRemoteTagsAsync(gitExe As String, workDir As String) As Task
        ' 1) 列出远程所有 tag
        Dim remoteTags As New List(Of String)
        Try
            Dim psi As New ProcessStartInfo() With {
            .FileName = gitExe,
            .Arguments = "ls-remote --tags origin",
            .WorkingDirectory = workDir,
            .RedirectStandardOutput = True,
            .UseShellExecute = False,
            .CreateNoWindow = True
        }
            Dim output As String = ""
            Await Task.Run(Sub()
                               Using p As Process = Process.Start(psi)
                                   output = p.StandardOutput.ReadToEnd()
                                   p.WaitForExit()
                               End Using
                           End Sub)

            ' 输出示例：
            '   3f2a1b...	refs/tags/v1.0.0
            '   9d8e7c...	refs/tags/v1.0.0^{}
            For Each line In output.Split({ControlChars.Cr, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries)
                Dim parts = line.Split(ControlChars.Tab)
                If parts.Length >= 2 Then
                    Dim refName = parts(1).Trim()
                    If refName.StartsWith("refs/tags/") AndAlso Not refName.EndsWith("^{}") Then
                        Dim tagName = refName.Substring("refs/tags/".Length)
                        If Not remoteTags.Contains(tagName) Then remoteTags.Add(tagName)
                    End If
                End If
            Next
        Catch ex As Exception
            Log("    [tag] 列出远程 tag 失败：" & ex.Message)
        End Try

        ' 2) 逐个删除远程 tag
        For Each t In remoteTags
            Log($"    删除远程 tag：{t}")
            Await RunGitAsync(gitExe, workDir, $"push origin :refs/tags/{t}", ignoreError:=True)
        Next

        ' 3) 同步清理本地 tag（可选，避免本地堆积）
        For Each t In remoteTags
            Await RunGitAsync(gitExe, workDir, $"tag -d ""{t}""", ignoreError:=True)
        Next

        If remoteTags.Count = 0 Then
            Log("    没有需要清理的历史 tag")
        End If
    End Function
    ''' <summary>把 PAT 注入到 https 地址中</summary>
    Private Function InjectToken(repoUrl As String, user As String, token As String) As String
        If Not repoUrl.StartsWith("https://") Then Return repoUrl
        Dim rest = repoUrl.Substring("https://".Length)
        Return $"https://{user}:{token}@{rest}"
    End Function

    ''' <summary>从仓库地址解析仓库名</summary>
    Private Function GetRepoNameFromUrl(url As String) As String
        Try
            Dim u = url.TrimEnd("/"c)
            If u.EndsWith(".git", StringComparison.OrdinalIgnoreCase) Then
                u = u.Substring(0, u.Length - 4)
            End If
            Dim parts = u.Split("/"c)
            If parts.Length >= 2 Then Return parts(parts.Length - 1)
        Catch
        End Try
        Return ""
    End Function

    ''' <summary>建仓库结果枚举</summary>
    Private Enum RepoResult
        Created
        Exists
        Failed
    End Enum

    ''' <summary>调用 GitHub API 检查/创建仓库</summary>
    Private Async Function EnsureGitHubRepoAsync(owner As String, repoName As String, token As String) As Task(Of RepoResult)
        Try
            Using client As New HttpClient()
                client.Timeout = TimeSpan.FromSeconds(20)
                client.DefaultRequestHeaders.UserAgent.ParseAdd("GitPushTool/1.0")
                client.DefaultRequestHeaders.Authorization = New AuthenticationHeaderValue("token", token)
                client.DefaultRequestHeaders.Accept.Add(New MediaTypeWithQualityHeaderValue("application/vnd.github+json"))

                Dim checkResp = Await client.GetAsync($"https://api.github.com/repos/{owner}/{repoName}")
                If checkResp.IsSuccessStatusCode Then Return RepoResult.Exists

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
                    Return RepoResult.Exists
                ElseIf CInt(resp.StatusCode) = 403 Then
                    Log("    [权限不足] PAT 需要勾选 repo（Classic）或 Administration:write（Fine-grained）")
                    Return RepoResult.Failed
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

    ''' <summary>JSON 转义</summary>
    Private Function EscapeJson(s As String) As String
        If s Is Nothing Then Return ""
        Dim q As String = Chr(34)
        Dim bs As String = Chr(92)
        Dim r As String = s
        r = r.Replace(bs, bs & bs)
        r = r.Replace(q, bs & q)
        r = r.Replace(vbCr, bs & "r")
        r = r.Replace(vbLf, bs & "n")
        r = r.Replace(vbTab, bs & "t")
        Return r
    End Function

    ' ==================== 执行 git ====================
    ''' <summary>异步执行 git 命令，输出到日志</summary>
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

    ' ==================== 日志 ====================
    ''' <summary>线程安全地追加日志</summary>
    Private Sub Log(text As String)
        If txtLog.InvokeRequired Then
            txtLog.Invoke(Sub() Log(text))
            Return
        End If
        txtLog.AppendText(text & Environment.NewLine)
    End Sub

    ' ==================== 模板 ====================
    ''' <summary>默认 .gitignore</summary>
    Private Function DefaultGitIgnore() As String
        Return "# ==== 通用 ====" & vbCrLf &
               "Thumbs.db" & vbCrLf &
               "Desktop.ini" & vbCrLf &
               ".DS_Store" & vbCrLf & vbCrLf &
               "# ==== .NET / VB.NET ====" & vbCrLf &
               "bin/" & vbCrLf &
               "obj/" & vbCrLf &
               "Debug/" & vbCrLf &
               "Release/" & vbCrLf &
               ".vs/" & vbCrLf &
               "*.suo" & vbCrLf &
               "*.user" & vbCrLf &
               "*.vbproj.user" & vbCrLf & vbCrLf &
               "# ==== Python ====" & vbCrLf &
               "__pycache__/" & vbCrLf &
               "*.py[cod]" & vbCrLf &
               "*.egg-info/" & vbCrLf &
               ".venv/" & vbCrLf &
               "venv/" & vbCrLf &
               ".pytest_cache/" & vbCrLf & vbCrLf &
               "# ==== 构建产物 ====" & vbCrLf &
               "*.exe" & vbCrLf &
               "*.dll" & vbCrLf &
               "*.pdb" & vbCrLf &
               "*.zip" & vbCrLf &
               "*.7z" & vbCrLf & vbCrLf &
               "# ==== 编辑器 ====" & vbCrLf &
               ".idea/" & vbCrLf &
               ".vscode/" & vbCrLf
    End Function

    ''' <summary>默认 README 内容</summary>
    Private Function DefaultReadme(projName As String) As String
        Dim sb As New StringBuilder()
        sb.AppendLine("# {项目名}")
        sb.AppendLine()
        sb.AppendLine("> 由 GitPushTool 自动生成")
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
        sb.AppendLine()
        sb.AppendLine("## 🚀 快速开始")
        sb.AppendLine()
        sb.AppendLine("```bash")
        sb.AppendLine("git clone https://github.com/{用户名}/{项目名}.git")
        sb.AppendLine("cd {项目名}")
        sb.AppendLine("```")
        sb.AppendLine()
        sb.AppendLine("## 📁 目录结构")
        sb.AppendLine()
        sb.AppendLine("```")
        sb.AppendLine("{项目名}/")
        sb.AppendLine("├── src/")
        sb.AppendLine("├── docs/")
        sb.AppendLine("├── tests/")
        sb.AppendLine("├── .gitignore")
        sb.AppendLine("└── README.md")
        sb.AppendLine("```")
        sb.AppendLine()
        sb.AppendLine("## 📝 更新日志")
        sb.AppendLine()
        sb.AppendLine("### v1.0.0 (" & DateTime.Now.ToString("yyyy-MM-dd") & ")")
        sb.AppendLine()
        sb.AppendLine("- 初始版本")
        sb.AppendLine()
        sb.AppendLine("## 📄 License")
        sb.AppendLine()
        sb.AppendLine("本项目基于 MIT 协议开源。")
        sb.AppendLine()
        sb.AppendLine("---")
        sb.AppendLine()
        sb.AppendLine("_Generated by GitPushTool on " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & "_")
        Return sb.ToString()
    End Function

    ' ==================== 仓库信息载体 ====================
    ''' <summary>仓库信息（用于列表显示）</summary>
    Private Class RepoInfo
        Public Property Name As String
        Public Property IsPrivate As Boolean
        Public Property UpdatedAt As String
        Public Property FullName As String
        Public Property LatestTag As String   ' 最新 tag，没有则为空

        Public Overrides Function ToString() As String
            Dim vis = If(IsPrivate, "私有", "公开")
            Dim tagText = If(String.IsNullOrEmpty(LatestTag), "-", LatestTag)
            Return $"{Name}    [{vis}]    {tagText}    {UpdatedAt}"
        End Function
    End Class

End Class

' ====================================================================
' ==================== 极简 JSON 解析器 =============================
' ====================================================================
''' <summary>极简 JSON 解析器：仅解析本项目用到的简单结构</summary>
Public Module SimpleJsonParser

    ''' <summary>解析顶层 JSON 数组</summary>
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

    ''' <summary>取字符串</summary>
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

    ''' <summary>取布尔值</summary>
    Public Function GetBool(objJson As String, key As String) As Boolean
        Dim raw = GetRawValue(objJson, key)
        If raw Is Nothing Then Return False
        Return raw.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>取原始值</summary>
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

    ''' <summary>字符串结束位置</summary>
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

    ''' <summary>JSON 反转义</summary>
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