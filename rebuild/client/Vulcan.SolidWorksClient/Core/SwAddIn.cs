using Microsoft.Win32;
using SldWorks;
//using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using SolidWorksTools;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
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
        public ISldWorks SwApp
        {
            get { return _swApp; }
        }
        #endregion

        #region ISwAddin 核心接口实现
        /// <summary>
        /// 插件加载时触发
        /// </summary>
        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                _swApp = (ISldWorks)ThisSW;
                _addinCookieID = Cookie;

                // 设置回调信息（必选）
                _swApp.SetAddinCallbackInfo(0, this, _addinCookieID);
                _cmdMgr = _swApp.GetCommandManager(_addinCookieID);

                // 创建基础工具栏（仅保留打开AI窗口的按钮）
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
                // 关闭AI窗口（避免插件卸载后窗口残留）
                if (_aiMainWindow != null)
                {
                    _aiMainWindow.Close();
                    _aiMainWindow = null;
                }

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

                // 强制GC回收
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

        #region 核心逻辑：创建仅含AI窗口按钮的工具栏
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

                #region 1. 创建命令组（仅打开AI窗口的按钮）
                ICommandGroup mainCmdGroup;
                int cmdGroupErr = 0;
                bool ignorePrevious = false;
                object registryIDs;

                // 读取注册表历史ID
                bool hasRegistryData = _cmdMgr.GetGroupDataFromRegistry(MainCmdGroupID, out registryIDs);
                ignorePrevious = hasRegistryData ? !CompareIDs((int[])registryIDs, MainItemIds) : true;

                // 创建命令组
                mainCmdGroup = _cmdMgr.CreateCommandGroup2(
                    MainCmdGroupID,
                    "Vulcan AI",             // 命令组名称
                    "AI建模助手",            // 提示信息
                    "",                      // 帮助文档路径
                    -1,                      // 菜单优先级
                    ignorePrevious,          // 忽略历史配置
                    ref cmdGroupErr          // 错误码
                );

                // 命令项类型：同时显示在菜单和工具栏
                int menuToolbarType = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);

                // 添加「打开AI生成窗口」按钮
                List<int> cmdIndexes = new List<int>();
                cmdIndexes.Add(mainCmdGroup.AddCommandItem2(
                    "打开AI生成窗口",        // 菜单显示名称
                    -1,                      // 无图标（可自行添加）
                    "打开Vulcan AI建模窗口", // 鼠标悬停提示
                    "AI生成",                // 工具栏显示名称
                    0,                       // 无图标索引
                    $"FunctionProxy({MainItemIds[0]})", // 点击回调
                    $"EnableFunction({MainItemIds[0]})", // 启用控制
                    MainItemIds[0],          // 命令ID
                    menuToolbarType          // 显示类型
                ));

                // 启用工具栏和菜单
                mainCmdGroup.HasToolbar = true;
                mainCmdGroup.HasMenu = true;
                mainCmdGroup.Activate();
                #endregion

                #region 2. 绑定工具栏到SolidWorks标签页
                foreach (int docType in docTypes)
                {
                    CommandTab cmdTab = _cmdMgr.GetCommandTab(docType, "Vulcan AI");

                    // 删除旧标签页（ID变更时）
                    if (cmdTab != null && !hasRegistryData && ignorePrevious)
                    {
                        _cmdMgr.RemoveCommandTab(cmdTab);
                        cmdTab = null;
                    }

                    // 新建标签页
                    if (cmdTab == null)
                    {
                        cmdTab = _cmdMgr.AddCommandTab(docType, "Vulcan AI");
                        CommandTabBox cmdBox = cmdTab.AddCommandTabBox();

                        // 收集命令ID
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
        /// 打开AI生成窗口（WPF窗口）
        /// </summary>
        private void OpenMainWindow()
        {
            try
            {
                Logger.Info("正在打开Vulcan AI窗口...");
                // 修复：直接传ISldWorks接口，不再强制转换，和SwModeler完全匹配
                var modeler = new SwModeler(_swApp);
                var mainWindow = new MainWindow(modeler);
                IntPtr ownerHwnd = IntPtr.Zero;
                try
                {
                    dynamic frame = _swApp.Frame();
                    if (frame != null)
                    {
                        ownerHwnd = new IntPtr((int)frame.GetHWnd());
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

        #region 命令回调与控制
        /// <summary>
        /// 工具栏按钮点击回调（核心业务入口）
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
                    _swApp.SendMsgToUser($"未知命令ID：{commandId}");
                    break;
            }
        }

        /// <summary>
        /// 命令启用控制（始终启用）
        /// </summary>
        public int EnableFunction(string data)
        {
            return 1;
        }
        #endregion

        #region 工具方法
        /// <summary>
        /// 对比命令ID是否一致
        /// </summary>
        private bool CompareIDs(int[] storedIDs, int[] addinIDs)
        {
            if (storedIDs == null || addinIDs == null || storedIDs.Length != addinIDs.Length)
                return false;

            List<int> storedList = new List<int>(storedIDs);
            List<int> addinList = new List<int>(addinIDs);
            storedList.Sort();
            addinList.Sort();

            for (int i = 0; i < addinList.Count; i++)
            {
                if (addinList[i] != storedList[i])
                    return false;
            }
            return true;
        }
        #endregion

        #region COM注册/反注册（必选）
        [ComRegisterFunctionAttribute]
        public static void RegisterFunction(Type t)
        {
            try
            {
                // 获取SwAddin特性
                SwAddinAttribute swAttr = null;
                foreach (System.Attribute attr in t.GetCustomAttributes(false))
                {
                    if (attr is SwAddinAttribute)
                    {
                        swAttr = (SwAddinAttribute)attr;
                        break;
                    }
                }
                if (swAttr == null)
                {
                    System.Windows.Forms.MessageBox.Show("未找到SwAddin特性，注册失败！");
                    return;
                }

                // 写入HKLM
                using (RegistryKey hklmKey = Registry.LocalMachine.CreateSubKey(
                    $"SOFTWARE\\SolidWorks\\Addins\\{{{t.GUID}}}"))
                {
                    hklmKey.SetValue(null, 0);
                    hklmKey.SetValue("Description", swAttr.Description);
                    hklmKey.SetValue("Title", swAttr.Title);
                }

                // 写入HKCU
                using (RegistryKey hkcuKey = Registry.CurrentUser.CreateSubKey(
                    $"Software\\SolidWorks\\AddInsStartup\\{{{t.GUID}}}"))
                {
                    hkcuKey.SetValue(null, Convert.ToInt32(swAttr.LoadAtStartup), RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"插件注册失败：{ex.Message}");
            }
        }

        [ComUnregisterFunctionAttribute]
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