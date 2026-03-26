using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using SolidWorksTools;
using System;
using System.Collections.Generic;
using System.Reflection;
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

        private Window _aiMainWindow = null;

        public int MainCmdGroupID = 5001;
        public int[] MainItemIds = new[] { 1002 };
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
                // 类型转换获取SolidWorks应用对象
                _swApp = (ISldWorks)ThisSW;
                _addinCookieID = Cookie;

                // 设置回调信息
                _swApp.SetAddinCallbackInfo(0, this, _addinCookieID);
                _cmdMgr = _swApp.GetCommandManager(_addinCookieID);

                // 创建工具栏
                CreateBasicCommandManager();

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
                // 关闭AI窗口
                _aiMainWindow?.Close();
                _aiMainWindow = null;

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
                    "AI Assistant For SolidWorks",
                    string.Empty,
                    -1,
                    ignorePrevious,
                    ref cmdGroupErr
                );

                // 命令项类型：同时显示在菜单和工具栏
                int menuToolbarType = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);

                // 添加「打开AI生成窗口」按钮
                List<int> cmdIndexes = new List<int>();
                cmdIndexes.Add(mainCmdGroup.AddCommandItem2(
                    "Active Vulcan AI",
                    -1,
                    "Connect To Vulcan AI",
                    "Hello Vulcan",
                    0,
                    $"FunctionProxy({MainItemIds[0]})",
                    $"EnableFunction({MainItemIds[0]})",
                    MainItemIds[0],
                    menuToolbarType
                ));

                // 启用工具栏和菜单
                mainCmdGroup.HasToolbar = true;
                mainCmdGroup.HasMenu = true;
                mainCmdGroup.Activate();
                #endregion

                #region 2. 绑定命令到Ribbon标签页
                foreach (int docType in docTypes)
                {
                    CommandTab cmdTab = _cmdMgr.GetCommandTab(docType, "Vulcan AI");

                    // ID变更时删除旧标签页
                    if (cmdTab != null && !hasRegistryData && ignorePrevious)
                    {
                        _cmdMgr.RemoveCommandTab(cmdTab);
                        cmdTab = null;
                    }

                    // 新建Ribbon标签页
                    if (cmdTab == null)
                    {
                        cmdTab = _cmdMgr.AddCommandTab(docType, "Vulcan AI");
                        CommandTabBox cmdBox = cmdTab.AddCommandTabBox();

                        // 收集命令ID与显示样式
                        List<int> cmdIDs = new List<int>();
                        List<int> textDisplayTypes = new List<int>();
                        foreach (int idx in cmdIndexes)
                        {
                            cmdIDs.Add(mainCmdGroup.get_CommandID(idx));
                            textDisplayTypes.Add((int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow);
                        }

                        // 绑定命令到工具栏
                        cmdBox.AddCommands(cmdIDs.ToArray(), textDisplayTypes.ToArray());
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"创建工具栏失败：{ex.Message}\n{ex.StackTrace}");
            }
        }
        #endregion

        #region 核心功能：打开AI生成窗口
        /// <summary>
        /// 打开AI生成窗口，传递和ISldWorks实例
        /// </summary>
        private void OpenMainWindow()
        {
            try
            {
                Logger.Info("正在打开Vulcan AI窗口...");
                // 传递ISldWorks
                var modeler = new SwModeler(_swApp);
                var mainWindow = new MainWindow(modeler);

                // 绑定SolidWorks主窗口为父窗口
                IntPtr ownerHwnd = IntPtr.Zero;
                try
                {
                    var frame = _swApp.Frame();
                    if (frame != null)
                    {
                        ownerHwnd = new IntPtr(frame.GetHWnd());
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning("获取SolidWorks窗口句柄失败", ex);
                }

                new WindowInteropHelper(mainWindow).Owner = ownerHwnd;
                Logger.Info("Vulcan AI窗口已打开");
                mainWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error("打开Vulcan窗口失败", ex);
                System.Windows.MessageBox.Show($"打开窗口失败：{ex.Message}", "错误");
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