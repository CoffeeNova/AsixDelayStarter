using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Automation;
using System.Runtime.InteropServices;

namespace AsixDelayedStarter
{
    class Tools
    {
        /// <summary>
        /// Возвращает список дескрипторов окон, имя класса которых className
        /// </summary>
        /// <param name="processID">id процесса</param>
        /// <param name="className">имя класса по которому производится выборка</param>
        /// <returns></returns>
        internal static List<IntPtr> GetWidgetWindowHandles(int processID, string className)
        {
            //get all windows handles
            List<IntPtr> rootWindows = GetRootWindowsOfProcess(processID);
            // find the handles witch contains widget window
            AutomationElement rootWindowAE;
            List<IntPtr> widgetHandles = new List<IntPtr>();
            foreach (IntPtr handle in rootWindows)
            {
                rootWindowAE = AutomationElement.FromHandle(handle);
                if (rootWindowAE == null)
                    continue;
                if (rootWindowAE.Current.ClassName == className)
                {
                    widgetHandles.Add(handle);
                }
            }
            return widgetHandles;
        }

        internal static List<IntPtr> GetRootWindowsOfProcess(int pid)
        {
            List<IntPtr> rootWindows = GetChildWindows(IntPtr.Zero);
            List<IntPtr> dsProcRootWindows = new List<IntPtr>();
            foreach (IntPtr hWnd in rootWindows)
            {
                uint lpdwProcessId;
                Interop.GetWindowThreadProcessId(hWnd, out lpdwProcessId);
                if (lpdwProcessId == pid)
                    dsProcRootWindows.Add(hWnd);
            }
            return dsProcRootWindows;
        }

        internal static List<IntPtr> GetChildWindows(IntPtr parent)
        {
            List<IntPtr> result = new List<IntPtr>();
            GCHandle listHandle = GCHandle.Alloc(result);
            try
            {
                Interop.Win32Callback childProc = new Interop.Win32Callback(EnumWindow);
                Interop.EnumChildWindows(parent, childProc, GCHandle.ToIntPtr(listHandle));
            }
            finally
            {
                if (listHandle.IsAllocated)
                    listHandle.Free();
            }
            return result;
        }

        internal static bool EnumWindow(IntPtr handle, IntPtr pointer)
        {
            GCHandle gch = GCHandle.FromIntPtr(pointer);
            List<IntPtr> list = gch.Target as List<IntPtr>;
            if (list == null)
            {
                throw new InvalidCastException("GCHandle Target could not be cast as List<IntPtr>");
            }
            list.Add(handle);
            //  You can modify this to check to see if you want to cancel the operation, then return a null here
            return true;
        }
    }
}
