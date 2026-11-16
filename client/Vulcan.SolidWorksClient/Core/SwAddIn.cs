using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using SolidWorksTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Vulcan.SolidWorksClient.Services;
using Vulcan.SolidWorksClient.UI;

namespace VulcanAddin
{
    [Guid("03D4BCE0-7A21-4660-82A1-4CEEF0FBD651"), ComVisible(true)]
    [SwAddin(Description = "Vulcan AI For SolidWorks", Title = "Vulcan", LoadAtStartup = true)]
    public class SwAddIn : ISwAddin
    {
        #region 核心成员变量
        private ISldWorks _swApp = null;
        private ICommandManager _cmdMgr = null;
        private int _addinCookieID;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr child, IntPtr parent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);

        private Window _aiMainWindow = null;
        private TaskpaneView _taskPaneView = null;
        private TaskPaneView _taskPaneControl = null;

        public int MainCmdGroupID = 5001;
        public int[] MainItemIds = new[] { 1002, 1003, 1004, 1005, 1006, 1007, 1008, 1009 };
        #endregion

        #region 属性封装
        public ISldWorks SwApp => _swApp;
        #endregion

        #region ISwAddin 核心接口实现
        /// <summary>
        /// 插件加载时触发
        /// </summary>
        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                // 统一转换为官方SDK的ISldWorks，和SwModeler完全匹配
                _swApp = (ISldWorks)ThisSW;
                _addinCookieID = Cookie;

                // 设置回调信息
                _swApp.SetAddinCallbackInfo(0, this, _addinCookieID);
                _cmdMgr = _swApp.GetCommandManager(_addinCookieID);

                // 创建工具栏与任务窗格
                CreateBasicCommandManager();
                CreateTaskPane();

                return true;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"插件加载失败：{ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 插件卸载时触发
        /// </summary>
        public bool DisconnectFromSW()
        {
            try
            {
                // 关闭AI窗口与任务窗格
                _aiMainWindow?.Close();
                _aiMainWindow = null;
                try
                {
                    _taskPaneView?.DeleteView();
                }
                catch (Exception ex)
                {
                    Logger.Warning("删除任务窗格失败", ex);
                }
                _taskPaneControl?.Dispose();
                _taskPaneControl = null;
                _taskPaneView = null;

                // 释放COM对象
                if (_cmdMgr != null)
                {
                    Marshal.ReleaseComObject(_cmdMgr);
                    _cmdMgr = null;
                }
                if (_swApp != null)
                {
                    Marshal.ReleaseComObject(_swApp);
                    _swApp = null;
                }

                // 强制GC回收COM资源
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                GC.WaitForPendingFinalizers();

                return true;
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"插件卸载失败：{ex.Message}");
                return false;
            }
        }
        #endregion

        #region 核心逻辑：创建AI窗口工具栏
        private void CreateBasicCommandManager()
        {
            try
            {
                // 支持的文档类型：零件/装配/工程图
                int[] docTypes = new[]
                {
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swDocumentTypes_e.swDocDRAWING,
                    (int)swDocumentTypes_e.swDocPART
                };

                #region 1. 创建命令组
                ICommandGroup mainCmdGroup;
                int cmdGroupErr = 0;
                bool ignorePrevious = false;

                // 读取注册表历史ID，判断是否需要重置
                bool hasRegistryData = _cmdMgr.GetGroupDataFromRegistry(MainCmdGroupID, out object registryIDs);
                ignorePrevious = hasRegistryData ? !CompareIDs((int[])registryIDs, MainItemIds) : true;

                // 创建命令组
                mainCmdGroup = _cmdMgr.CreateCommandGroup2(
                    MainCmdGroupID,
                    "Vulcan AI",
                    "AI建模助手",
                    string.Empty,
                    -1,
                    ignorePrevious,
                    ref cmdGroupErr
                );

                // 命令项类型：同时显示在菜单和工具栏
                int menuToolbarType = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);

                var commandDefinitions = new[]
                {
                    new { Id = 1002, Menu = "打开Vulcan面板", Hint = "显示或打开Vulcan AI面板", Label = "面板", Icon = "pane" },
                    new { Id = 1003, Menu = "圆柱示例", Hint = "填充圆柱建模示例", Label = "圆柱", Icon = "cylinder" },
                    new { Id = 1004, Menu = "安装板示例", Hint = "填充安装板与四孔示例", Label = "安装板", Icon = "plate" },
                    new { Id = 1005, Menu = "快速设置", Hint = "配置服务端与模型提供商", Label = "设置", Icon = "settings" },
                    new { Id = 1006, Menu = "测试连接", Hint = "检查Vulcan服务端连通性", Label = "连接", Icon = "health" },
                    new { Id = 1007, Menu = "运行状态", Hint = "查看最近操作与连接状态", Label = "状态", Icon = "status" },
                    new { Id = 1008, Menu = "打开日志", Hint = "打开客户端日志目录", Label = "日志", Icon = "logs" },
                    new { Id = 1009, Menu = "关于Vulcan", Hint = "查看版本与支持范围", Label = "关于", Icon = "about" }
                };

                // 图标条（20/32/40/64/96/128 六档）：每个命令占据条内一个方格，
                // ImageListIndex 使用命令序号。主图标使用品牌图标。
                string[] iconSizes = { "20", "32", "40", "64", "96", "128" };
                string iconDir = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                    "icons");
                List<string> brandIcons = new List<string>();
                List<string> iconStrips = new List<string>();
                foreach (string size in iconSizes)
                {
                    string brandIcon = Path.Combine(iconDir, "icon_" + size + ".png");
                    if (File.Exists(brandIcon))
                    {
                        brandIcons.Add(brandIcon);
                    }
                    string strip = Path.Combine(iconDir, "commands", "strip_" + size + ".png");
                    if (File.Exists(strip))
                    {
                        iconStrips.Add(strip);
                    }
                }

                bool iconStripsReady = iconStrips.Count == iconSizes.Length;
                try
                {
                    if (brandIcons.Count > 0)
                    {
                        mainCmdGroup.MainIconList = brandIcons.ToArray();
                    }
                    if (iconStripsReady)
                    {
                        mainCmdGroup.IconList = iconStrips.ToArray();
                        // TextBelow（大按钮）在部分 SolidWorks 版本中仍读取旧图标列表。
                        mainCmdGroup.SmallIconList = Path.Combine(iconDir, "commands", "strip_20.png");
                        mainCmdGroup.LargeIconList = Path.Combine(iconDir, "commands", "strip_32.png");
                    }
                    else if (brandIcons.Count > 0)
                    {
                        Logger.Warning("命令图标条缺失，回退到品牌图标");
                        mainCmdGroup.IconList = brandIcons.ToArray();
                    }
                }
                catch (Exception iconEx)
                {
                    Logger.Warning("设置插件图标失败", iconEx);
                }

                List<int> cmdIndexes = new List<int>();
                for (int index = 0; index < commandDefinitions.Length; index++)
                {
                    var command = commandDefinitions[index];
                    int imageIndex = iconStripsReady ? index : 0;
                    cmdIndexes.Add(mainCmdGroup.AddCommandItem2(
                        command.Menu,
                        -1,
                        command.Hint,
                        command.Label,
                        imageIndex,
                        $"FunctionProxy({command.Id})",
                        $"EnableFunction({command.Id})",
                        command.Id,
                        menuToolbarType));
                }

                // 启用工具栏和菜单
                mainCmdGroup.HasToolbar = true;
                mainCmdGroup.HasMenu = true;
                #endregion

                #region 2. 激活命令组并创建 Ribbon 标签页
                if (!mainCmdGroup.Activate())
                {
                    Logger.Warning("Vulcan 命令组激活失败");
                }

                foreach (int docType in docTypes)
                {
                    CommandTab legacyTools = _cmdMgr.GetCommandTab(docType, "Vulcan Tools");
                    if (legacyTools != null)
                    {
                        _cmdMgr.RemoveCommandTab(legacyTools);
                    }

                    CommandTab cmdTab = _cmdMgr.GetCommandTab(docType, "Vulcan AI");
                    if (cmdTab != null)
                    {
                        try
                        {
                            object[] boxes = cmdTab.CommandTabBoxes() as object[];
                            if (boxes != null)
                            {
                                foreach (object box in boxes)
                                {
                                    cmdTab.RemoveCommandTabBox(box as CommandTabBox);
                                }
                            }
                            _cmdMgr.RemoveCommandTab(cmdTab);
                        }
                        catch (Exception clearEx)
                        {
                            Logger.Warning("清理旧 Vulcan AI 标签页失败", clearEx);
                        }
                    }

                    cmdTab = _cmdMgr.AddCommandTab(docType, "Vulcan AI");
                    if (cmdTab == null)
                    {
                        Logger.Warning("创建 Vulcan AI 标签页失败，docType=" + docType);
                        continue;
                    }

                    // 每个命令独立成一个分组，保证 8 个命令从左到右单行排开，
                    // 不会像单个大分组那样被 SolidWorks 折成多行网格。
                    // 注意：SW2025 SP0 中 swCommandTabButton_TextBelow（大按钮）
                    // 即使提供 SmallIconList/LargeIconList 也会渲染为空白，
                    // 因此这里统一使用 TextHorizontal，图标在左、文字在右。
                    int[][] commandGroups =
                    {
                        new[] { 0 },
                        new[] { 1 },
                        new[] { 2 },
                        new[] { 3 },
                        new[] { 4 },
                        new[] { 5 },
                        new[] { 6 },
                        new[] { 7 }
                    };

                    foreach (int[] group in commandGroups)
                    {
                        List<int> tabCommandIds = new List<int>();
                        List<int> tabTextStyles = new List<int>();
                        foreach (int position in group)
                        {
                            tabCommandIds.Add(mainCmdGroup.get_CommandID(cmdIndexes[position]));
                            tabTextStyles.Add(
                                (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextHorizontal);
                        }

                        CommandTabBox tabBox = cmdTab.AddCommandTabBox();
                        bool added = tabBox.AddCommands(
                            tabCommandIds.ToArray(),
                            tabTextStyles.ToArray());
                        Logger.Info(
                            "Vulcan AI 标签页分组 docType=" + docType +
                            " ids=" + string.Join(",", tabCommandIds) +
                            " added=" + added);
                    }

                    cmdTab.Visible = true;
                    cmdTab.Active = true;
                }
                #endregion
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"创建工具栏失败：{ex.Message}\n{ex.StackTrace}");
            }
        }
        #endregion

        private void CreateTaskPane()
        {
            try
            {
                string iconPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty,
                    "icons",
                    "icon_32.png");
                _taskPaneView = _swApp.CreateTaskpaneView2(iconPath, "Vulcan AI");
                if (_taskPaneView == null)
                {
                    Logger.Warning("任务窗格创建失败，使用模态面板作为回退");
                    return;
                }

                _taskPaneControl = new TaskPaneView(new SwModeler(_swApp));
                _taskPaneControl.CreateControl();
                IntPtr handle = _taskPaneControl.Handle;
                IntPtr paneHandle = new IntPtr(_taskPaneView.GetTaskpaneViewWnd());
                IntPtr previousParent = SetParent(handle, paneHandle);
                bool shown;
                if (previousParent != IntPtr.Zero)
                {
                    MoveWindow(handle, 0, 0, 320, 700, true);
                    shown = true;
                }
                else
                {
                    shown = IntPtr.Size == 8
                        ? _taskPaneView.DisplayWindowFromHandlex64(handle.ToInt64())
                        : _taskPaneView.DisplayWindowFromHandle(handle.ToInt32());
                }
                if (!shown)
                {
                    Logger.Warning("任务窗格无法承载面板，使用模态面板作为回退");
                }
                else
                {
                    Logger.Info("任务窗格已创建");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("创建任务窗格失败", ex);
                _taskPaneView = null;
                _taskPaneControl = null;
            }
        }

        #region 核心功能：打开AI生成窗口
        /// <summary>
        /// 打开AI生成窗口，传递和SwModeler完全兼容的ISldWorks实例
        /// </summary>
        private void ActivateVulcanTab()
        {
            try
            {
                int docType = (int)swDocumentTypes_e.swDocPART;
                ModelDoc2 activeDoc = _swApp.ActiveDoc as ModelDoc2;
                if (activeDoc != null)
                {
                    docType = activeDoc.GetType();
                }

                CommandTab tab = _cmdMgr.GetCommandTab(docType, "Vulcan AI");
                if (tab != null)
                {
                    tab.Visible = true;
                    tab.Active = true;
                    Logger.Info("Vulcan AI 标签页已激活 docType=" + docType);
                }
            }
            catch (Exception ex)
            {
                Logger.Warning("激活 Vulcan AI 标签页失败", ex);
            }
        }

        private void OpenMainWindow(string prompt = null)
        {
            ActivateVulcanTab();
            if (_taskPaneView != null && _taskPaneControl != null)
            {
                _taskPaneView.ShowView();
                if (!string.IsNullOrEmpty(prompt))
                {
                    _taskPaneControl.SetPrompt(prompt);
                    _taskPaneControl.SetStatus("已填入示例，点击开始生成。");
                    VulcanStatus.ReportAction("已填入示例");
                }
                return;
            }

            try
            {
                Logger.Info("正在打开Vulcan AI窗口...");
                // 传递官方SDK的ISldWorks，和SwModeler完全匹配，无类型冲突
                var modeler = new SwModeler(_swApp);
                ShowWpfDialog(() =>
                {
                    var mainWindow = new MainWindow(modeler);
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        mainWindow.SetPrompt(prompt);
                    }
                    return mainWindow;
                });
                Logger.Info("Vulcan AI窗口已打开");
            }
            catch (Exception ex)
            {
                Logger.Error("打开Vulcan窗口失败", ex);
                System.Windows.MessageBox.Show($"打开窗口失败：{ex.Message}", "错误");
            }
        }

        private IntPtr GetOwnerHwnd()
        {
            try
            {
                Frame frame = _swApp.Frame() as Frame;
                return frame == null ? IntPtr.Zero : new IntPtr(frame.GetHWnd());
            }
            catch (Exception ex)
            {
                Logger.Warning("获取SolidWorks窗口句柄失败", ex);
                return IntPtr.Zero;
            }
        }

        private void OpenSettings()
        {
            if (ShowWpfDialog(() => new SettingsWindow()))
            {
                ReportStatus("设置已保存。");
            }
        }

        private void OpenStatusWindow()
        {
            ShowWpfDialog(() => new StatusWindow());
        }

        private bool ShowWpfDialog(Func<Window> factory)
        {
            try
            {
                if (_taskPaneControl != null)
                {
                    return _taskPaneControl.PanelDispatcher.Invoke(() =>
                    {
                        Window window = factory();
                        new WindowInteropHelper(window).Owner = GetOwnerHwnd();
                        return window.ShowDialog() == true;
                    });
                }

                bool result = false;
                var thread = new System.Threading.Thread(() =>
                {
                    Window window = factory();
                    new WindowInteropHelper(window).Owner = GetOwnerHwnd();
                    result = window.ShowDialog() == true;
                });
                thread.SetApartmentState(System.Threading.ApartmentState.STA);
                thread.IsBackground = true;
                thread.Start();
                thread.Join();
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("打开WPF窗口失败", ex);
                ReportStatus("打开窗口失败：" + ex.Message);
                return false;
            }
        }

        private void OpenLogFolder()
        {
            try
            {
                string folder = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                    "Vulcan_Logs");
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", folder);
                VulcanStatus.ReportAction("打开日志目录");
            }
            catch (Exception ex)
            {
                ReportStatus("无法打开日志目录：" + ex.Message);
            }
        }

        private void CheckConnection()
        {
            try
            {
                string body = Task.Run(() => new ApiClient().CheckHealthAsync())
                    .GetAwaiter()
                    .GetResult();
                VulcanStatus.ReportHealth(true, "连接正常：" + body);
                ReportStatus("服务端连接正常");
            }
            catch (Exception ex)
            {
                VulcanStatus.ReportHealth(false, "连接失败：" + ex.Message);
                ReportStatus("连接失败：" + ex.Message);
            }
        }

        private void ShowAbout()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            RunOnUiThread(() =>
            {
                System.Windows.MessageBox.Show(
                    "Vulcan AI " + version + "\n\n" +
                    "用自然语言生成 SolidWorks 参数化特征。\n" +
                    "支持零件、装配体与工程图操作。\n\n" +
                    "GPLv3 · https://github.com/Robusr/Vulcan",
                    "关于 Vulcan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        }

        private void RunOnUiThread(Action action)
        {
            if (_taskPaneControl != null)
            {
                _taskPaneControl.PanelDispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }

        private void ReportStatus(string message)
        {
            Logger.Info(message);
            VulcanStatus.ReportAction(message);
            if (_taskPaneView != null && _taskPaneControl != null)
            {
                _taskPaneControl.SetStatus(message);
                _taskPaneView.ShowView();
            }
            else
            {
                _swApp.SendMsgToUser2(
                    message,
                    (int)swMessageBoxIcon_e.swMbInformation,
                    (int)swMessageBoxBtn_e.swMbOk);
            }
        }
        #endregion

        #region 命令回调与启用控制
        /// <summary>
        /// 工具栏按钮点击回调入口
        /// </summary>
        public void FunctionProxy(string data)
        {
            if (!int.TryParse(data, out int commandId)) return;

            switch (commandId)
            {
                case 1002:
                    OpenMainWindow();
                    break;
                case 1003:
                    OpenMainWindow("在前视基准面拉伸一个直径 50、高度 100 的圆柱体");
                    break;
                case 1004:
                    OpenMainWindow("在前视基准面拉伸 200×100×20 的安装板，四角各打一个直径 10mm 的通孔");
                    break;
                case 1005:
                    OpenSettings();
                    break;
                case 1006:
                    CheckConnection();
                    break;
                case 1007:
                    OpenStatusWindow();
                    break;
                case 1008:
                    OpenLogFolder();
                    break;
                case 1009:
                    ShowAbout();
                    break;
                default:
                    _swApp.SendMsgToUser2($"未知命令ID：{commandId}", (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
                    break;
            }
        }

        /// <summary>
        /// 命令启用状态控制
        /// </summary>
        public int EnableFunction(string data)
        {
            // 1=启用，0=禁用
            return 1;
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 对比注册表命令ID是否一致
        /// </summary>
        private bool CompareIDs(int[] storedIDs, int[] addinIDs)
        {
            if (storedIDs == null || addinIDs == null || storedIDs.Length != addinIDs.Length)
                return false;

            Array.Sort(storedIDs);
            Array.Sort(addinIDs);

            for (int i = 0; i < addinIDs.Length; i++)
            {
                if (addinIDs[i] != storedIDs[i])
                    return false;
            }
            return true;
        }
        #endregion

        #region COM注册/反注册
        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                // 获取插件特性
                var swAttr = t.GetCustomAttribute<SwAddinAttribute>();
                if (swAttr == null)
                {
                    System.Windows.Forms.MessageBox.Show("未找到SwAddin特性，注册失败！");
                    return;
                }

                // 写入HKLM注册表（SolidWorks插件注册）
                using (var hklmKey = Registry.LocalMachine.CreateSubKey($"SOFTWARE\\SolidWorks\\Addins\\{{{t.GUID}}}"))
                {
                    hklmKey.SetValue(null, 0);
                    hklmKey.SetValue("Description", swAttr.Description);
                    hklmKey.SetValue("Title", swAttr.Title);
                }

                // 写入HKCU启动项
                using (var hkcuKey = Registry.CurrentUser.CreateSubKey($"Software\\SolidWorks\\AddInsStartup\\{{{t.GUID}}}"))
                {
                    hkcuKey.SetValue(null, Convert.ToInt32(swAttr.LoadAtStartup), RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"插件注册失败：{ex.Message}");
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                // 删除注册表项
                Registry.LocalMachine.DeleteSubKey($"SOFTWARE\\SolidWorks\\Addins\\{{{t.GUID}}}", false);
                Registry.CurrentUser.DeleteSubKey($"Software\\SolidWorks\\AddInsStartup\\{{{t.GUID}}}", false);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"插件反注册失败：{ex.Message}");
            }
        }
        #endregion
    }
}