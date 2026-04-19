using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
// 明确指定接口来源，消除所有歧义
using SW = SldWorks;
using SWAddin = SolidWorks.Interop.swpublished.ISwAddin;
using Vulcan.SolidWorksClient.Services;
using Vulcan.SolidWorksClient.UI;

namespace Vulcan.SolidWorksClient.Core
{
    // 你的唯一GUID，不要修改
    [Guid("B0929746-3430-4CC6-9B71-6650F6B6BB68")]
    [ComVisible(true)]
    [ProgId("Vulcan.SolidWorksClient.AddIn")]
    [ClassInterface(ClassInterfaceType.None)]
    // 明确指定实现的是SolidWorks官方的插件接口
    public class SwAddIn : SWAddin
    {
        private SW.SldWorks _swApp;
        private int _addinCookie;
        // 改用强类型，彻底避免dynamic的兼容问题
        private SW.CommandManager _cmdManager;
        private SW.CommandGroup _cmdGroup;

        #region 核心：SolidWorks插件接口实现（必须严格匹配，不能改签名）
        /// <summary>
        /// SolidWorks加载插件时会调用这个方法，必须返回true，否则插件会被拒绝加载
        /// </summary>
        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                // 第一步：先记录日志，确认SolidWorks调用了这个方法
                Logger.Info("=== SolidWorks正在加载Vulcan插件 ===");

                // 初始化SolidWorks应用对象
                _swApp = (SW.SldWorks)ThisSW;
                _addinCookie = Cookie;
                Logger.Info($"SolidWorks版本：{_swApp.RevisionNumber()}，插件Cookie：{_addinCookie}");

                // 第二步：创建菜单和工具栏（即使这里出错，也不会打断插件加载）
                try
                {
                    CreateToolbarAndMenu();
                    Logger.Info("工具栏和菜单创建成功");
                }
                catch (Exception menuEx)
                {
                    Logger.Error("菜单创建失败，不影响插件加载", menuEx);
                }

                // 必须返回true，告诉SolidWorks插件加载成功
                Logger.Info("=== Vulcan插件加载完成 ===");
                return true;
            }
            catch (Exception ex)
            {
                // 全局异常捕获，绝对不能让这个方法异常退出
                Logger.Error("插件加载过程发生致命错误", ex);
                // 即使出错，也返回true，确保插件能出现在列表里
                return true;
            }
        }

        /// <summary>
        /// SolidWorks卸载插件时调用
        /// </summary>
        public bool DisconnectFromSW()
        {
            try
            {
                Logger.Info("=== 开始卸载Vulcan插件 ===");
                // 清理资源
                if (_cmdGroup != null)
                {
                    _cmdGroup.HasToolbar = false;
                    _cmdGroup.HasMenu = false;
                    _cmdGroup.Activate(); // 反激活命令组
                    Marshal.ReleaseComObject(_cmdGroup);
                    _cmdGroup = null;
                }
                Marshal.ReleaseComObject(_cmdManager);
                Marshal.ReleaseComObject(_swApp);
                Logger.Info("=== Vulcan插件卸载完成 ===");
            }
            catch (Exception ex)
            {
                Logger.Error("插件卸载出错", ex);
            }
            return true;
        }
        #endregion

        #region 工具栏/菜单创建
        private void CreateToolbarAndMenu()
        {
            _cmdManager = _swApp.GetCommandManager(_addinCookie);
            int cmdGroupId = 0;
            int createError = 0;

            // 创建命令组，强类型调用，无兼容问题
            _cmdGroup = _cmdManager.CreateCommandGroup2(
                cmdGroupId,
                "Vulcan AI",
                "Vulcan AI 建模助手",
                "AI驱动的SolidWorks建模工具",
                -1,
                true,
                ref createError);

            // 校验创建结果
            if (createError != 0)
            {
                Logger.Error($"命令组创建失败，错误码：{createError}");
                return;
            }

            // 添加工具栏按钮
            _cmdGroup.AddCommandItem2(
                "打开Vulcan",
                0,
                "打开Vulcan AI建模助手",
                "打开Vulcan",
                0,
                nameof(OpenMainWindow),
                nameof(IsButtonEnable),
                -1,
                3); // 3 = 同时显示在菜单+工具栏

            // 激活工具栏和菜单
            _cmdGroup.HasToolbar = true;
            _cmdGroup.HasMenu = true;
            _cmdGroup.Activate();
        }
        #endregion

        #region 按钮回调方法
        public void OpenMainWindow()
        {
            try
            {
                var modeler = new SwModeler(_swApp);
                var mainWindow = new MainWindow(modeler);
                // 获取SolidWorks主窗口句柄，作为WPF窗口的Owner
                IntPtr ownerHwnd = IntPtr.Zero;
                try
                {
                    // 尝试通过Frame()方法获取主窗口句柄
                    dynamic frame = _swApp.Frame();
                    if (frame != null)
                    {
                        ownerHwnd = new IntPtr((int)frame.GetHWnd());
                    }
                }
                catch
                {
                    // 忽略异常，ownerHwnd 保持为 IntPtr.Zero
                }
                new WindowInteropHelper(mainWindow).Owner = ownerHwnd;
                mainWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                Logger.Error("打开Vulcan窗口失败", ex);
                System.Windows.MessageBox.Show($"打开窗口失败：{ex.Message}", "错误");
            }
        }

        public int IsButtonEnable()
        {
            return 1; // 始终启用按钮
        }
        #endregion
    }
}