using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Net;
using System.Collections;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using FOG;
using IniReaderObj;
using System.IO;

using System.Windows.Forms;

namespace FOG 
{

    public class DisplayManager : AbstractFOGService
    {
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_HIDE = 0;
        private const int SW_SHOWNORMAL = 1;
        private const int SW_SHOWMINIMIZED = 2;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_SHOWNOACTIVATE = 4;
        private const int SW_RESTORE = 9;
        private const int SW_SHOWDEFAULT = 10;

        private int intStatus;
        private String strURLDisplay;
        private String strURLModuleStatus;
        private Boolean blGo;

        private const String MOD_NAME = "FOG::DisplayManager";

        public DisplayManager()
        {
            intStatus = STATUS_STOPPED;
        }

        private Boolean readSettings()
        {
            if (ini != null)
            {
                if (ini.isFileOk())
                {
                    // Get the FOG Server IP Address or hostname
                    String ip = ini.readSetting("fog_service", "ipaddress");

                    if (ip == null || ip.Trim().Length == 0)
                        ip = "fogserver";

                    // get the module status URL 
                    String strPreMS = ini.readSetting("fog_service", "urlprefix");
                    String strPostMS = ini.readSetting("fog_service", "urlpostfix");
                    if ( ip != null && strPreMS != null && strPostMS != null )
                        strURLModuleStatus = strPreMS + ip + strPostMS;
                    else
                    {
                        return false;
                    }


                    String pre = ini.readSetting("displaymanager", "urlprefix");
                    String post = ini.readSetting("displaymanager", "urlpostfix");
                    if (ip != null && ip.Length > 0 && pre != null && pre.Length > 0 && post != null && post.Length > 0)
                    {
                        strURLDisplay = pre + ip + post;
                        return true;
                    }
                }
            }
            return false;
        }

        public override void mStart()
        {
            try
            {
                intStatus = STATUS_RUNNING;
                if (readSettings())
                {
                    blGo = true;
                    doWork();
                }
                else
                {
                    log(MOD_NAME, "Failed to read ini settings.");
                }
            }
            catch
            {
            }
        }

        public override string mGetDescription()
        {
            return "Display Manager - This module will reset the client computer's resolution to a default value on user log in.";
        }

        private string decode64(string strEncode)
        {
            try
            {
                byte[] b = Convert.FromBase64String(strEncode);
                return Encoding.ASCII.GetString(b);
            }
            catch
            {
                return "";
            }
        }

        private void doWork()
        {
            try
            {
                log(MOD_NAME, "Starting display manager process...");
                switch (System.Environment.OSVersion.Version.Major)
                {
                    case 5:
                        break;
                    case 6:
                        break;
                    case 7:
                        break;
                    default:
                        log(MOD_NAME, "This module has only been tested on Windows XP, Vista and 7!");
                        break;
                }

                ArrayList alMACs = getMacAddress();
                String macList = null;
                if (alMACs != null && alMACs.Count > 0)
                {
                    String[] strMacs = (String[])alMACs.ToArray(typeof(String));
                    macList = String.Join("|", strMacs);
                }  

                // First check and see if the module is active
                //
                Boolean blLoop = false;
                if (macList != null && macList.Length > 0)
                {
                    Boolean blConnectOK = false;
                    String strData = "";
                    while (!blConnectOK)
                    {
                        try
                        {
                            log(MOD_NAME, "Attempting to connect to fog server...");
                            WebClient wc = new WebClient();
                            String strPath = strURLModuleStatus + "?mac=" + macList + "&moduleid=displaymanager";
                            strData = wc.DownloadString(strPath);
                            blConnectOK = true;
                        }
                        catch (Exception exp)
                        {
                            log(MOD_NAME, "Failed to connect to fog server!");
                            log(MOD_NAME, exp.Message);
                            log(MOD_NAME, exp.StackTrace);
                            log(MOD_NAME, "Sleeping for 1 minute.");
                            try
                            {
                                System.Threading.Thread.Sleep(60000);
                            }
                            catch { }
                        }
                    }
                    
                    strData = strData.Trim();
                    if (strData.StartsWith("#!ok", true, null))
                    {
                        log(MOD_NAME, "Module is active...");
                        blLoop = true;
                    }
                    else if (strData.StartsWith("#!db", true, null))
                    {
                        log(MOD_NAME, "Database error.");
                    }
                    else if (strData.StartsWith("#!im", true, null))
                    {
                        log(MOD_NAME, "Invalid MAC address format.");
                    }
                    else if (strData.StartsWith("#!ng", true, null))
                    {
                        log(MOD_NAME, "Module is disabled globally on the FOG Server.");
                    }
                    else if (strData.StartsWith("#!nh", true, null))
                    {
                        log(MOD_NAME, "Module is disabled on this host.");
                    }
                    else if (strData.StartsWith("#!um", true, null))
                    {
                        log(MOD_NAME, "Unknown Module ID passed to server.");
                    }
                    else
                    {
                        log(MOD_NAME, "Unknown error, module will exit.");
                    }

                    
                    if (blLoop)
                    {
                        Boolean blLgIn = isLoggedIn();
                        int X = -1;
                        int Y = -1;
                        int R = -1;
                        try
                        {
                            WebClient wc = new WebClient();
                            String strRes = wc.DownloadString(strURLDisplay + "?mac=" + macList);
                            strRes = strRes.Trim();
                            if (strRes != null)
                            {
                                String strDisplaySettings = decode64(strRes);
                                if (strDisplaySettings != null)
                                {
                                    String[] strParts = strDisplaySettings.Split('x');
                                    if (strParts.Length == 3)
                                    {
                                        try
                                        {
                                            X = int.Parse(strParts[0]);
                                        }
                                        catch 
                                        {
                                            blGo = false;
                                            log(MOD_NAME, "Unable to convert width into a valid integer.");
                                        }

                                        try
                                        {
                                            Y = int.Parse(strParts[1]);
                                        }
                                        catch
                                        {
                                            blGo = false;
                                            log(MOD_NAME, "Unable to convert height into a valid integer.");
                                        }

                                        try
                                        {
                                            R = int.Parse(strParts[2]);
                                        }
                                        catch
                                        {
                                            blGo = false;
                                            log(MOD_NAME, "Unable to convert refresh rate into a valid integer.");
                                        }
                                    }
                                    else
                                    {
                                        blGo = false;
                                        log(MOD_NAME, "Invalid server response.");
                                    }
                                }
                                else
                                {
                                    blGo = false;
                                    log(MOD_NAME, "Unable to determine screen resolution settings.");
                                }
                            }
                            else
                            {
                                blGo = false;
                                log(MOD_NAME, "Server response was null.");
                            }
                        }
                        catch (Exception exp)
                        {
                            log(MOD_NAME, exp.Message);
                            log(MOD_NAME, exp.StackTrace);
                            blGo = false;
                        }


                        log(MOD_NAME, "Starting display manager monitoring loop...");
                        Boolean blFirst = true;
                        DisplayChanger display = new DisplayChanger();
                        while (blGo)
                        {
                            Boolean blCurLgIn = isLoggedIn();

                            // if this is the first iteration of the loop
                            // and no one is logged in, then attempt to 
                            // do a cleanup.  This will cleanup users on a reboot
                            if (blFirst )
                            {
                                log(MOD_NAME, "Changing Display Setting to " + X + " x " + Y + " x " + R + " .");
                                if (!display.changeDisplaySettings(X, Y, R, 0))
                                {
                                    log(MOD_NAME, "Failed to change display settings.");
                                }
                                else
                                {
                                    log(MOD_NAME, "Display settings changed.");
                                }
                                blFirst = false;
                            }
                           

                            if (blLgIn != blCurLgIn)
                            {
                                if (blCurLgIn)
                                {
                                    log(MOD_NAME, "Login detected, taking action...");
                                    log(MOD_NAME, "Changing Display Setting to " + X + " x " + Y + " x " + R + ".");

                                    try
                                    {
                                        System.Threading.Thread.Sleep(20000);
                                    }
                                    catch (Exception)
                                    {

                                    }

                                    if (!display.changeDisplaySettings(X, Y, R, 0))
                                    {
                                        log(MOD_NAME, "Failed to change display settings.");
                                    }
                                    else
                                    {
                                        log(MOD_NAME, "Display settings changed.");
                                    }
                                }
                                blLgIn = blCurLgIn;
                            }

                            try
                            {
                                System.Threading.Thread.Sleep(30000);
                            }
                            catch (Exception )
                            {

                            }
                        }
                        log(MOD_NAME, "Module has finished work and will now exit.");
                    }
                }
                else
                {
                    log(MOD_NAME, "Unable to continue, MAC is null!");
                }

            }
            catch (Exception e)
            {
                pushMessage("FOG DisplayManager error:\n" + e.Message);
                log(MOD_NAME, e.Message);
                log(MOD_NAME, e.StackTrace);
            }
            finally
            {
            }
            intStatus = STATUS_TASKCOMPLETE;
            
        }

        public override Boolean mStop()
        {
            log(MOD_NAME, "Shutdown complete");
            blGo = false;
            return true;
        }

        public override int mGetStatus()
        {
            return intStatus;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEVMODE1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;

        public short dmOrientation;
        public short dmPaperSize;
        public short dmPaperLength;
        public short dmPaperWidth;

        public short dmScale;
        public short dmCopies;
        public short dmDefaultSource;
        public short dmPrintQuality;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public short dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;

        public int dmDisplayFlags;
        public int dmDisplayFrequency;

        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;

        public int dmPanningWidth;
        public int dmPanningHeight;
    };

    class UnmanagedWin32
    {
        [DllImport("user32.dll")]
        public static extern int EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE1 devMode);

        [DllImport("user32.dll")]
        public static extern int ChangeDisplaySettings(ref DEVMODE1 devMode, int flags);

        public const int ENUM_CURRENT_SETTINGS = -1;

        public const int CDS_UPDATEREGISTRY = 0x01;
        public const int CDS_TEST = 0x02;

        public const int DISP_CHANGE_SUCCESSFUL = 0;
        public const int DISP_CHANGE_RESTART = 1;
        public const int DISP_CHANGE_FAILED = -1;
    }

    class DisplayChanger
    {
        public DisplayChanger()
        {

        }

        public DEVMODE1[] getSupportedModes()
        {
            ArrayList alModes = new ArrayList();
            int intRet = 1;
            int intNum = 0;

            while (intRet != 0)
            {
                DEVMODE1 dm = new DEVMODE1();
                dm.dmDeviceName = new String(new char[32]);
                dm.dmFormName = new String(new char[32]);
                dm.dmSize = (short)Marshal.SizeOf(dm);

                intRet = UnmanagedWin32.EnumDisplaySettings(null, intNum++, ref dm);
                if (intRet != 0)
                {
                    alModes.Add(dm);
                }
            }
            return (DEVMODE1[])(alModes.ToArray(typeof(DEVMODE1)));
        }

        public Boolean changeDisplaySettings(int X, int Y, int refresh, int orientation)
        {
            DEVMODE1[] arDM = getSupportedModes();
            
            if (arDM != null && arDM.Length > 0)
            {
                
                for (int i = 0; i < arDM.Length; i++)
                {
                    if (arDM[i].dmPelsWidth == X && arDM[i].dmPelsHeight == Y && arDM[i].dmDisplayFrequency == refresh && arDM[i].dmOrientation == orientation)
                    {
                        
                        DEVMODE1 dmset = new DEVMODE1();
                        dmset.dmDeviceName = new String(new char[32]);
                        dmset.dmFormName = new String(new char[32]);
                        dmset.dmSize = (short)Marshal.SizeOf(dmset);
                        if (UnmanagedWin32.EnumDisplaySettings(null, UnmanagedWin32.ENUM_CURRENT_SETTINGS, ref dmset) != 0)
                        {
                            
                            dmset.dmPelsWidth = X;
                            dmset.dmPelsHeight = Y;
                            dmset.dmOrientation = (short)orientation;
                            dmset.dmDisplayFrequency = refresh;
                            
                            int intTest = UnmanagedWin32.ChangeDisplaySettings(ref dmset, UnmanagedWin32.CDS_TEST);
                            
                            if (intTest != UnmanagedWin32.DISP_CHANGE_FAILED)
                            {
                                
                                intTest = UnmanagedWin32.ChangeDisplaySettings(ref dmset, UnmanagedWin32.CDS_UPDATEREGISTRY);
                                if (intTest == UnmanagedWin32.DISP_CHANGE_SUCCESSFUL)
                                    return true;
                                else if (intTest == UnmanagedWin32.DISP_CHANGE_RESTART)
                                    return true;

                            }
                            
                        }
                        
                    }
                }
            }
            
            return false;
        }
    }
}
