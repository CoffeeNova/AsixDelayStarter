using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Management;
using NLog;
using System.Windows.Automation;

namespace AsixDelayedStarter
{
    //debug config 
    struct _Debug
    {
        public static bool smProcessExist;
        public static bool smHaveUI;
    }

    class Program
    {
        private static AsixDelayedStarter _ads;

        static void Main(string[] args)
        {
            if (ProgCopyCheck())
                return;

#if DEBUG
            //debug variables initialization
            //_Debug.smProcessExist = true;
            //_Debug.smHaveUI = true;
#endif
            Console.WriteLine("|--------------------------------------------|");
            Console.WriteLine("|Программа стартер для scada системы \"ASIX\"  |");
            Console.WriteLine("|       Developed by Igor Salzhetitin        |");
            Console.WriteLine("|          ОАО \"БЕЛСОЛОД\" 2016г              |");
            Console.WriteLine("|--------------------------------------------|");

            _ads = AsixDelayedStarter.Instance;
        }

        private static bool ProgCopyCheck()
        {
            var curProc = Process.GetCurrentProcess();
            var allProc = Process.GetProcesses();

            return allProc.Contains<Process>(curProc);
        }
    }

    public sealed class AsixDelayedStarter
    {
        private static Logger _log = LogManager.GetCurrentClassLogger();

        private static readonly Lazy<AsixDelayedStarter> _instance = new Lazy<AsixDelayedStarter>(() => new AsixDelayedStarter());

        public static AsixDelayedStarter Instance
        {
            get { return _instance.Value; }
        }

        private AsixDelayedStarter()
        {
#if !DEBUG
            Thread.Sleep(20000);
#endif
            //waiting for station manager process
            _log.Debug("start waiting for s7wnsmgx.exe");
            Console.Write("Ожидание запуска процесса s7wnsmgx.exe");
            while (!(StationManagerProcess() != null || _Debug.smProcessExist))
            { 
                Thread.Sleep(5000);
                Console.Write(".");
            }
            Console.Write("\r\n");  
            _log.Debug("s7wnsmgx.exe exist!");
            Console.WriteLine("Процесс s7wnsmgx.exe запущен");
            //waiting until station manager completes his configuration load
            //learn about it when station manager tracings his UI (button) in notification area
            _log.Debug("Waiting station manager loads his configuration");
            Console.Write("Ожидание завершения загрузки конфигурации Station Manager");
            while (!(StationManagerHaveUI() || _Debug.smHaveUI))
            { 
                Thread.Sleep(5000);
                Console.Write(".");
            }
            Console.Write("\r\n"); 
            _log.Debug("Station manager have icon in notification area");
            Console.WriteLine("Загрузка Station Manager завершена");
            Thread.Sleep(1000);
            Console.WriteLine("Запускаем Asix");

            if(!AsixProcessCheck())
                StartAsix();
            else
            {
                _log.Error("Asix already started");
                Console.WriteLine("Asix уже запущен");
            }
            Thread.Sleep(5000);

        }

        private Process StationManagerProcess()
        {
            string smProcName = "s7wnsmgx";
            var smProc = Process.GetProcessesByName(smProcName);

            return smProc.Length == 0 ? null : smProc[0];
        }

        private bool StationManagerHaveUI()
        {
            try
            {
                AutomationElement smButton = null; ;
                var rootElement = AutomationElement.RootElement;
                //it could be as in "overflow notification area" as in "user promoted notification area"
                //check visible area first
                var promNotifAreaAE = PromotedNotificationAreaAE(rootElement);
                if (promNotifAreaAE == null)
                _log.Debug("Get User Promoted Notification Area automation element successfully");
                smButton = StationManagerButtonAE(promNotifAreaAE);
                if (smButton != null)
                    return true;
                //then we go deeper to overflow notification area. First of all find notification chevron button 
                _log.Debug("Station manager have no icon in user promoted area, try to find it in hidden area");
                var notifChevButtonAE = NotificationChevronButtonAE(rootElement);
                if (notifChevButtonAE == null)
                {
                    _log.Debug("Notification Chevron Button AE not exist");
                    return false;
                }
                IntPtr foreWindowHwnd = Interop.GetForegroundWindow();
                InvokeAutomationElement(notifChevButtonAE); //click this fucking button
                //now we have new element named "Overflow Notification Area". find it. and find a child button on it.
                var notifOverflowAreaAE = NotificationOverflowAreaAE(rootElement);
                _log.Debug("Get User Promoted Notification Overflow Area automation element successfully");
                smButton = StationManagerButtonAE(notifOverflowAreaAE);
                Interop.SetForegroundWindow(foreWindowHwnd);
                if ( smButton!= null)
                    return true;
                else
                {
                    _log.Debug("Station manager have no icon in hidden promoted area also");
                    return false;
                }
            }
            catch { return false; }
        }

        private AutomationElement PromotedNotificationAreaAE(AutomationElement rootAE)
        {
            try
            {
                var shellTrayAE = rootAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
                if (shellTrayAE == null)
                    throw new Exception("Cant find Shell_TrayWnd automation element (is it Windows?)");
                var trayNotifyAE = shellTrayAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "TrayNotifyWnd"));
                if (trayNotifyAE == null)
                    throw new Exception("Cant find TrayNotifyWnd automation element (is it Windows?)");
                var sysPagerAE = trayNotifyAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "SysPager"));
                if (sysPagerAE == null)
                    throw new Exception("Cant find SysPager automation element (is it Windows?)");
                var toolbarAE = sysPagerAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ToolbarWindow32"));
                return toolbarAE;
            }
            catch(Exception ex)
            {
                _log.Error(ex.Message);
                 _log.Error(ex);
                 Console.WriteLine("Незапланированное завершение работы программы. Смотрите error.log.");
                System.Environment.Exit(0);
                return null;
            }
        }

        private AutomationElement StationManagerButtonAE(AutomationElement parentElement)
        {
            if (parentElement == null)
                return null;
            return parentElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "Station Configuration Editor"));
        }

        private AutomationElement NotificationChevronButtonAE(AutomationElement rootAE)
        {
            try
            {
                var shellTrayAE = rootAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
                if (shellTrayAE == null)
                    throw new Exception("Cant find Shell_TrayWnd automation element (is it Windows?)");
                var trayNotifyAE = shellTrayAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "TrayNotifyWnd"));
                if (trayNotifyAE == null)
                    throw new Exception("Cant find TrayNotifyWnd automation element (is it Windows?)");
                var notifyChevron = trayNotifyAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "NotificationChevron"));
                return notifyChevron;
            }
            catch(Exception ex)
            {
                _log.Error(ex.Message);
                _log.Error(ex);
                Console.WriteLine("Незапланированное завершение работы программы. Смотрите error.log.");
                System.Environment.Exit(0);
                return null;
            }
        }

        private AutomationElement NotificationOverflowAreaAE(AutomationElement rootAE)
        {
            try
            {
                var overflowWindowAE = rootAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "NotifyIconOverflowWindow"));
                if (overflowWindowAE == null)
                    throw new Exception("Cant find Notification Overflow Window automation element");
                var overflowNotifArea = overflowWindowAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ToolbarWindow32"));
                if (overflowNotifArea == null)
                    throw new Exception("Cant find Overflow Notification Area automation element");
                return overflowNotifArea;
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
                _log.Error(ex);
                Console.WriteLine("Незапланированное завершение работы программы. Смотрите error.log.");
                System.Environment.Exit(0);
                return null;
            }
        }
        private void InvokeAutomationElement(AutomationElement automationElement)
        {
            var invokePattern = automationElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            invokePattern.Invoke();
        }

        private void StartAsix()
        {
            Process pr = new Process();
            ProcessStartInfo prStartInfo = new ProcessStartInfo();
            prStartInfo.FileName = @"C:\AsixApp\Belsolod\Belsolod\Asix - Belsolod.lnk";
            //prStartInfo.FileName = @"C:\Program Files\Askom\Asix\as32.exe";
            //prStartInfo.Arguments = @" /8 Belsolod.xml Serwer_1";
            prStartInfo.UseShellExecute = true;
            prStartInfo.Verb = "runas";
            try
            {
                pr.StartInfo = prStartInfo;
                pr.Start();
                _log.Debug("Asix process started");
            }
            catch (System.ComponentModel.Win32Exception win32Ex)
            {
                //_log.Error(@"Can't find C:\Program Files\Askom\Asix\as32.exe");
                _log.Error(@"Can't find C:\AsixApp\Belsolod\Belsolod\Asix - Belsolod.lnk");
            }
            catch (InvalidOperationException ex)
            {
                _log.Error(ex.Message);
            }
        }

        private bool AsixProcessCheck()
        {
            return Process.GetProcessesByName("_as32").Length == 0 ? false : true;
        }

    }

    
}
