﻿using System;
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
            _Debug.smProcessExist = true;
#endif

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
            //waiting for station manager process
            Process cmProc = StationManagerProcess();
            _log.Debug("start waiting for s7wnsmgx.exe");
            while (!(cmProc == null || _Debug.smProcessExist))
            { 
                Thread.Sleep(5000);
                cmProc = StationManagerProcess();
            }
                
            _log.Debug("s7wnsmgx.exe exist!");
            
            //waiting until station manager completes his configuration load
            //learn about it when station manager tracings his UI (button) in notification area
            StationManagerHaveUI(cmProc);

        }

        private Process StationManagerProcess()
        {
            string smProcName = "s7wnsmgx";
            var smProc = Process.GetProcessesByName(smProcName);

            return smProc.Length == 0 ? null : smProc[0];
        }

        private void StationManagerHaveUI(Process process)
        {
            try
            {
                var rootElement = AutomationElement.RootElement;
                //it could be as in "overflow notification area" as in "user promoted notification area"
                //check visible area first
                var promNotifAreaAE = PromotedNotificationAreaAE(rootElement);
                _log.Debug("Get User Promoted Notification Area automation element successfully");

                //AutomationElement notifOverflowArea = AutomationElement.RootElement
                var shellTrayAE = rootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "Shell_TrayWnd"));
                if (shellTrayAE == null)
                    throw new Exception("Cant find Shell_TrayWnd automation element (is it Windows?)");
                var trayNotifyAE = shellTrayAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "TrayNotifyWnd"));
                if (trayNotifyAE == null)
                    throw new Exception("Cant find TrayNotifyWnd automation element (is it Windows?)");
                var notifyChevron = trayNotifyAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "Notification Chevron"));
                if (trayNotifyAE == null)
                    throw new Exception("Cant find Notification Chevron automation element (is it Windows?)");
                InvokeAutomationElement(notifyChevron);
            }
            catch(Exception ex)
            {
                _log.Error(ex.Message);
                System.Environment.Exit(0);
            }
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
                var sysPagerAE = shellTrayAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "SysPager"));
                if (sysPagerAE == null)
                    throw new Exception("Cant find SysPager automation element (is it Windows?)");
                var toolbarAE = shellTrayAE.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ClassNameProperty, "ToolbarWindow32"));
                return toolbarAE;
            }
            catch(Exception ex)
            {
                _log.Error(ex.Message);
                System.Environment.Exit(0);
                return null;
            }
        }

        private bool IsStationManagerButtonExist(AutomationElement element)
        {
            return element.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "Station Configuration Editor")) == null ? false : true;
        }
        private void InvokeAutomationElement(AutomationElement automationElement)
        {
            var invokePattern = automationElement.GetCurrentPattern(InvokePattern.Pattern) as InvokePattern;
            invokePattern.Invoke();
        }

    }

    
}