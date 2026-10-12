Imports System.Diagnostics
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.IO
Imports System.IO.Compression
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
    Private txtVersion As TextBox        ' 版本号输入框（只读：自动从工程文件 / exe / apk 等探测，禁止手工修改）
    Private lblVersionInfo As Label      ' 版本号来源提示（例如来自某个 exe / apk / 工程文件）
    Private chkTag As CheckBox           ' 是否打 tag
    Private txtTag As TextBox            ' tag 名称
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
    Private suppressProjectChanged As Boolean = False ' 抑制 txtProject 变更引起的重复加载
    Private versionSourceText As String = ""          ' 最近一次版本号探测的来源说明（用于界面展示）

    ' 工程文件与编译产物里都探测不到版本号时使用的初始版本号
    Private Const InitialVersion As String = "1.0.0.0"

    ' 生成 README 目录结构时每层最多列出的条目数（防止 README 过长）
    Private Const MaxTreeEntriesPerLevel As Integer = 12

    ' 配置文件目录与路径：C:\Users\Administrator\AppData\Roaming\GitPushTool
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

        ' 窗体图标：加载嵌入资源里的 app.ico（同时 exe 文件图标也由 vbproj 的 ApplicationIcon 指定）
        Try
            Using icoStream = GetType(MainForm).Assembly.GetManifestResourceStream("GitPushTool.app.ico")
                If icoStream IsNot Nothing Then Me.Icon = New Icon(icoStream)
            End Using
        Catch
            ' 图标加载失败不影响主流程
        End Try

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

        ' 版本号：自动从不同项目类型里探测读取，文本框设为只读（不可手工修改）
        lblVersion = New Label() With {.Text = "版本号：", .Location = New Point(10, 220), .AutoSize = True}
        pnlPush.Controls.Add(lblVersion)
        txtVersion = New TextBox() With {
            .Text = "",
            .Location = New Point(90, 217),
            .Size = New Size(130, 23),
            .ReadOnly = True,               ' 版本号只读，禁止手改
            .BackColor = Color.WhiteSmoke,  ' 用浅灰底色直观提示“不可编辑”
            .TabStop = False                ' 不参与 Tab 焦点切换
        }
        pnlPush.Controls.Add(txtVersion)
        chkTag = New CheckBox() With {.Text = "同步打 tag", .Location = New Point(230, 220), .AutoSize = True, .Checked = False}
        pnlPush.Controls.Add(chkTag)
        txtTag = New TextBox() With {.Text = "v1.0.0", .Location = New Point(330, 217), .Size = New Size(160, 23)}
        pnlPush.Controls.Add(txtTag)

        ' 版本号来源提示（例如：来源：GitPushTool.exe 的 FileVersion）
        lblVersionInfo = New Label() With {
            .Text = "",
            .Location = New Point(90, 243),
            .Size = New Size(400, 16),
            .ForeColor = Color.Gray,
            .AutoEllipsis = True
        }
        pnlPush.Controls.Add(lblVersionInfo)

        ' 选项（删除“确认升级”按钮后整体上移，避免留空）
        chkInit = New CheckBox() With {.Text = "自动 git init", .Location = New Point(90, 268), .AutoSize = True, .Checked = True}
        pnlPush.Controls.Add(chkInit)
        chkIgnore = New CheckBox() With {.Text = "生成 .gitignore", .Location = New Point(210, 268), .AutoSize = True, .Checked = True}
        pnlPush.Controls.Add(chkIgnore)
        chkReadme = New CheckBox() With {.Text = "生成 README.md", .Location = New Point(340, 268), .AutoSize = True, .Checked = True}
        pnlPush.Controls.Add(chkReadme)
        chkCreateRepo = New CheckBox() With {.Text = "自动建远程仓库", .Location = New Point(90, 296), .AutoSize = True, .Checked = True}
        pnlPush.Controls.Add(chkCreateRepo)
        chkForce = New CheckBox() With {.Text = "强制覆盖推送（--force）", .Location = New Point(240, 296), .AutoSize = True, .Checked = False}
        pnlPush.Controls.Add(chkForce)

        ' 按钮
        btnSettings = New Button() With {.Text = "设置", .Location = New Point(90, 330), .Size = New Size(90, 32), .UseVisualStyleBackColor = True}
        pnlPush.Controls.Add(btnSettings)
        btnPush = New Button() With {.Text = "创建并推送", .Location = New Point(330, 330), .Size = New Size(160, 32), .UseVisualStyleBackColor = True}
        pnlPush.Controls.Add(btnPush)

        ' 提示
        lblHint = New Label() With {
            .Text = "提示：可把项目文件夹拖到窗口自动填入项目路径",
            .Location = New Point(10, 372),
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

        '设置控件启用状态
        SetControlsEnabled(False)

        ' ========== 事件绑定 ==========
        AddHandler btnBrowse.Click, AddressOf btnBrowse_Click
        AddHandler chkTag.CheckedChanged, Sub()
                                              If chkTag.Checked Then
                                                  ' 勾选打 tag 时，用探测到的版本号生成默认 tag 名
                                                  txtTag.Text = BuildTagName(txtVersion.Text.Trim())
                                              Else
                                                  txtTag.Text = ""
                                              End If
                                          End Sub
        AddHandler btnPush.Click, AddressOf btnPush_Click
        AddHandler btnSettings.Click, AddressOf btnSettings_Click
        AddHandler txtProject.TextChanged, AddressOf txtProject_TextChanged
        AddHandler btnRefresh.Click, AddressOf btnRefresh_Click
        AddHandler txtSearch.TextChanged, AddressOf txtSearch_TextChanged
        AddHandler btnSelectAll.Click, AddressOf btnSelectAll_Click
        AddHandler btnInvert.Click, AddressOf btnInvert_Click
        AddHandler btnDelete.Click, AddressOf btnDelete_Click
        AddHandler Me.FormClosing, AddressOf MainForm_FormClosing
        AddHandler Me.Shown, AddressOf MainForm_Shown

        ' ========== 让主窗体和所有子控件都支持拖放 ==========
        EnableDragDropRecursive(Me)
    End Sub

    ''' <summary>递归给所有子控件启用拖放并把事件指向主窗体处理</summary>
    Private Sub EnableDragDropRecursive(parent As Control)
        For Each c As Control In parent.Controls
            c.AllowDrop = True
            AddHandler c.DragEnter, AddressOf MainForm_DragEnter
            AddHandler c.DragDrop, AddressOf MainForm_DragDrop
            If c.HasChildren Then EnableDragDropRecursive(c)
        Next
    End Sub
    '设置统一的控件是否启用（打 tag、推送）；版本号始终只读展示，不参与启用开关
    Private Sub SetControlsEnabled(enabled As Boolean)
        chkTag.Enabled = enabled
        btnPush.Enabled = enabled
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

                    If arr.Count < 100 Then Exit Do
                    page += 1
                Loop

                ' 批量拉每个仓库的最新 tag
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

    ' ==================== 拖拽 ====================
    ''' <summary>拖入文件时判定：只接受文件夹</summary>
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

    ''' <summary>拖入文件夹后写入项目路径</summary>
    Private Sub MainForm_DragDrop(sender As Object, e As DragEventArgs)
        Dim paths = CType(e.Data.GetData(DataFormats.FileDrop), String())
        If paths Is Nothing OrElse paths.Length = 0 Then Return
        If Not Directory.Exists(paths(0)) Then Return
        txtProject.Text = paths(0)
    End Sub

    ''' <summary>项目路径变化时自动拼仓库地址并读取 .vbproj 版本号</summary>
    Private Sub txtProject_TextChanged(sender As Object, e As EventArgs)
        If suppressProjectChanged Then Return

        Dim dir = txtProject.Text.Trim()
        If dir = "" Then Return
        Try
            Dim name = New DirectoryInfo(dir).Name
            If name <> "" Then
                txtRepo.Text = $"https://github.com/{currentUser}/{name}.git"
                '启用版本号编辑\升级按钮\打tag
                SetControlsEnabled(True)
            Else
                SetControlsEnabled(False)
                MessageBox.Show("无法获取项目文件夹名称，请检查路径是否正确。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If



            ' 自动探测版本号（支持 VB/C# 工程、Node、Maven、Gradle、Flutter、Rust、Python、
            ' AndroidManifest、Info.plist、编译产物 exe/dll、apk/aab 等，探测顺序见 ReadProjectVersion）
            Dim v = ReadProjectVersion(dir)
            If v <> "" Then
                txtVersion.Text = v
            Else
                ' 工程文件、编译产物里都没有版本信息时，按初始版本处理
                txtVersion.Text = InitialVersion
            End If
            lblVersionInfo.Text = versionSourceText   ' 展示版本号来源，便于确认探测是否正确

            ' 版本号变化后同步刷新默认 tag 名
            If chkTag.Checked Then txtTag.Text = BuildTagName(txtVersion.Text.Trim())

            ' 自动读取 README.md 内容
            Dim readmePath = Path.Combine(dir, "README.md")
            If File.Exists(readmePath) Then
                txtReadme.Text = File.ReadAllText(readmePath, Encoding.UTF8)
            Else
                txtReadme.Text = DefaultReadme(name)
            End If

            ' 统一整理 README：立即替换 {项目名}/{用户名} 占位符 + 刷新目录结构
            ' （此前占位符只在推送时替换，面板里会一直显示模板占位符）
            NormalizeReadme(dir, name)

            ' 自动把探测到的版本号同步进 README 的更新日志（无需再手动点“确认升级”）
            If v <> "" Then AutoSyncReadmeVersion(v)
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

    ' ==================== README 版本号自动同步 ====================
    ''' <summary>
    ''' 自动把探测到的版本号同步到 README 的更新日志（替代原来的“确认升级”按钮）：
    ''' 1) 若更新日志第一条记录的版本号与探测值一致 → 仅刷新该条日期
    ''' 2) 若不一致 → 在更新日志最前面插入一条新版本记录，保留历史记录
    ''' 3) README 没有“更新日志”小节时自动补齐标题再插入
    ''' 4) 最后刷新末尾的 _Generated by GitPushTool ..._ 时间戳
    ''' </summary>
    Private Sub AutoSyncReadmeVersion(newVer As String)
        ' 无版本号或 README 为空时不处理
        If String.IsNullOrWhiteSpace(newVer) Then Return
        Dim text = txtReadme.Text
        If String.IsNullOrWhiteSpace(text) Then Return

        ' 统一换行符后按行拆分处理，避免 CRLF / LF 混用导致正则失配
        Dim lines = New List(Of String)(text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(vbLf))
        Dim today = DateTime.Now.ToString("yyyy-MM-dd")

        ' 定位更新日志里第一条 "### vX.Y.Z (日期)" 记录（第一条 = 最新版本）
        Dim idx = -1
        Dim oldVer As String = ""
        For i = 0 To lines.Count - 1
            Dim m = Regex.Match(lines(i), "^(#{2,6})[ \t]*v?(\d+(?:\.\d+){1,3})[ \t]*(\([^)]*\))?[ \t]*$")
            If m.Success Then
                idx = i
                oldVer = m.Groups(2).Value
                Exit For
            End If
        Next

        If idx >= 0 AndAlso VersionEquals(oldVer, newVer) Then
            ' 版本号没变：只刷新这一条记录的日期
            lines(idx) = "### v" & newVer & " (" & today & ")"
        Else
            ' 版本号变了（或 README 里还没有版本记录）：插入一条新记录，保留历史
            Dim entry = New List(Of String) From {
                "### v" & newVer & " (" & today & ")",
                "",
                If(IsFirstReleaseVersion(newVer), "- 初始版本", "- 版本更新"),
                ""
            }
            If idx >= 0 Then
                lines.InsertRange(idx, entry)                       ' 插到现有记录前面
            Else
                Dim insertAt = InsertChangelogHeader(lines)         ' 没有“更新日志”小节则先补标题
                lines.InsertRange(insertAt, entry)
            End If
        End If

        ' 刷新末尾生成时间戳
        RefreshGeneratedStamp(lines)

        Dim newText = String.Join(vbCrLf, lines)
        If newText = text Then
            Log("==> [版本号] README 已是最新版本 " & newVer & "，无需修改")
        Else
            txtReadme.Text = newText
            Log($"==> [版本号] README 已自动同步为 {newVer}")
        End If
    End Sub

    ''' <summary>判断 README 里是否已有“更新日志”标题，没有则补一个；返回内容应插入的行号</summary>
    Private Function InsertChangelogHeader(lines As List(Of String)) As Integer
        ' 已存在 "## 📝 更新日志" 之类的小节：插到标题下一行
        For i = 0 To lines.Count - 1
            If Regex.IsMatch(lines(i), "^##\s*.*更新日志") Then
                Return i + 1
            End If
        Next
        ' 没有小节：插到末尾生成时间戳之前
        For i = lines.Count - 1 To 0 Step -1
            If lines(i).StartsWith("_Generated by GitPushTool on ") Then
                lines.InsertRange(i, New String() {"## 📝 更新日志", ""})
                Return i + 2
            End If
        Next
        ' 兜底：追加到文件末尾
        lines.Add("## 📝 更新日志")
        lines.Add("")
        Return lines.Count
    End Function

    ''' <summary>刷新 README 末尾的 _Generated by GitPushTool on xxx_ 时间戳</summary>
    Private Sub RefreshGeneratedStamp(lines As List(Of String))
        For i = 0 To lines.Count - 1
            If Regex.IsMatch(lines(i), "_Generated by GitPushTool on ") Then
                lines(i) = "_Generated by GitPushTool on " & DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & "_"
                Return
            End If
        Next
    End Sub

    ''' <summary>比较两个版本号是否等价（位数不同时按 0 补齐，如 1.0 与 1.0.0 视为相同）</summary>
    Private Function VersionEquals(a As String, b As String) As Boolean
        If String.IsNullOrWhiteSpace(a) OrElse String.IsNullOrWhiteSpace(b) Then Return False
        Dim pa = a.Split("."c)
        Dim pb = b.Split("."c)
        Dim n = Math.Max(pa.Length, pb.Length)
        For i = 0 To n - 1
            ' 缺失的段按 0 处理；非数字段直接按字符串比较
            Dim sa = If(i < pa.Length, pa(i).Trim(), "0")
            Dim sb2 = If(i < pb.Length, pb(i).Trim(), "0")
            Dim na As Long = 0, nb As Long = 0
            Dim okA = Long.TryParse(sa, na)
            Dim okB = Long.TryParse(sb2, nb)
            If okA AndAlso okB Then
                If na <> nb Then Return False
            ElseIf String.Compare(sa, sb2, StringComparison.OrdinalIgnoreCase) <> 0 Then
                Return False
            End If
        Next
        Return True
    End Function

    ''' <summary>根据版本号生成默认 tag 名（取前三段并加 v 前缀，如 2.3.9.4 -> v2.3.9）</summary>
    Private Function BuildTagName(ver As String) As String
        Dim s = If(ver, "").Trim()
        If s = "" Then Return ""
        Dim segs = s.Split("."c)
        If segs.Length >= 3 Then Return "v" & segs(0) & "." & segs(1) & "." & segs(2)
        Return "v" & s
    End Function

    ' ==================== README 占位符与目录结构 ====================
    ''' <summary>
    ''' 统一整理 README 面板内容：
    ''' 1) 立即把 {项目名}/{用户名} 占位符替换为真实值（不再等到推送时才替换）
    ''' 2) 把"目录结构"小节刷新为当前项目的真实目录树
    ''' 3) 把"环境要求"小节的运行时同步为项目实际技术栈
    ''' 4) 自动导入项目图标并在 README 顶部显示（ico 会转成 PNG，GitHub 才能渲染）
    ''' </summary>
    Private Sub NormalizeReadme(projectDir As String, projName As String)
        If String.IsNullOrWhiteSpace(txtReadme.Text) Then Return

        ' 1) 占位符替换（{项目名} / {用户名}）
        txtReadme.Text = txtReadme.Text.Replace("{项目名}", projName).Replace("{用户名}", currentUser)

        ' 2) 目录结构刷新（以当前项目实际目录为准）
        UpdateReadmeDirectoryTree(projectDir)

        ' 3) 环境要求里的运行时同步（自动识别 .NET / Node / Python / Java / Android 等）
        UpdateReadmeEnvironment(projectDir)

        ' 4) 应用图标：导入到 docs/ 并在 README 顶部插图
        UpdateReadmeIcon(projectDir)
    End Sub

    ''' <summary>
    ''' 更新 README 里"目录结构"小节：替换该标题下第一个代码块的内容
    ''' 没有该小节时自动补齐（插到"更新日志"之前），没有代码块时自动插入
    ''' </summary>
    Private Sub UpdateReadmeDirectoryTree(projectDir As String)
        ' 先生成目录树文本；生成失败或为空则不改动 README
        Dim tree = BuildDirectoryTree(projectDir)
        If String.IsNullOrWhiteSpace(tree) Then Return

        ' 统一换行符后按行处理，避免 CRLF / LF 混用
        Dim lines = New List(Of String)(txtReadme.Text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(vbLf))

        ' 定位 "## 📁 目录结构" 标题
        Dim headIdx = -1
        For i = 0 To lines.Count - 1
            If Regex.IsMatch(lines(i), "^#{1,6}\s*.*目录结构") Then
                headIdx = i
                Exit For
            End If
        Next

        If headIdx < 0 Then
            ' 没有小节：插到"更新日志"标题之前；找不到就追加到末尾
            Dim insertAt = lines.Count
            For i = 0 To lines.Count - 1
                If Regex.IsMatch(lines(i), "^#{1,6}\s*.*更新日志") Then
                    insertAt = i
                    Exit For
                End If
            Next
            lines.InsertRange(insertAt, New String() {"## 📁 目录结构", "", "```", "", "```", ""})
            headIdx = insertAt
        End If

        ' 在标题之后找第一对 ``` 代码块（遇到下一个标题就停）
        Dim fenceOpen = -1
        For i = headIdx + 1 To lines.Count - 1
            If lines(i).TrimStart().StartsWith("```") Then
                fenceOpen = i
                Exit For
            End If
            If Regex.IsMatch(lines(i), "^#{1,6}\s") Then Exit For
        Next

        ' 目录树由 StringBuilder.AppendLine 生成（换行符是 vbCrLf），
        ' 必须先统一成 vbLf 再拆分，否则每行末尾残留 "\r" 会让 README 里多出空行
        Dim treeLines = New List(Of String)(tree.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(vbLf))
        If fenceOpen >= 0 Then
            ' 找闭合围栏
            Dim fenceClose = -1
            For i = fenceOpen + 1 To lines.Count - 1
                If lines(i).TrimStart().StartsWith("```") Then
                    fenceClose = i
                    Exit For
                End If
            Next
            If fenceClose < 0 Then
                ' 代码块没有闭合：插入目录树并补上闭合围栏
                treeLines.Add("```")
                lines.InsertRange(fenceOpen + 1, treeLines)
            Else
                ' 用新的目录树替换代码块内部内容
                lines.RemoveRange(fenceOpen + 1, fenceClose - fenceOpen - 1)
                lines.InsertRange(fenceOpen + 1, treeLines)
            End If
        Else
            ' 标题下没有代码块：直接插入一个完整代码块
            Dim block As New List(Of String) From {"```"}
            block.AddRange(treeLines)
            block.Add("```")
            lines.InsertRange(headIdx + 1, block)
        End If

        txtReadme.Text = String.Join(vbCrLf, lines)
    End Sub

    ''' <summary>生成项目目录结构树文本（根目录行 + 各层条目，跳过构建/依赖目录，最多 3 层）</summary>
    Private Function BuildDirectoryTree(projectDir As String) As String
        Try
            If String.IsNullOrWhiteSpace(projectDir) OrElse Not Directory.Exists(projectDir) Then Return ""
            Dim sb As New StringBuilder()
            ' 第一行：根目录名
            sb.AppendLine(New DirectoryInfo(projectDir).Name & "/")
            AppendTreeLevel(sb, projectDir, "", 0, 3)
            Return sb.ToString().TrimEnd()
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' 递归拼接目录树的一层；每层先列子目录再列文件，均按名称排序
    ''' 单层最多列 MaxEntriesPerLevel 项，超出部分用省略行表示，避免 README 过长
    ''' </summary>
    Private Sub AppendTreeLevel(sb As StringBuilder, dir As String, indent As String, level As Integer, maxLevel As Integer)
        Try
            ' 收集本层条目：Key=名称，Value=是否目录
            Dim entries As New List(Of KeyValuePair(Of String, Boolean))()
            For Each d In Directory.GetDirectories(dir)
                Dim nm = New DirectoryInfo(d).Name
                If IsSkipDirectory(nm) Then Continue For
                entries.Add(New KeyValuePair(Of String, Boolean)(nm, True))
            Next
            For Each f In Directory.GetFiles(dir)
                entries.Add(New KeyValuePair(Of String, Boolean)(Path.GetFileName(f), False))
            Next
            ' 先目录后文件，各按名称排序
            entries = entries.OrderBy(Function(e) If(e.Value, 0, 1)).ThenBy(Function(e) e.Key, StringComparer.OrdinalIgnoreCase).ToList()

            Dim total = entries.Count
            Dim shown = Math.Min(total, MaxTreeEntriesPerLevel)
            For i = 0 To shown - 1
                ' 是否为本层最后一个条目（本层被截断时永远不是最后一个）
                Dim isLast = (i = shown - 1) AndAlso (total <= MaxTreeEntriesPerLevel)
                Dim e = entries(i)
                sb.Append(indent).Append(If(isLast, "└── ", "├── "))
                If e.Value Then
                    ' 目录：名称后加 /，并在未到最大层级时递归展开
                    sb.Append(e.Key).Append("/").AppendLine()
                    If level + 1 < maxLevel Then
                        AppendTreeLevel(sb, Path.Combine(dir, e.Key),
                                        indent & If(isLast, "    ", "│   "), level + 1, maxLevel)
                    End If
                Else
                    sb.Append(e.Key).AppendLine()
                End If
            Next
            ' 本层条目被截断时补一行省略提示
            If total > MaxTreeEntriesPerLevel Then
                sb.Append(indent).Append("└── …（其余 ").Append(total - MaxTreeEntriesPerLevel).Append(" 项未列出）").AppendLine()
            End If
        Catch
            ' 单个目录读取失败跳过即可
        End Try
    End Sub

    ' ==================== README 环境要求（运行时）自动同步 ====================
    ''' <summary>
    ''' 探测项目运行时 / 技术栈，并同步到 README 的"环境要求"小节
    ''' 支持：.NET、Node.js、Python、Java、Android、Flutter、Go、Rust、PHP 等
    ''' </summary>
    Private Sub UpdateReadmeEnvironment(projectDir As String)
        Dim osText As String = ""
        Dim runtime = DetectRuntime(projectDir, osText)
        ' 探测不到任何技术栈时不改动 README，避免误覆盖用户手写内容
        If String.IsNullOrWhiteSpace(runtime) Then Return

        ' 统一换行符后按行处理
        Dim lines = New List(Of String)(txtReadme.Text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(vbLf))

        ' 定位 "## 🛠️ 环境要求" 标题
        Dim headIdx = -1
        For i = 0 To lines.Count - 1
            If Regex.IsMatch(lines(i), "^#{1,6}\s*.*环境要求") Then
                headIdx = i
                Exit For
            End If
        Next

        If headIdx < 0 Then
            ' 没有小节：插到"更新日志"/"目录结构"/"快速开始"之前，都找不到就追加到末尾
            Dim insertAt = lines.Count
            For Each kw In New String() {"更新日志", "目录结构", "快速开始"}
                For i = 0 To lines.Count - 1
                    If Regex.IsMatch(lines(i), "^#{1,6}\s*.*" & kw) Then
                        insertAt = i
                        Exit For
                    End If
                Next
                If insertAt < lines.Count Then Exit For
            Next
            lines.InsertRange(insertAt, New String() {"## 🛠️ 环境要求", "", ""})
            headIdx = insertAt
        End If

        ' 小节范围：从标题下一行到下一个标题之前
        Dim endIdx = lines.Count
        For i = headIdx + 1 To lines.Count - 1
            If Regex.IsMatch(lines(i), "^#{1,6}\s") Then
                endIdx = i
                Exit For
            End If
        Next

        ' 1) 运行时行：已存在则替换，不存在则追加到小节末尾
        Dim runtimeLine = "- 运行时：" & runtime
        If Not ReplaceSectionLine(lines, headIdx, endIdx, "运行时", runtimeLine) Then
            AppendLineToSection(lines, headIdx, endIdx, runtimeLine)
        End If

        ' 2) 操作系统行：仅在能推断出操作系统、且原行还是模板默认值时才替换
        If osText <> "" Then
            ReplaceSectionLine(lines, headIdx, endIdx, "操作系统",
                               "- 操作系统：" & osText, onlyIfContains:="Windows 10 / 11")
        End If

        txtReadme.Text = String.Join(vbCrLf, lines)
    End Sub

    ''' <summary>
    ''' 探测项目运行时 / 技术栈；osText 返回可推断的操作系统描述
    ''' 探测顺序：.NET 工程 → Node → Python → Java/Maven → Gradle/Android → Flutter → Go → Rust → PHP → apk 兜底
    ''' </summary>
    Private Function DetectRuntime(projectDir As String, ByRef osText As String) As String
        osText = ""
        Try
            If String.IsNullOrWhiteSpace(projectDir) OrElse Not Directory.Exists(projectDir) Then Return ""
            Dim v As String = ""

            ' 1) .NET：优先读 TargetFramework / TargetFrameworks（如 net10.0-windows）
            For Each ext In New String() {"*.vbproj", "*.csproj", "*.fsproj"}
                Dim files = Directory.GetFiles(projectDir, ext, SearchOption.TopDirectoryOnly)
                If files.Length = 0 Then Continue For
                Dim content = File.ReadAllText(files(0), Encoding.UTF8)
                Dim tfm = GetXmlTagValue(content, "TargetFramework")
                If tfm = "" Then tfm = GetXmlTagValue(content, "TargetFrameworks")
                If tfm <> "" Then Return ParseDotNetTfm(tfm, osText)
            Next

            ' 2) Node.js：package.json 的 engines.node（没写就只标 Node.js）
            v = FirstMatchInFile(projectDir, "package.json", """node""\s*:\s*""([^""]+)""")
            If v <> "" Then
                osText = "Windows / Linux / macOS"
                Return "Node.js " & v
            End If
            Dim hasPkg = FindFile(projectDir, "package.json")
            If hasPkg <> "" Then
                osText = "Windows / Linux / macOS"
                Return "Node.js"
            End If

            ' 3) Python：pyproject.toml 的 requires-python / setup.py 的 python_requires
            v = FirstMatchInFile(projectDir, "pyproject.toml", "requires-python\s*=\s*[""']([^""']+)[""']")
            If v = "" Then v = FirstMatchInFile(projectDir, "setup.py", "python_requires\s*=\s*['""]([^'""]+)['""]")
            If v <> "" Then
                osText = "Windows / Linux / macOS"
                Return "Python " & v
            End If
            ' 有 requirements.txt / setup.py 但没有版本约束时，只标 Python
            If FindFile(projectDir, "requirements.txt") <> "" OrElse FindFile(projectDir, "setup.py") <> "" Then
                osText = "Windows / Linux / macOS"
                Return "Python"
            End If

            ' 4) Java / Maven：pom.xml 的 java.version 或 maven.compiler.source
            v = FirstMatchInFile(projectDir, "pom.xml", "<java\.version>([^<>]+)</java\.version>")
            If v = "" Then v = FirstMatchInFile(projectDir, "pom.xml", "<maven\.compiler\.source>([^<>]+)</maven\.compiler\.source>")
            If v <> "" Then
                osText = "Windows / Linux / macOS"
                Return "Java " & v
            End If
            If FindFile(projectDir, "pom.xml") <> "" Then
                osText = "Windows / Linux / macOS"
                Return "Java（Maven）"
            End If

            ' 5) Gradle / Android：compileSdk、minSdk、sourceCompatibility
            Dim compileSdk = FirstMatchInFile(projectDir, "build.gradle", "compileSdk(?:Version)?\s*[= ]\s*['""]?(\d+)['""]?")
            If compileSdk = "" Then compileSdk = FirstMatchInFile(projectDir, "build.gradle.kts", "compileSdk\s*=\s*(\d+)")
            Dim minSdk = FirstMatchInFile(projectDir, "build.gradle", "minSdk(?:Version)?\s*[= ]\s*['""]?(\d+)['""]?")
            If minSdk = "" Then minSdk = FirstMatchInFile(projectDir, "build.gradle.kts", "minSdk\s*=\s*(\d+)")
            Dim jvm = FirstMatchInFile(projectDir, "build.gradle", "sourceCompatibility\s*[= ]\s*['""]([^'""]+)['""]")
            If jvm = "" Then jvm = FirstMatchInFile(projectDir, "build.gradle.kts", "sourceCompatibility\s*=\s*.*?(\d+)")
            If compileSdk <> "" OrElse minSdk <> "" Then
                osText = "Android"
                Dim r = "Android（compileSdk " & If(compileSdk <> "", compileSdk, "-") &
                        "，minSdk " & If(minSdk <> "", minSdk, "-") & "）"
                Return r
            End If
            If FindFile(projectDir, "build.gradle") <> "" OrElse FindFile(projectDir, "build.gradle.kts") <> "" Then
                osText = "Windows / Linux / macOS"
                If jvm <> "" Then Return "Java " & jvm & "（Gradle）"
                Return "Java（Gradle）"
            End If

            ' 6) Flutter / Dart：pubspec.yaml 的 environment.sdk
            v = FirstMatchInFile(projectDir, "pubspec.yaml", "(?m)^\s*sdk\s*:\s*['""]?([^\r\n]+)['""]?")
            If v <> "" Then
                osText = "Android / iOS / Windows / macOS"
                Return "Flutter（Dart SDK " & v.Trim() & "）"
            End If

            ' 7) Go：go.mod 的 go 1.xx
            v = FirstMatchInFile(projectDir, "go.mod", "(?m)^\s*go\s+(\d+\.\d+(?:\.\d+)?)")
            If v <> "" Then
                osText = "Windows / Linux / macOS"
                Return "Go " & v
            End If

            ' 8) Rust：rust-toolchain 的 channel 或 Cargo.toml 的 edition
            v = FirstMatchInFile(projectDir, "rust-toolchain.toml", "channel\s*=\s*[""']([^""']+)[""']")
            If v = "" Then v = FirstMatchInFile(projectDir, "rust-toolchain", "([^\r\n]+)")
            Dim edition = FirstMatchInFile(projectDir, "Cargo.toml", "(?m)^\s*edition\s*=\s*""([^""]+)""")
            If v <> "" Then
                osText = "Windows / Linux / macOS"
                Return "Rust（" & v & If(edition <> "", "，edition " & edition, "") & "）"
            End If
            If edition <> "" Then
                osText = "Windows / Linux / macOS"
                Return "Rust（edition " & edition & "）"
            End If

            ' 9) PHP：composer.json 的 php 约束
            v = FirstMatchInFile(projectDir, "composer.json", """php""\s*:\s*""([^""]+)""")
            If v <> "" Then
                osText = "Windows / Linux / macOS"
                Return "PHP " & v
            End If

            ' 10) 兜底：只有 apk/aab 时按 Android 处理
            Dim pkg = FindLatestFile(projectDir, New String() {"*.apk", "*.aab"}, allowBuildDirs:=True)
            If pkg <> "" Then
                osText = "Android"
                Return "Android"
            End If

        Catch
            ' 探测失败不改动 README
        End Try
        Return ""
    End Function

    ''' <summary>把 TargetFramework(s) 解析成易读的运行时名与操作系统描述</summary>
    Private Function ParseDotNetTfm(tfmList As String, ByRef osText As String) As String
        Dim parts = tfmList.Split(";"c).Select(Function(s) s.Trim()).Where(Function(s) s <> "").ToList()
        Dim names As New List(Of String)()
        Dim platforms As New List(Of String)()

        For Each p In parts
            Dim lower = p.ToLowerInvariant()
            ' 平台后缀：-windows、-android、-ios、-macos、-linux、-browser 等
            Dim dash = lower.IndexOf("-"c)
            Dim platform = If(dash > 0, lower.Substring(dash + 1), "")
            Dim baseTfm = If(dash > 0, lower.Substring(0, dash), lower)
            Dim name = MapDotNetTfm(baseTfm)
            If Not names.Contains(name) Then names.Add(name)
            If platform <> "" AndAlso Not platforms.Contains(platform) Then platforms.Add(platform)
        Next

        ' 由平台后缀推断适用操作系统
        If platforms.Contains("windows") Then
            osText = "Windows 10 / 11"
        ElseIf platforms.Contains("android") Then
            osText = "Android"
        ElseIf platforms.Contains("ios") OrElse platforms.Contains("maccatalyst") Then
            osText = "iOS"
        ElseIf platforms.Contains("macos") Then
            osText = "macOS"
        ElseIf platforms.Contains("linux") Then
            osText = "Linux"
        ElseIf platforms.Count = 0 Then
            ' 不带平台后缀（如 net8.0）表示跨平台
            osText = "Windows / Linux / macOS"
        End If

        Dim runtime = String.Join(" / ", names)
        If platforms.Count > 0 Then
            runtime &= "（" & String.Join("、", platforms.Select(AddressOf MapPlatformName)) & "）"
        End If
        Return runtime
    End Function

    ''' <summary>把 TFM 的基础部分映射为易读名称（net10.0 → .NET 10.0；net48 → .NET Framework 4.8）</summary>
    Private Function MapDotNetTfm(tfm As String) As String
        ' net8.0 / net10.0 等新式 TFM
        Dim m = Regex.Match(tfm, "^net(\d+)\.(\d+)$")
        If m.Success Then Return ".NET " & m.Groups(1).Value & "." & m.Groups(2).Value
        ' net48 / net472 等 .NET Framework（第二位是次版本，其余为修订号）
        m = Regex.Match(tfm, "^net(\d)(\d{1,2})$")
        If m.Success Then
            Dim major = m.Groups(1).Value
            Dim minor = m.Groups(2).Value
            If minor.Length = 1 Then Return ".NET Framework " & major & "." & minor
            Return ".NET Framework " & major & "." & minor(0) & "." & minor.Substring(1)
        End If
        ' netstandard2.0 / netcoreapp3.1
        m = Regex.Match(tfm, "^netstandard(\d+\.\d+)$")
        If m.Success Then Return ".NET Standard " & m.Groups(1).Value
        m = Regex.Match(tfm, "^netcoreapp(\d+\.\d+)$")
        If m.Success Then Return ".NET Core " & m.Groups(1).Value
        Return tfm
    End Function

    ''' <summary>把 TFM 平台后缀映射为易读名称</summary>
    Private Function MapPlatformName(platform As String) As String
        Select Case platform.ToLowerInvariant()
            Case "windows" : Return "Windows"
            Case "android" : Return "Android"
            Case "ios" : Return "iOS"
            Case "macos" : Return "macOS"
            Case "maccatalyst" : Return "Mac Catalyst"
            Case "linux" : Return "Linux"
            Case "browser" : Return "WebAssembly"
            Case "tvos" : Return "tvOS"
            Case Else : Return platform
        End Select
    End Function

    ''' <summary>
    ''' 在指定小节内替换以 "- key" 开头的行；返回是否替换成功
    ''' onlyIfContains 非空时，只有当原行包含该文本才替换（避免覆盖用户自定义内容）
    ''' </summary>
    Private Function ReplaceSectionLine(lines As List(Of String), startIdx As Integer, endIdx As Integer,
                                        key As String, newLine As String, Optional onlyIfContains As String = "") As Boolean
        For i = startIdx + 1 To endIdx - 1
            If Regex.IsMatch(lines(i), "^\s*-\s*" & key & "\s*[:：]?") Then
                If onlyIfContains <> "" AndAlso Not lines(i).Contains(onlyIfContains) Then Return False
                lines(i) = newLine
                Return True
            End If
        Next
        Return False
    End Function

    ''' <summary>在指定小节末尾追加一行（跳过小节尾部的空行）</summary>
    Private Sub AppendLineToSection(lines As List(Of String), startIdx As Integer, endIdx As Integer, newLine As String)
        Dim lastContent = startIdx        ' 小节内最后一个非空行的位置
        For i = startIdx + 1 To endIdx - 1
            If lines(i).Trim() <> "" Then lastContent = i
        Next
        If lastContent = startIdx Then
            ' 小节是空的：标题下插入空行 + 内容 + 空行
            lines.InsertRange(startIdx + 1, New String() {"", newLine, ""})
        Else
            lines.Insert(lastContent + 1, newLine)
        End If
    End Sub

    ' ==================== README 应用图标自动导入 ====================
    ''' <summary>
    ''' 自动把项目图标导入项目目录（统一放到 docs/ 下，ico 会转成 PNG），
    ''' 并在 README 顶部插入图片引用，这样 GitHub 仓库页面就能直接看到程序图标
    ''' 说明：GitHub 不渲染 .ico，所以 ico 必须先转成 png
    ''' </summary>
    Private Sub UpdateReadmeIcon(projectDir As String)
        Try
            ' 1) 找图标：本地图片/图标文件优先，找不到再从 apk 里提取
            Dim relPath As String = ""
            Dim src = FindProjectIcon(projectDir)
            If src <> "" Then
                relPath = ImportProjectIcon(projectDir, src)
            Else
                relPath = TryExtractApkIcon(projectDir)
            End If
            If relPath = "" Then Exit Sub

            ' 2) 在 README 顶部插入或更新图标引用（幂等，不会重复插入）
            UpsertReadmeIconLine(relPath)

            ' 3) 提示 .gitignore 可能会把图标排除掉
            CheckIconIgnored(projectDir, relPath)
        Catch ex As Exception
            Log("==> [图标] 处理失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>
    ''' 查找项目图标：不做文件名白名单，只按扩展名匹配
    ''' 扩展名优先级：ico（程序/应用图标）→ png → jpg → svg
    ''' 先查项目根与常见资源目录（仅顶层），没有再浅层递归整个项目
    ''' </summary>
    Private Function FindProjectIcon(projectDir As String) As String
        Try
            Dim exts = New String() {"*.ico", "*.png", "*.jpg", "*.jpeg", "*.svg"}
            ' 常见资源目录（"" 表示项目根目录）
            Dim dirs = New String() {"", "Resources", "res", "assets", "icons", "icon",
                                     "images", "img", "docs", "public", "static"}

            ' 第一轮：根目录与常见资源目录下按扩展名找
            For Each d In dirs
                Dim dd = If(d = "", projectDir, Path.Combine(projectDir, d))
                If Not Directory.Exists(dd) Then Continue For
                For Each ext In exts
                    Dim files = Directory.GetFiles(dd, ext, SearchOption.TopDirectoryOnly)
                    If files.Length > 0 Then Return PickPreferredIcon(files)
                Next
            Next

            ' 第二轮：整个项目浅层递归（跳过 bin/obj/node_modules 等目录）
            For Each ext In New String() {"*.ico", "*.png"}
                Dim hit = FindFirstFileByPattern(projectDir, ext, 3)
                If hit <> "" Then Return hit
            Next
        Catch
        End Try
        Return ""
    End Function

    ''' <summary>
    ''' 从候选图标里挑最合适的：文件名含 icon / logo / app / favicon 的优先
    ''' 都没有则取第一个（文件名完全无法预测时的兜底）
    ''' </summary>
    Private Function PickPreferredIcon(files As String()) As String
        Dim hints = New String() {"icon", "logo", "app", "favicon"}
        For Each h In hints
            For Each f In files
                If Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(h) Then Return f
            Next
        Next
        Return files(0)
    End Function

    ''' <summary>在项目目录里浅层递归查找第一个匹配通配符的文件（跳过构建/依赖目录）</summary>
    Private Function FindFirstFileByPattern(projectDir As String, pattern As String, maxDepth As Integer) As String
        Try
            Dim dirs As New Queue(Of String)()
            dirs.Enqueue(projectDir)
            Dim depth = 0
            While dirs.Count > 0 AndAlso depth <= maxDepth
                Dim count = dirs.Count
                For i = 1 To count
                    Dim d = dirs.Dequeue()
                    Dim files = Directory.GetFiles(d, pattern, SearchOption.TopDirectoryOnly)
                    If files.Length > 0 Then Return PickPreferredIcon(files)
                    For Each subDir In Directory.GetDirectories(d)
                        If IsSkipDirectory(New DirectoryInfo(subDir).Name) Then Continue For
                        dirs.Enqueue(subDir)
                    Next
                Next
                depth += 1
            End While
        Catch
        End Try
        Return ""
    End Function

    ''' <summary>
    ''' 把图标导入到项目的 docs 目录，返回相对 README 的路径（如 docs/app.png）
    ''' 文件名沿用原图标名：ico 只换扩展名（xxx.ico → xxx.png），png/jpg/svg 原名复制
    ''' ico 会被转成 PNG（GitHub 不渲染 .ico）；png/jpg/svg 直接复制
    ''' 目标文件已存在且不比源文件旧时跳过，保证重复调用幂等
    ''' </summary>
    Private Function ImportProjectIcon(projectDir As String, srcPath As String) As String
        Try
            Dim ext = Path.GetExtension(srcPath).ToLowerInvariant()
            If ext <> ".ico" AndAlso ext <> ".png" AndAlso ext <> ".jpg" AndAlso
               ext <> ".jpeg" AndAlso ext <> ".svg" Then Return ""

            ' 文件名保持与原图标一致：ico → 同名 .png，其它格式连扩展名都不改
            Dim baseName = Path.GetFileNameWithoutExtension(srcPath)
            Dim destName = If(ext = ".ico", baseName & ".png", Path.GetFileName(srcPath))
            Dim destDir = Path.Combine(projectDir, "docs")
            Dim destPath = Path.Combine(destDir, destName)

            ' 已经导入过且图标没有更新 → 直接返回，避免重复写盘
            If File.Exists(destPath) AndAlso
               File.GetLastWriteTime(destPath) >= File.GetLastWriteTime(srcPath) Then
                Return "docs/" & destName
            End If

            If Not Directory.Exists(destDir) Then Directory.CreateDirectory(destDir)

            If ext = ".ico" Then
                ' ICO → PNG（GitHub 不渲染 ico）
                If Not ConvertIcoToPng(srcPath, destPath) Then Return ""
                Log("==> [图标] 已由 " & Path.GetFileName(srcPath) & " 生成 docs/" & destName)
            Else
                File.Copy(srcPath, destPath, True)
                Log("==> [图标] 已导入 docs/" & destName)
            End If

            Return "docs/" & destName
        Catch ex As Exception
            Log("==> [图标] 导入失败：" & ex.Message)
            Return ""
        End Try
    End Function

    ''' <summary>
    ''' 把 ico 转成 png。
    ''' 优先直接解析 ico 文件结构、取出最大的一帧：若该帧本身就是 PNG（Vista 风格 ico 常见），
    ''' 原样写出即可做到无损；否则（老式 BMP 帧）再用 GDI+ 渲染成 256×256。
    ''' 说明：内嵌 PNG 帧的 ico 用 Icon.ToBitmap() 会抛 "Requested range extends past the end of the array"，
    ''' 所以必须先走取帧这条路
    ''' </summary>
    Private Function ConvertIcoToPng(srcPath As String, destPath As String) As Boolean
        ' ---- 方式一：直接解析 ICO 结构取帧 ----
        Try
            Dim data = File.ReadAllBytes(srcPath)
            ' ICO 头：保留字(2) + 类型(2) + 帧数(2)，其后每帧 16 字节目录项
            If data.Length > 6 AndAlso BitConverter.ToUInt16(data, 2) = 1 Then
                Dim count = BitConverter.ToUInt16(data, 4)
                Dim bestSize = -1, bestOffset = 0, bestLen = 0
                For i = 0 To count - 1
                    Dim p = 6 + i * 16
                    If p + 16 > data.Length Then Exit For
                    Dim w = CInt(data(p)) : If w = 0 Then w = 256   ' 目录项里 0 表示 256
                    Dim len = CInt(BitConverter.ToUInt32(data, p + 8))
                    Dim off = CInt(BitConverter.ToUInt32(data, p + 12))
                    If w > bestSize AndAlso off >= 0 AndAlso len > 0 AndAlso off + len <= data.Length Then
                        bestSize = w : bestOffset = off : bestLen = len
                    End If
                Next
                ' PNG 帧以 0x89 'P' 'N' 'G' 开头 → 直接写出，画质无损
                If bestLen > 8 AndAlso data(bestOffset) = &H89 AndAlso data(bestOffset + 1) = &H50 AndAlso
                   data(bestOffset + 2) = &H4E AndAlso data(bestOffset + 3) = &H47 Then
                    File.WriteAllBytes(destPath, data.Skip(bestOffset).Take(bestLen).ToArray())
                    Return True
                End If
            End If
        Catch
            ' 解析失败则回退到 GDI+ 方式
        End Try

        ' ---- 方式二：老式 BMP 帧，用 GDI+ 渲染 ----
        Try
            Dim ico As Icon = Nothing
            Try
                ico = New Icon(srcPath, New Size(256, 256))   ' 多尺寸 ico 取最接近 256 的一帧
            Catch
                ico = New Icon(srcPath)                       ' 兜底：用默认尺寸
            End Try
            Using ico
                Using srcBmp = ico.ToBitmap()
                    Using bmp As New Bitmap(256, 256)
                        Using g = Graphics.FromImage(bmp)
                            g.InterpolationMode = Drawing2D.InterpolationMode.HighQualityBicubic
                            g.SmoothingMode = Drawing2D.SmoothingMode.HighQuality
                            g.Clear(Color.Transparent)
                            g.DrawImage(srcBmp, 0, 0, 256, 256)
                        End Using
                        bmp.Save(destPath, ImageFormat.Png)
                    End Using
                End Using
            End Using
            Return True
        Catch ex As Exception
            Log("==> [图标] ICO 转换失败：" & ex.Message)
        End Try
        Return False
    End Function

    ''' <summary>从 apk 里提取应用图标（aapt 查询图标路径 + 解压 apk），返回相对 README 的路径</summary>
    Private Function TryExtractApkIcon(projectDir As String) As String
        Try
            ' 项目里没有图标文件时，才尝试从 apk 包中提取
            Dim apk = FindLatestFile(projectDir, New String() {"*.apk"}, allowBuildDirs:=True)
            If apk = "" Then Return ""
            Dim aapt = FindAapt()
            If aapt = "" Then Return ""

            ' aapt dump badging 的输出里含 application-icon-480:'res/xxx.png' 这类条目，取分辨率最高的
            Dim output As String = ""
            Dim psi As New ProcessStartInfo() With {
                .FileName = aapt,
                .Arguments = $"dump badging ""{apk}""",
                .RedirectStandardOutput = True,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .StandardOutputEncoding = Encoding.UTF8
            }
            Using p As Process = Process.Start(psi)
                output = p.StandardOutput.ReadToEnd()
                p.WaitForExit()
            End Using

            Dim bestRes = -1
            Dim bestEntry As String = ""
            For Each m As Match In Regex.Matches(output, "application-icon-(\d+):'([^']+)'")
                Dim res As Integer
                If Integer.TryParse(m.Groups(1).Value, res) AndAlso res > bestRes Then
                    bestRes = res
                    bestEntry = m.Groups(2).Value
                End If
            Next
            If bestEntry = "" Then Return ""

            Dim destDir = Path.Combine(projectDir, "docs")
            If Not Directory.Exists(destDir) Then Directory.CreateDirectory(destDir)

            ' 文件名沿用 apk 内部图标名（如 ic_launcher.png）
            Dim entryName = Path.GetFileNameWithoutExtension(bestEntry) & ".png"
            Dim destPath = Path.Combine(destDir, entryName)

            ' 直接从 apk（zip）里读出该图标条目并存为 PNG
            Using zip = ZipFile.OpenRead(apk)
                Dim entry = zip.GetEntry(bestEntry)
                If entry Is Nothing Then Return ""
                Using s = entry.Open()
                    Using bmp As New Bitmap(s)
                        bmp.Save(destPath, ImageFormat.Png)
                    End Using
                End Using
            End Using

            Log("==> [图标] 已从 " & Path.GetFileName(apk) & " 提取图标 docs/" & entryName)
            Return "docs/" & entryName
        Catch
            Return ""
        End Try
    End Function

    ''' <summary>在 README 第一个一级标题后插入或更新应用图标引用（避免重复插入）</summary>
    Private Sub UpsertReadmeIconLine(relPath As String)
        If String.IsNullOrWhiteSpace(txtReadme.Text) Then Return
        Dim lines = New List(Of String)(txtReadme.Text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split(vbLf))
        Dim imgLine = $"<img src=""{relPath}"" width=""120"" alt=""应用图标"" />"

        ' 已存在本工具插入的图标行（按 alt="应用图标" 识别）→ 更新路径即可
        For i = 0 To lines.Count - 1
            If lines(i).Contains("alt=""应用图标""") Then
                If lines(i) = imgLine Then Return
                lines(i) = imgLine
                txtReadme.Text = String.Join(vbCrLf, lines)
                Return
            End If
        Next

        ' 用户自己已经引用过同一张图片 → 不重复插入
        For i = 0 To lines.Count - 1
            If lines(i).Contains(relPath) Then Return
        Next

        ' 插到第一个 # 标题之后
        Dim titleIdx = -1
        For i = 0 To lines.Count - 1
            If Regex.IsMatch(lines(i), "^#\s") Then
                titleIdx = i
                Exit For
            End If
        Next
        lines.InsertRange(If(titleIdx >= 0, titleIdx + 1, 0), New String() {"", imgLine})
        txtReadme.Text = String.Join(vbCrLf, lines)
        Log("==> [图标] README 顶部已插入应用图标")
    End Sub

    ''' <summary>检查 .gitignore 是否会忽略刚导入的图标，会的话给出警告</summary>
    Private Sub CheckIconIgnored(projectDir As String, relPath As String)
        Try
            Dim gi = Path.Combine(projectDir, ".gitignore")
            If Not File.Exists(gi) Then Return
            Dim ext = Path.GetExtension(relPath).ToLowerInvariant()
            For Each line In File.ReadAllLines(gi, Encoding.UTF8)
                Dim t = line.Trim()
                If t = "" OrElse t.StartsWith("#") Then Continue For
                ' 命中 "*<扩展名>"、"docs/"、"docs/*" 这类规则时图标不会被提交
                If t = "*" & ext OrElse t = "docs/" OrElse t = "docs/*" Then
                    Log("==> [警告] .gitignore 里的 """ & t & """ 会忽略 " & relPath & "，GitHub 上将看不到图标")
                    Return
                End If
            Next
        Catch
        End Try
    End Sub

    ''' <summary>判断是否属于 "1.0.0" 初始版本系列（主 1 次 0 修订 0）</summary>
    Private Function IsFirstReleaseVersion(v As String) As Boolean
        If String.IsNullOrWhiteSpace(v) Then Return False
        Dim parts = v.Split("."c)
        If parts.Length < 3 Then Return False
        If parts(0) <> "1" Then Return False
        If parts(1) <> "0" Then Return False
        If parts(2) <> "0" Then Return False
        Return True
    End Function

    ''' <summary>去掉版本号末尾的 .0（2.3.5.0 -> 2.3.5；2.3.5 -> 2.3；至少保留两段）</summary>
    Private Function TrimTrailingZero(v As String) As String
        If String.IsNullOrWhiteSpace(v) Then Return v
        Dim parts = v.Split("."c).ToList()
        While parts.Count > 2 AndAlso parts(parts.Count - 1) = "0"
            parts.RemoveAt(parts.Count - 1)
        End While
        Return String.Join(".", parts)
    End Function

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

    ' ==================== 项目版本号自动探测 ====================
    ''' <summary>
    ''' 在项目目录里自动探测版本号（只读，不写回），按优先级依次尝试：
    ''' VB/C# 工程 → AndroidManifest → Gradle → Flutter → Node → Maven → Rust →
    ''' Python → Info.plist → nuspec → apk/aab（aapt）→
    ''' 编译输出目录（bin\Release / bin\Debug 下的 exe/dll）→ 其它子目录的 exe/dll
    ''' 工程文件里没有版本号时，会回退读取编译产物的文件版本；全部都没有则返回空串，
    ''' 由调用方按初始版本（InitialVersion）处理。探测来源写入 versionSourceText 供界面提示
    ''' </summary>
    Private Function ReadProjectVersion(projectDir As String) As String
        versionSourceText = ""
        Try
            If String.IsNullOrWhiteSpace(projectDir) OrElse Not Directory.Exists(projectDir) Then Return ""

            Dim v As String = ""

            ' 1) .NET 工程文件：Version > AssemblyVersion > FileVersion > VersionPrefix
            For Each ext In New String() {"*.vbproj", "*.csproj", "*.fsproj"}
                Dim projFiles = Directory.GetFiles(projectDir, ext, SearchOption.TopDirectoryOnly)
                If projFiles.Length = 0 Then Continue For
                Dim content = File.ReadAllText(projFiles(0), Encoding.UTF8)
                v = GetXmlTagValue(content, "Version")
                If v = "" Then v = GetXmlTagValue(content, "AssemblyVersion")
                If v = "" Then v = GetXmlTagValue(content, "FileVersion")
                If v = "" Then v = GetXmlTagValue(content, "VersionPrefix")
                If v <> "" Then
                    versionSourceText = "来源：" & Path.GetFileName(projFiles(0))
                    Return v
                End If
            Next

            ' 2) AndroidManifest.xml：android:versionName="x"
            v = FirstMatchInFile(projectDir, "AndroidManifest.xml", "android:versionName\s*=\s*""([^""]+)""")
            If v <> "" Then
                versionSourceText = "来源：AndroidManifest.xml"
                Return v
            End If

            ' 3) Gradle（Android）：versionName "x" 或 versionName = "x"
            v = FirstMatchInFile(projectDir, "build.gradle", "versionName\s+['""]([^'""]+)['""]")
            If v = "" Then v = FirstMatchInFile(projectDir, "build.gradle.kts", "versionName\s*=\s*['""]([^'""]+)['""]")
            If v <> "" Then
                versionSourceText = "来源：build.gradle(.kts)"
                Return v
            End If

            ' 4) Flutter：pubspec.yaml 的 version: 1.2.3+4（+ 后是构建号，去掉）
            v = FirstMatchInFile(projectDir, "pubspec.yaml", "(?m)^\s*version\s*:\s*([^\s+#]+)")
            If v <> "" Then
                versionSourceText = "来源：pubspec.yaml"
                Return v
            End If

            ' 5) Node / 前端 / Electron：package.json 的 "version": "x"
            v = FirstMatchInFile(projectDir, "package.json", """version""\s*:\s*""([^""]+)""")
            If v <> "" Then
                versionSourceText = "来源：package.json"
                Return v
            End If

            ' 6) Maven：pom.xml 的第一个 <version>（跳过含 ${} 的占位符）
            v = FirstMatchInFile(projectDir, "pom.xml", "<version>([^$<>]+)</version>")
            If v <> "" Then
                versionSourceText = "来源：pom.xml"
                Return v
            End If

            ' 7) Rust：Cargo.toml 的 version = "x"
            v = FirstMatchInFile(projectDir, "Cargo.toml", "(?m)^\s*version\s*=\s*""([^""]+)""")
            If v <> "" Then
                versionSourceText = "来源：Cargo.toml"
                Return v
            End If

            ' 8) Python：pyproject.toml / setup.py / setup.cfg
            v = FirstMatchInFile(projectDir, "pyproject.toml", "(?m)^\s*version\s*=\s*""([^""]+)""")
            If v = "" Then v = FirstMatchInFile(projectDir, "setup.py", "version\s*=\s*['""]([^'""]+)['""]")
            If v = "" Then v = FirstMatchInFile(projectDir, "setup.cfg", "(?m)^\s*version\s*=\s*([^\r\n#]+)")
            If v <> "" Then
                versionSourceText = "来源：Python 打包配置"
                Return v
            End If

            ' 9) iOS / macOS：Info.plist 的 CFBundleShortVersionString
            v = FirstMatchInFile(projectDir, "Info.plist",
                                 "<key>CFBundleShortVersionString</key>\s*<string>([^<]+)</string>")
            If v <> "" Then
                versionSourceText = "来源：Info.plist"
                Return v
            End If

            ' 10) NuGet：*.nuspec 的 <version>
            For Each f In Directory.GetFiles(projectDir, "*.nuspec", SearchOption.TopDirectoryOnly)
                v = GetXmlTagValue(File.ReadAllText(f, Encoding.UTF8), "version")
                If v <> "" Then
                    versionSourceText = "来源：" & Path.GetFileName(f)
                    Return v
                End If
            Next

            ' 11) apk / aab：用 Android SDK 的 aapt(aapt2) dump badging 读取 versionName
            '     Android 产物在 app\build\outputs\apk 下，所以这里允许进入 build 目录
            Dim pkg = FindLatestFile(projectDir, New String() {"*.apk", "*.aab"}, allowBuildDirs:=True)
            If pkg <> "" Then
                v = TryGetAndroidPackageVersion(pkg)
                If v <> "" Then
                    versionSourceText = "来源：" & Path.GetFileName(pkg)
                    Return v
                End If
            End If

            ' 12) 编译输出目录里的 exe / dll：bin\Release\<tfm>\、bin\Debug\<tfm>\ 等
            '     工程文件里没写版本号时，编译产物的文件版本就是最可靠的来源
            Dim buildOut = FindLatestBuildOutput(projectDir)
            If buildOut <> "" Then
                v = TryGetFileVersion(buildOut)
                If v <> "" Then
                    versionSourceText = "来源：" & Path.GetFileName(buildOut) & "（编译输出）"
                    Return v
                End If
            End If

            ' 13) 兜底：项目其它子目录里的 exe / dll（仍然跳过依赖与构建垃圾目录）
            Dim bin = FindLatestFile(projectDir, New String() {"*.exe", "*.dll"})
            If bin <> "" Then
                v = TryGetFileVersion(bin)
                If v <> "" Then
                    versionSourceText = "来源：" & Path.GetFileName(bin)
                    Return v
                End If
            End If

            ' 14) 全部都没有：返回空串，由调用方按初始版本处理
            versionSourceText = "未识别到版本号，按初始版本 " & InitialVersion & " 处理"

        Catch ex As Exception
            versionSourceText = "版本号读取失败：" & ex.Message
        End Try
        Return ""
    End Function

    ''' <summary>取 &lt;tag&gt;...&lt;/tag&gt; 的值</summary>
    Private Function GetXmlTagValue(xml As String, tag As String) As String
        Dim m = Regex.Match(xml, "<" & tag & ">(.*?)</" & tag & ">", RegexOptions.IgnoreCase)
        If m.Success Then Return m.Groups(1).Value.Trim()
        Return ""
    End Function

    ''' <summary>在项目目录（含子目录，跳过构建/依赖目录）里找指定文件名，并用正则第一个分组取值</summary>
    Private Function FirstMatchInFile(projectDir As String, fileName As String, pattern As String) As String
        Try
            Dim f = FindFile(projectDir, fileName)
            If f = "" Then Return ""
            Dim content = File.ReadAllText(f, Encoding.UTF8)
            Dim m = Regex.Match(content, pattern)
            If m.Success Then Return m.Groups(1).Value.Trim()
        Catch
            ' 读取或匹配失败按“没找到”处理
        End Try
        Return ""
    End Function

    ''' <summary>在项目目录里递归查找指定文件名（最多 5 层，跳过 bin/obj/node_modules 等目录）</summary>
    Private Function FindFile(projectDir As String, fileName As String) As String
        Try
            Dim dirs As New Queue(Of String)()
            dirs.Enqueue(projectDir)
            Dim depth = 0
            While dirs.Count > 0 AndAlso depth < 5
                Dim count = dirs.Count
                For i = 1 To count
                    Dim d = dirs.Dequeue()
                    Dim hit = Path.Combine(d, fileName)
                    If File.Exists(hit) Then Return hit
                    For Each subDir In Directory.GetDirectories(d)
                        Dim nm = New DirectoryInfo(subDir).Name
                        If IsSkipDirectory(nm) Then Continue For
                        dirs.Enqueue(subDir)
                    Next
                Next
                depth += 1
            End While
        Catch
        End Try
        Return ""
    End Function

    ''' <summary>
    ''' 在项目目录里找最近修改的匹配文件（apk/aab/exe/dll 等），最多下探 6 层
    ''' allowBuildDirs=True 时允许进入 bin/build/out 等构建目录（找产物时需要）
    ''' </summary>
    Private Function FindLatestFile(projectDir As String, patterns As String(), Optional allowBuildDirs As Boolean = False) As String
        Dim best = ""
        Dim bestTime = DateTime.MinValue
        Try
            Dim dirs As New Queue(Of String)()
            dirs.Enqueue(projectDir)
            Dim depth = 0
            While dirs.Count > 0 AndAlso depth < 6
                Dim count = dirs.Count
                For i = 1 To count
                    Dim d = dirs.Dequeue()
                    For Each pat In patterns
                        For Each f In Directory.GetFiles(d, pat, SearchOption.TopDirectoryOnly)
                            Dim t = File.GetLastWriteTime(f)
                            If t > bestTime Then
                                bestTime = t
                                best = f
                            End If
                        Next
                    Next
                    For Each subDir In Directory.GetDirectories(d)
                        If IsSkipDirectory(New DirectoryInfo(subDir).Name, allowBuildDirs) Then Continue For
                        dirs.Enqueue(subDir)
                    Next
                Next
                depth += 1
            End While
        Catch
        End Try
        Return best
    End Function

    ''' <summary>
    ''' 在项目编译输出目录里找最新的 exe/dll（工程文件里没写版本号时的主要来源）
    ''' 依次尝试 bin\Release → bin\Debug → bin → publish → out
    ''' 目标框架目录名（net10.0-windows / net8.0 等）不固定，这里递归扫描而不硬编码
    ''' </summary>
    Private Function FindLatestBuildOutput(projectDir As String) As String
        Dim roots As New List(Of String)()
        Dim binDir = Path.Combine(projectDir, "bin")
        If Directory.Exists(binDir) Then
            roots.Add(Path.Combine(binDir, "Release"))   ' 优先 Release 产物
            roots.Add(Path.Combine(binDir, "Debug"))
            roots.Add(binDir)                            ' 兜底：bin 下的其它布局
        End If
        roots.Add(Path.Combine(projectDir, "publish"))   ' dotnet publish 的输出
        roots.Add(Path.Combine(projectDir, "out"))       ' 部分工具的发布目录

        For Each root In roots
            If Not Directory.Exists(root) Then Continue For
            ' 优先找主程序 exe；类库项目只有 dll 时再退而求其次
            Dim hit = FindLatestFileNoSkip(root, New String() {"*.exe"}, 3)
            If hit = "" Then hit = FindLatestFileNoSkip(root, New String() {"*.exe", "*.dll"}, 3)
            If hit <> "" Then Return hit
        Next
        Return ""
    End Function

    ''' <summary>在指定根目录下递归查找最近修改的匹配文件（不过滤任何目录，专用于构建输出目录）</summary>
    Private Function FindLatestFileNoSkip(root As String, patterns As String(), maxDepth As Integer) As String
        Dim best = ""
        Dim bestTime = DateTime.MinValue
        Try
            Dim dirs As New Queue(Of String)()
            dirs.Enqueue(root)
            Dim depth = 0
            While dirs.Count > 0 AndAlso depth <= maxDepth
                Dim count = dirs.Count
                For i = 1 To count
                    Dim d = dirs.Dequeue()
                    ' 排除中间产物目录，避免取到 obj/refs 之类的临时 dll
                    If New DirectoryInfo(d).Name.Equals("ref", StringComparison.OrdinalIgnoreCase) Then Continue For
                    For Each pat In patterns
                        For Each f In Directory.GetFiles(d, pat, SearchOption.TopDirectoryOnly)
                            Dim t = File.GetLastWriteTime(f)
                            If t > bestTime Then
                                bestTime = t
                                best = f
                            End If
                        Next
                    Next
                    For Each subDir In Directory.GetDirectories(d)
                        dirs.Enqueue(subDir)
                    Next
                Next
                depth += 1
            End While
        Catch
        End Try
        Return best
    End Function

    ''' <summary>读取 exe/dll 的文件版本号（FileVersion 优先，其次 ProductVersion）</summary>
    Private Function TryGetFileVersion(filePath As String) As String
        Try
            Dim fvi = FileVersionInfo.GetVersionInfo(filePath)
            Dim v = If(fvi.FileVersion, fvi.ProductVersion)
            If Not String.IsNullOrWhiteSpace(v) Then Return v.Trim()
        Catch
            ' 单个文件读取失败不影响后续探测
        End Try
        Return ""
    End Function

    ''' <summary>
    ''' 版本探测时需要跳过的目录（依赖、版本控制等）
    ''' includeBuildDirs=False 时额外跳过 bin/obj/build/dist/out（找源配置文件时避免误读生成物）
    ''' </summary>
    Private Function IsSkipDirectory(name As String, Optional includeBuildDirs As Boolean = False) As Boolean
        Dim skip As New List(Of String) From {
            "node_modules", "packages", "vendor", "Pods", ".gradle", ".git", ".vs"
        }
        If Not includeBuildDirs Then
            skip.AddRange(New String() {"bin", "obj", "build", "dist", "out"})
        End If
        Return skip.Contains(name) OrElse name.StartsWith(".")
    End Function

    ''' <summary>用 Android SDK 的 aapt / aapt2 读取 apk、aab 的 versionName</summary>
    Private Function TryGetAndroidPackageVersion(packageFile As String) As String
        Try
            Dim aapt = FindAapt()
            If aapt = "" Then Return ""
            Dim psi As New ProcessStartInfo() With {
                .FileName = aapt,
                .Arguments = $"dump badging ""{packageFile}""",
                .RedirectStandardOutput = True,
                .UseShellExecute = False,
                .CreateNoWindow = True,
                .StandardOutputEncoding = Encoding.UTF8
            }
            Using p As Process = Process.Start(psi)
                Dim output = p.StandardOutput.ReadToEnd()
                p.WaitForExit()
                ' 输出形如：package: name='xxx' versionCode='12' versionName='1.2.3'
                Dim m = Regex.Match(output, "versionName='([^']+)'")
                If m.Success Then Return m.Groups(1).Value.Trim()
            End Using
        Catch
        End Try
        Return ""
    End Function

    ''' <summary>查找可用的 aapt.exe / aapt2.exe（优先 ANDROID_HOME / ANDROID_SDK_ROOT 环境变量）</summary>
    Private Function FindAapt() As String
        Try
            Dim roots As New List(Of String)()
            For Each evn In New String() {"ANDROID_HOME", "ANDROID_SDK_ROOT"}
                Dim val = Environment.GetEnvironmentVariable(evn)
                If Not String.IsNullOrWhiteSpace(val) Then roots.Add(val)
            Next
            ' 默认 SDK 安装位置兜底
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                   "Android", "Sdk"))

            For Each root In roots
                If Not Directory.Exists(root) Then Continue For
                Dim bt = Path.Combine(root, "build-tools")
                If Not Directory.Exists(bt) Then Continue For
                ' 取版本号最大的一套 build-tools（里面的 aapt 支持的格式更新）
                Dim versions = Directory.GetDirectories(bt).
                    OrderByDescending(Function(x) New DirectoryInfo(x).Name).ToList()
                For Each ver In versions
                    For Each exe In New String() {"aapt.exe", "aapt2.exe"}
                        Dim p = Path.Combine(ver, exe)
                        If File.Exists(p) Then Return p
                    Next
                Next
            Next
        Catch
        End Try
        Return ""
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

    ''' <summary>写入配置（不保存 README；README 在按钮和关闭时单独写）</summary>
    Private Sub SaveConfig(Optional IsFormClosed As Boolean = False)
        '如果是窗体关闭时且工程路径为空，则不保存配置，避免覆盖之前的有效配置
        If IsFormClosed AndAlso String.IsNullOrWhiteSpace(txtProject.Text) Then
            Return
        End If

        '先保存 README 到磁盘，避免关闭时丢失
        WriteReadmeToDisk(IsFormClosed)

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
        Catch ex As Exception
            Log("保存配置失败：" & ex.Message)
        End Try

    End Sub

    ''' <summary>把文本框里的 README 写回上次加载的项目路径</summary>
    Private Sub WriteReadmeToDisk(Optional IsFormClosed As Boolean = False)
        Dim projectPath = txtProject.Text.Trim()
        If projectPath = "" OrElse Not Directory.Exists(projectPath) Then
            MessageBox.Show("请先选择有效的项目路径！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If String.IsNullOrWhiteSpace(txtReadme.Text) Then Return
        Dim readmePath = Path.Combine(projectPath, "README.md")

        Try
            File.WriteAllText(readmePath, txtReadme.Text, New UTF8Encoding(False))
            Log($"==> [README] 已写入 {readmePath}")

            If Not IsFormClosed Then   '非关闭窗体时，写入后立即重新读取，确保编码和换行一致
                ' 重新读取，确保编码和换行一致
                txtReadme.Text = File.ReadAllText(readmePath, Encoding.UTF8)
            End If

        Catch ex As Exception
            Log("==> [README] 写入失败：" & ex.Message)
        End Try
    End Sub

    ''' <summary>窗体关闭时保存配置和 README</summary>
    Private Sub MainForm_FormClosing(sender As Object, e As FormClosingEventArgs)
        SaveConfig(IsFormClosed:=True)
    End Sub

    ' ==================== 推送主流程 ====================
    ''' <summary>推送按钮：init → add → commit → tag → push（版本号只读自动探测，README 版本自动同步）</summary>
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

        ' 推送前重新探测一次版本号（版本号只读，保证与项目实际情况一致）
        Dim detectedVer = ReadProjectVersion(projectPath)
        If detectedVer <> "" Then
            txtVersion.Text = detectedVer
            lblVersionInfo.Text = versionSourceText
        Else
            ' 工程文件与编译产物里都没有版本信息：按初始版本处理
            txtVersion.Text = InitialVersion
            lblVersionInfo.Text = versionSourceText
        End If
        ' tag 名为空时用当前版本号生成默认值（初始版本即 v1.0.0）
        If chkTag.Checked AndAlso txtTag.Text.Trim() = "" Then
            txtTag.Text = BuildTagName(txtVersion.Text.Trim())
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

            ' README.md：写入磁盘前统一整理（占位符替换 + 目录结构刷新 + 版本号同步）
            If chkReadme.Checked Then
                Dim readmePath = Path.Combine(projectPath, "README.md")
                Dim projName = New DirectoryInfo(projectPath).Name
                If String.IsNullOrWhiteSpace(txtReadme.Text) Then txtReadme.Text = DefaultReadme(projName)
                ' 替换 {项目名}/{用户名} 占位符，并把目录结构刷新为推送时的真实结构
                NormalizeReadme(projectPath, projName)
                ' 自动把探测到的版本号同步进 README（新版本号会插入一条更新记录）
                If detectedVer <> "" Then AutoSyncReadmeVersion(detectedVer)

                Log("==> 写入 README.md")
                File.WriteAllText(readmePath, txtReadme.Text, New UTF8Encoding(False))
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

            ' ---- 打 tag（保留 Git 仓库原有 tag，只创建/更新当前这一个）----
            If chkTag.Checked Then
                Dim tagName = txtTag.Text.Trim()
                If tagName = "" Then
                    ' 未填 tag 名时用版本号生成默认值（取前三段，加 v 前缀）
                    tagName = BuildTagName(txtVersion.Text.Trim())
                    txtTag.Text = tagName
                End If

                If tagName <> "" Then
                    ' 本地同名 tag 若已存在则强制重新指向当前提交（不影响其他任何 tag）
                    If Await LocalTagExistsAsync(gitExe, projectPath, tagName) Then
                        Log($"==> git tag -f {tagName}（本地同名 tag 已存在，重新指向当前提交）")
                        Await RunGitAsync(gitExe, projectPath, $"tag -f ""{tagName}""", ignoreError:=True)
                    Else
                        Log($"==> git tag {tagName}")
                        Await RunGitAsync(gitExe, projectPath, $"tag ""{tagName}""", ignoreError:=True)
                    End If
                End If
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
                ' 只强制推送当前这一个 tag（不会影响仓库里的其他 tag）
                Log($"==> git push origin {txtTag.Text.Trim()}（仅推送当前 tag，保留仓库原有 tag）")
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

    ''' <summary>判断本地仓库里是否已存在某个 tag（用于决定是否用 -f 重新指向）</summary>
    Private Async Function LocalTagExistsAsync(gitExe As String, workDir As String, tagName As String) As Task(Of Boolean)
        Dim exists As Boolean = False
        Await Task.Run(Sub()
                           Try
                               Dim psi As New ProcessStartInfo() With {
                                   .FileName = gitExe,
                                   .Arguments = $"tag -l ""{tagName}""",
                                   .WorkingDirectory = workDir,
                                   .RedirectStandardOutput = True,
                                   .UseShellExecute = False,
                                   .CreateNoWindow = True
                               }
                               Using p As Process = Process.Start(psi)
                                   Dim output = p.StandardOutput.ReadToEnd()
                                   p.WaitForExit()
                                   exists = output.Split({ControlChars.Cr, ControlChars.Lf}, StringSplitOptions.RemoveEmptyEntries).
                                                   Any(Function(s) s.Trim() = tagName)
                               End Using
                           Catch
                               ' 查询失败按“不存在”处理，走普通 tag 创建
                           End Try
                       End Sub)
        Return exists
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