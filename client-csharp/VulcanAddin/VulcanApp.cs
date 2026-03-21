using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using SolidWorksTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using static System.Net.Mime.MediaTypeNames;

namespace VulcanAddin
{
    [Guid("5F1FC63D-5094-46AE-A6B2-88ADE9399E9C"), ComVisible(true)]
    [SwAddin(Description = "SolidWorksAddinStudy description", Title = "SolidWorksAddinStudy", LoadAtStartup = true)]
    public class VulcanApp : ISwAddin
    {
        private ISldWorks iSwApp = null;
        private ICommandManager iCmdMgr = null;

        /// <summary>
        /// 插件cookie
        /// </summary>
        private int addinCookieID;

        public int mainCmdGroupID = 5001;
        public int mainSubCmdGroupID = 8001;

        public int flyoutGroupID1 = 6000;
        public int flyoutGroupID2 = 7000;

        //本示例只有3个命名，三个图标。
        public int[] mainItemIds = new[] { 1002, 1003, 1004, 1005 };

        /// <summary>
        /// 主图标的6种尺寸
        /// </summary>
        private string[] mainIcons = new string[6];

        /// <summary>
        /// 工具栏图标带6种尺寸文件
        /// </summary>
        private string[] icons = new string[6];

        public ISldWorks SwApp
        {
            get { return iSwApp; }
        }

        public VulcanApp()
        {
        }

        /// <summary>
        /// 连接到SolidWorks
        /// </summary>
        /// <param name="ThisSW"></param>
        /// <param name="Cookie"></param>
        /// <returns></returns>
        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            iSwApp = (ISldWorks)ThisSW;

            //iSwApp.SendMsgToUser("SolidWorks正在加载此插件...");

            addinCookieID = Cookie;
            iSwApp.SetAddinCallbackInfo(0, this, addinCookieID);
            iCmdMgr = iSwApp.GetCommandManager(addinCookieID);
            AddCommandMgr();

            return true;
        }

        /// <summary>
        /// 增加命令
        /// </summary>
        public void AddCommandMgr()
        {
            try
            {
                ICommandGroup cmdGroup;

                //如果要支持多语言，就在这里下功夫
                string Title = "Vulcan";
                string ToolTip = "Vulcan ToolTip";

                int[] docTypes = new int[]{(int)swDocumentTypes_e.swDocASSEMBLY,
                                       (int)swDocumentTypes_e.swDocDRAWING,
                                       (int)swDocumentTypes_e.swDocPART};



                int cmdGroupErr = 0;
                bool ignorePrevious = false;

                object registryIDs;
                //通过id从注册表获取工具栏的信息,并返回之前的命令id
                bool getDataResult = iCmdMgr.GetGroupDataFromRegistry(mainCmdGroupID, out registryIDs);

                //当前版本的插件id
                var knownIDs = mainItemIds;

                if (getDataResult)
                {
                    //如果命令id集不一样了，那么就要忽略，后面要重新建立
                    if (!CompareIDs((int[])registryIDs, knownIDs)) //if the IDs don't match, reset the commandGroup
                    {
                        ignorePrevious = true;
                    }
                }
                else
                {
                    ignorePrevious = true;
                }

                cmdGroup = iCmdMgr.CreateCommandGroup2(mainCmdGroupID, Title, ToolTip, "", -1, ignorePrevious, ref cmdGroupErr);

                // 设置对应的图标带 ，后面增加命令的时候就是传递的图标带的序号，从0开始
                icons[0] = $@"{RegDllPath()}\icons\toolbar20x.png";// iBmp.CreateFileFromResourceBitmap("toolbar20x.png", thisAssembly);
                icons[1] = $@"{RegDllPath()}\icons\toolbar32x.png";// iBmp.CreateFileFromResourceBitmap("toolbar32x.png", thisAssembly);
                icons[2] = $@"{RegDllPath()}\icons\toolbar40x.png";// iBmp.CreateFileFromResourceBitmap("toolbar40x.png", thisAssembly);
                icons[3] = $@"{RegDllPath()}\icons\toolbar64x.png";// iBmp.CreateFileFromResourceBitmap("toolbar64x.png", thisAssembly);
                icons[4] = $@"{RegDllPath()}\icons\toolbar96x.png";// iBmp.CreateFileFromResourceBitmap("toolbar96x.png", thisAssembly);
                icons[5] = $@"{RegDllPath()}\icons\toolbar128x.png";//iBmp.CreateFileFromResourceBitmap("toolbar128x.png", thisAssembly);

                mainIcons[0] = $@"{RegDllPath()}\icons\mainicon_20.png";//iBmp.CreateFileFromResourceBitmap("mainicon_20.png", thisAssembly);
                mainIcons[1] = $@"{RegDllPath()}\icons\mainicon_32.png";//iBmp.CreateFileFromResourceBitmap("mainicon_32.png", thisAssembly);
                mainIcons[2] = $@"{RegDllPath()}\icons\mainicon_40.png";//iBmp.CreateFileFromResourceBitmap("mainicon_40.png", thisAssembly);
                mainIcons[3] = $@"{RegDllPath()}\icons\mainicon_64.png";//iBmp.CreateFileFromResourceBitmap("mainicon_64.png", thisAssembly);
                mainIcons[4] = $@"{RegDllPath()}\icons\mainicon_96.png";//iBmp.CreateFileFromResourceBitmap("mainicon_96.png", thisAssembly);
                mainIcons[5] = $@"{RegDllPath()}\icons\mainicon_128.png";//iBmp.CreateFileFromResourceBitmap("mainicon_128.png", thisAssembly);

                cmdGroup.MainIconList = mainIcons;
                cmdGroup.IconList = icons;

                //菜单的类型有哪些 菜单 工具条
                int menuToolbarOption = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);

                //菜单
                List<int> cmdIndexs = new List<int>();

                //API提示的信息有误
                //第一个参数是菜单里面的名称
                //第三个参数是提示信息
                //第四个参数是工具条上的名称
                var tempCmdIndex1 = cmdGroup.AddCommandItem2("Cmd1", -1, "Cmd Tooltip1", "Cmd-1", 0, $"FunctionProxy({mainItemIds[0]})", $@"EnableFunction({mainItemIds[0]})", mainItemIds[0], menuToolbarOption);
                var tempCmdIndex2 = cmdGroup.AddCommandItem2("Cmd2", -1, "Cmd Tooltip2", "Cmd-2", 1, $"FunctionProxy({mainItemIds[1]})", $@"EnableFunction({mainItemIds[1]})", mainItemIds[1], menuToolbarOption);
                var tempCmdIndex3 = cmdGroup.AddCommandItem2("Cmd3", -1, "Cmd Tooltip3", "Cmd-3", 2, $"FunctionProxy({mainItemIds[2]})", $@"EnableFunction({mainItemIds[2]})", mainItemIds[2], menuToolbarOption);



                cmdIndexs.Add(tempCmdIndex1);
                cmdIndexs.Add(tempCmdIndex2);
                cmdIndexs.Add(tempCmdIndex3);

                cmdGroup.HasToolbar = true;
                cmdGroup.HasMenu = true;

                cmdGroup.Activate();


                var cmdGroupSub = iCmdMgr.CreateCommandGroup2(mainSubCmdGroupID, $"Vulcan\\SubMenu", ToolTip, "", -1, ignorePrevious, ref cmdGroupErr);


                cmdGroupSub.MainIconList = mainIcons;
                cmdGroupSub.IconList = icons;

                cmdGroupSub.AddCommandItem2($@"SubCmd4", -1, "Cmd Tooltip4", "Cmd-4", 2, $"FunctionProxy({mainItemIds[2]})", $@"EnableFunction({mainItemIds[2]})", mainItemIds[2], menuToolbarOption);

                cmdGroupSub.Activate();

                #region 下拉式工具栏



                //创建一个下拉式工具栏


                FlyoutGroup flyGroup1 = iCmdMgr.CreateFlyoutGroup2(flyoutGroupID1, "FlyoutGroup1", "可下拉1", "工具栏说明",
                  cmdGroup.MainIconList, cmdGroup.IconList, $"FlyoutCallback(6000)", "FlyoutEnable");


                flyGroup1.FlyoutType = (int)swCommandFlyoutStyle_e.swCommandFlyoutStyle_Simple;

                var addResult = flyGroup1.AddContextMenuFlyout((int)swDocumentTypes_e.swDocPART, (int)swSelectType_e.swSelFACES);


                FlyoutGroup flyGroup2 = iCmdMgr.CreateFlyoutGroup2(flyoutGroupID2, "FlyoutGroup2", "可下拉2", "工具栏说明2",
                    cmdGroup.MainIconList, cmdGroup.IconList, $"FlyoutCallback(7000)", "FlyoutEnable");


                flyGroup2.FlyoutType = (int)swCommandFlyoutStyle_e.swCommandFlyoutStyle_Simple;

                #endregion

                iCmdMgr.AddContextMenu(1548, "aaa");

                #region 右键菜单

                //右键菜单 （好像没看到图标的定义方式)  --选中草后 右键显示

                var popItem1 = SwApp.AddMenuPopupItem4((int)swDocumentTypes_e.swDocPART, addinCookieID, "ProfileFeature", "右键菜单",
                    "FlyoutCallback(7000)", "FlyoutEnable", "我的右键菜单", "");

                SwApp.AddMenuPopupItem4((int)swDocumentTypes_e.swDocPART, addinCookieID, "ProfileFeature", "子菜单For草图@右键子菜单",
                    "FlyoutCallback(7000)", "FlyoutEnable", "子菜单@右键菜单", "");




                #endregion


                var menuNewId = SwApp.AddMenu((int)swDocumentTypes_e.swDocPART, "MyMenu", 0);


                #region 工具菜单栏下面显示新菜单项

                var menuId = iSwApp.AddMenuItem5((int)swDocumentTypes_e.swDocPART, addinCookieID, "子菜单1@新菜单1", 0, "FlyoutCallback(7000)", "FlyoutEnable", "My menu item", icons);
                var menuId2 = iSwApp.AddMenuItem5((int)swDocumentTypes_e.swDocPART, addinCookieID, "子菜单3@子菜单2@新菜单1", 0, "FlyoutCallback(7000)", "FlyoutEnable", "My menu item", icons);
                var menuId3 = iSwApp.AddMenuItem5((int)swDocumentTypes_e.swDocPART, addinCookieID, "子菜单4@Addin Study", 0, "FlyoutCallback(7000)", "FlyoutEnable", "My menu item", icons);



                #endregion


                //增加到工具条，是通过每个文档对象来增加的。 比如零件 装配 工程图
                bool bResult;

                foreach (int type in docTypes)
                {
                    CommandTab cmdTab;

                    cmdTab = iCmdMgr.GetCommandTab(type, Title);

                    //如果已经存在，并且id命令有变化，需要移除之后 ，重新增加。
                    if (cmdTab != null & !getDataResult && ignorePrevious)
                    {
                        bool res = iCmdMgr.RemoveCommandTab(TabToRemove: cmdTab);
                        cmdTab = null;
                    }

                    //工具栏为空时，重新增加
                    if (cmdTab == null)
                    {
                        cmdTab = iCmdMgr.AddCommandTab(type, Title);

                        CommandTabBox cmdBox = cmdTab.AddCommandTabBox();

                        List<int> cmdIDs = new List<int>();

                        //工具栏样式，
                        List<int> showTextType = new List<int>();


                        for (int i = 0; i < cmdIndexs.Count; i++)
                        {
                            cmdIDs.Add(cmdGroup.get_CommandID(i));
                            showTextType.Add((int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow);
                        }

                        //把下拉式工具栏加到菜单里。
                        cmdIDs.Add(flyGroup1.CmdID);
                        showTextType.Add((int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow);

                        cmdIDs.Add(flyGroup2.CmdID);
                        showTextType.Add((int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow);

                        bResult = cmdBox.AddCommands(cmdIDs.ToArray(), showTextType.ToArray());

                        CommandTabBox cmdBox1 = cmdTab.AddCommandTabBox();

                        //这个是加分割线，记得从后往前，因为分割后最前的id集变少了。
                        //cmdTab.AddSeparator(cmdBox1, cmdIDs[0]);

                    }
                }


                #region 新上下文菜单



                IFrame frame = (IFrame)SwApp.Frame();

                var imgPath1 = $@"{RegDllPath()}\icons\Pic1 (1).png";
                var imgPath2 = $@"{RegDllPath()}\icons\Pic1 (2).png";
                var imgPath3 = $@"{RegDllPath()}\icons\Pic1 (3).png";
                var imgPath4 = $@"{RegDllPath()}\icons\Pic1 (4).png";
                var imgPath5 = $@"{RegDllPath()}\icons\Pic1 (5).png";
                var imgPath6 = $@"{RegDllPath()}\icons\Pic1 (6).png";


                var resultCode = frame.AddMenuPopupIcon2((int)swDocumentTypes_e.swDocPART, (int)swSelectType_e.swSelNOTHING, "新上下文菜单", addinCookieID, "PopupCallbackFunction", "PopupEnable", "", imgPath1);

                // create and register the third party menu 创建并注册第三方组菜单
                registerID = SwApp.RegisterThirdPartyPopupMenu();


                // add a menu break at the top of the menu  在组菜单最上方增加一个不能点击的菜单
                resultCode = SwApp.AddItemToThirdPartyPopupMenu2(registerID, (int)swDocumentTypes_e.swDocPART, "我的菜单", addinCookieID, "", "", "", "", "", (int)swMenuItemType_e.swMenuItemType_Break);
                // add a couple of items to to the menu   增加菜单内的命令集
                resultCode = SwApp.AddItemToThirdPartyPopupMenu2(registerID, (int)swDocumentTypes_e.swDocPART, "命令测试1", addinCookieID, "FlyoutCallback(7000)", "FlyoutEnable", "", "Test1", imgPath2, (int)swMenuItemType_e.swMenuItemType_Default);
                resultCode = SwApp.AddItemToThirdPartyPopupMenu2(registerID, (int)swDocumentTypes_e.swDocPART, "命令测试2", addinCookieID, "FlyoutCallback(7000)", "FlyoutEnable", "", "Test4", imgPath3, (int)swMenuItemType_e.swMenuItemType_Default);
                // add a separator bar to the menu  增加分割线
                resultCode = SwApp.AddItemToThirdPartyPopupMenu2(registerID, (int)swDocumentTypes_e.swDocPART, "", addinCookieID, "", "", "", "", "", (int)swMenuItemType_e.swMenuItemType_Separator);

                //继续增加个命令
                resultCode = SwApp.AddItemToThirdPartyPopupMenu2(registerID, (int)swDocumentTypes_e.swDocPART, "命令测试3", addinCookieID, "FlyoutCallback(7000)", "FlyoutEnable", "", "Test5", imgPath4, (int)swMenuItemType_e.swMenuItemType_Default);

                // add an icon to the menu bar  给菜单条上面再加图标按钮
                resultCode = SwApp.AddItemToThirdPartyPopupMenu2(registerID, (int)swDocumentTypes_e.swDocPART, "", addinCookieID, "FlyoutCallback(7000)", "FlyoutEnable", "", "NoOp", imgPath5, (int)swMenuItemType_e.swMenuItemType_Default);


                #endregion


            }
            catch (Exception ex)
            {
                SwApp.SendMsgToUser(ex.StackTrace);
            }
        }

        private int registerID = 0;


        public int PopupEnable()
        {
            if (iSwApp.ActiveDoc == null)
                return 0;
            else
                return 1;
        }

        public void PopupCallbackFunction()
        {
            bool bRet;

            bRet = iSwApp.ShowThirdPartyPopupMenu(registerID, 500, 500);
        }


        /// <summary>
        /// 决定此命令在该环境下是否可用
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public int EnableFunction(string data)
        {
            int commandType = int.Parse(data);

            return 1;
        }

        #region 下拉菜单的方法

        public void FlyoutCallback(int gId)
        {
            if (gId == flyoutGroupID1)
            {
                FlyoutGroup flyGroup1 = iCmdMgr.GetFlyoutGroup(gId);
                flyGroup1.RemoveAllCommandItems();

                flyGroup1.AddCommandItem("AAA", "test", 0, $"FlyoutCommandItem1({gId + 1})", $"FlyoutEnableCommandItem1({gId + 1})");
                flyGroup1.AddCommandItem("BBB", "test", 0, $"FlyoutCommandItem1({gId + 2})", $"FlyoutEnableCommandItem1({gId + 2})");
                flyGroup1.AddCommandItem("CCC", "test", 0, $"FlyoutCommandItem1({gId + 3})", $"FlyoutEnableCommandItem1({gId + 3})");


            }
            if (gId == flyoutGroupID2)
            {
                FlyoutGroup flyGroup2 = iCmdMgr.GetFlyoutGroup(gId);
                flyGroup2.RemoveAllCommandItems();

                flyGroup2.AddCommandItem("XXX", "test", 0, $"FlyoutCommandItem1({gId + 1})", $"FlyoutEnableCommandItem1({gId + 1})");
                flyGroup2.AddCommandItem("YYY", "test", 0, $"FlyoutCommandItem1({gId + 2})", $"FlyoutEnableCommandItem1({gId + 2})");


            }

            if (gId == 7000)
            {
                SwApp.SendMsgToUser("id==7000");
            }

        }

        public int FlyoutEnable()
        {
            return 1;
        }

        public void FlyoutCommandItem1(int flyCmdId)
        {
            if (flyCmdId == 6001)
            {
                iSwApp.SendMsgToUser("Flyout command 6001");

                //var tempMode= iSwApp.IActiveDoc2.GetPopupMenuMode();
                //iSwApp.IActiveDoc2.SetPopupMenuMode(1);
            }
            if (flyCmdId == 7000)
            {
                iSwApp.SendMsgToUser("Flyout command 7000");
            }
            if (flyCmdId == 7001)
            {
                iSwApp.SendMsgToUser("Flyout command 7001");
            }
            if (flyCmdId == 3)
            {
                iSwApp.SendMsgToUser("Flyout command 3");
            }

        }

        public int FlyoutEnableCommandItem1(int flyCmdId)
        {
            if (flyCmdId == 6001)
            {
                return 1;
            }
            if (flyCmdId == 6002)
            {
                return 1;
            }
            if (flyCmdId == 3)
            {
                return 1;
            }
            return 1;
        }

        #endregion


        /// <summary>
        /// 通过用户点击的菜单id来执行不同的动作
        /// </summary>
        /// <param name="data"></param>
        public void FunctionProxy(string data)
        {
            int commandId = int.Parse(data);

            switch (commandId)
            {
                case 1002:
                    SwApp.SendMsgToUser("Cmd1 Click");

                    break;

                case 1003:
                    SwApp.SendMsgToUser("Cmd2 Click");
                    break;

                case 1004:
                    SwApp.SendMsgToUser("Cmd3 Click");
                    break;


            }
        }

        public void RemoveCommandMgr()
        {
            //iBmp.Dispose();
            iCmdMgr.RemoveCommandGroup(mainCmdGroupID);
        }

        public bool CompareIDs(int[] storedIDs, int[] addinIDs)
        {
            List<int> storedList = new List<int>(storedIDs);
            List<int> addinList = new List<int>(addinIDs);

            addinList.Sort();
            storedList.Sort();

            if (addinList.Count != storedList.Count)
            {
                return false;
            }
            else
            {
                for (int i = 0; i < addinList.Count; i++)
                {
                    if (addinList[i] != storedList[i])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 断开连接，卸载插件时执行
        /// </summary>
        /// <returns></returns>
        public bool DisconnectFromSW()
        {
            Marshal.ReleaseComObject(iCmdMgr);
            iCmdMgr = null;
            Marshal.ReleaseComObject(iSwApp);
            iSwApp = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            return true;
        }

        /// <summary>
        /// 当前dll路径, 最后路径无\
        /// </summary>
        /// <returns></returns>
        public string RegDllPath()
        {
            try
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
            catch (Exception)
            {
                return "";
            }
        }

        #region SolidWorks注册表，用于注册插件信息

        [ComRegisterFunctionAttribute]
        public static void RegisterFunction(Type t)
        {
            #region Get Custom Attribute: SwAddinAttribute

            SwAddinAttribute SWattr = null;
            Type type = typeof(VulcanApp);

            foreach (System.Attribute attr in type.GetCustomAttributes(false))
            {
                if (attr is SwAddinAttribute)
                {
                    SWattr = attr as SwAddinAttribute;
                    break;
                }
            }

            #endregion Get Custom Attribute: SwAddinAttribute

            try
            {
                Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.Registry.LocalMachine;
                Microsoft.Win32.RegistryKey hkcu = Microsoft.Win32.Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID.ToString() + "}";
                Microsoft.Win32.RegistryKey addinkey = hklm.CreateSubKey(keyname);
                addinkey.SetValue(null, 0);

                addinkey.SetValue("Description", SWattr.Description);
                addinkey.SetValue("Title", SWattr.Title);

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID.ToString() + "}";
                addinkey = hkcu.CreateSubKey(keyname);
                addinkey.SetValue(null, Convert.ToInt32(SWattr.LoadAtStartup), Microsoft.Win32.RegistryValueKind.DWord);
            }
            catch (System.NullReferenceException nl)
            {
                Console.WriteLine("There was a problem registering this dll: SWattr is null. \n\"" + nl.Message + "\"");
                System.Windows.Forms.MessageBox.Show("There was a problem registering this dll: SWattr is null.\n\"" + nl.Message + "\"");
            }
            catch (System.Exception e)
            {
                Console.WriteLine(e.Message);

                System.Windows.Forms.MessageBox.Show("There was a problem registering the function: \n\"" + e.Message + "\"");
            }
        }

        [ComUnregisterFunctionAttribute]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                Microsoft.Win32.RegistryKey hklm = Microsoft.Win32.Registry.LocalMachine;
                Microsoft.Win32.RegistryKey hkcu = Microsoft.Win32.Registry.CurrentUser;

                string keyname = "SOFTWARE\\SolidWorks\\Addins\\{" + t.GUID.ToString() + "}";
                hklm.DeleteSubKey(keyname);

                keyname = "Software\\SolidWorks\\AddInsStartup\\{" + t.GUID.ToString() + "}";
                hkcu.DeleteSubKey(keyname);
            }
            catch (System.NullReferenceException nl)
            {
                Console.WriteLine("There was a problem unregistering this dll: " + nl.Message);
                System.Windows.Forms.MessageBox.Show("There was a problem unregistering this dll: \n\"" + nl.Message + "\"");
            }
            catch (System.Exception e)
            {
                Console.WriteLine("There was a problem unregistering this dll: " + e.Message);
                System.Windows.Forms.MessageBox.Show("There was a problem unregistering this dll: \n\"" + e.Message + "\"");
            }
        }

        #endregion SolidWorks注册表，用于注册插件信息
    }
}
