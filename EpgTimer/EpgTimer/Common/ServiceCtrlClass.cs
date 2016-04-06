using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceProcess;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace EpgTimer
{
    class ServiceCtrlClass
    {
        private const int STANDARD_RIGHTS_REQUIRED = 0xF0000;
        private const int SERVICE_WIN32_OWN_PROCESS = 0x00000010;

        [StructLayout(LayoutKind.Sequential)]
        private class SERVICE_STATUS
        {
            public int dwServiceType = 0;
            public ServiceState dwCurrentState = 0;
            public int dwControlsAccepted = 0;
            public int dwWin32ExitCode = 0;
            public int dwServiceSpecificExitCode = 0;
            public int dwCheckPoint = 0;
            public int dwWaitHint = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class QUERY_SERVICE_CONFIG
        {
            public int dwServiceType = 0;
            public int dwStartType = 0;
            public int dwErrorControl = 0;
            public IntPtr lpBinaryPathName = IntPtr.Zero;
            public IntPtr lpLoadOrderGroup = IntPtr.Zero;
            public int dwTagId = 0;
            public IntPtr lpDependencies = IntPtr.Zero;
            public IntPtr lpServiceStartName = IntPtr.Zero;
            public IntPtr lpDisplayName = IntPtr.Zero;
        }

        #region OpenSCManager
        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr OpenSCManager(string machineName, string databaseName, ScmAccessRights dwDesiredAccess);
        #endregion

        #region OpenService
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, ServiceAccessRights dwDesiredAccess);
        #endregion

        #region CreateService
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, ServiceAccessRights dwDesiredAccess, int dwServiceType, ServiceBootFlag dwStartType, ServiceError dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies, string lp, string lpPassword);
        #endregion

        #region CloseServiceHandle
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseServiceHandle(IntPtr hSCObject);
        #endregion

        #region QueryServiceStatus
        [DllImport("advapi32.dll")]
        private static extern int QueryServiceStatus(IntPtr hService, SERVICE_STATUS lpServiceStatus);
        #endregion

        #region QueryServiceConfig
        [DllImport("advapi32.dll", SetLastError = true, CharSet=CharSet.Unicode)]
        private static extern int QueryServiceConfig(IntPtr hService, IntPtr lpServiceConfig, int cbBufSize, ref int pcbBytesNeeded);
        #endregion

        #region DeleteService
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteService(IntPtr hService);
        #endregion

        #region ControlService
        [DllImport("advapi32.dll")]
        private static extern int ControlService(IntPtr hService, ServiceControl dwControl, SERVICE_STATUS lpServiceStatus);
        #endregion

        #region StartService
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int StartService(IntPtr hService, int dwNumServiceArgs, int lpServiceArgVectors);
        #endregion

        public static string QueryServiceExePath(string serviceName)
        {
            string exePath = null;

            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);
            if (scm != IntPtr.Zero)
            {
                IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus | ServiceAccessRights.QueryConfig);
                if (service != IntPtr.Zero)
                {
                    int bytesNeeded = 1000;
                    IntPtr qscPtr = Marshal.AllocCoTaskMem(bytesNeeded);
                    int ret = QueryServiceConfig(service, qscPtr, bytesNeeded, ref bytesNeeded);
                    if (ret == 0)
                    {
                        qscPtr = Marshal.AllocCoTaskMem(bytesNeeded);
                        ret = QueryServiceConfig(service, qscPtr, bytesNeeded, ref bytesNeeded);
                    }
                    if (ret > 0)
                    {
                        QUERY_SERVICE_CONFIG qsc = Marshal.PtrToStructure(qscPtr, typeof(QUERY_SERVICE_CONFIG)) as QUERY_SERVICE_CONFIG;
                        exePath = Marshal.PtrToStringUni(qsc.lpBinaryPathName);
                    }
                }
                CloseServiceHandle(service);
            }
            CloseServiceHandle(scm);
            return exePath;
        }

        public static bool Install(string serviceName, string displayName, string fileName)
        {
            bool ret = false;
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);
            if (scm == IntPtr.Zero)
            {
                return false;
            }

            IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);
            if (service == IntPtr.Zero)
            {
                ret = File.Exists(fileName) == true && RunAs(fileName, "-install") == 0;
            }
            else
            {
                CloseServiceHandle(service);
            }

            CloseServiceHandle(scm);
            return ret;
        }

        public static bool Uninstall(string serviceName)
        {
            string fileName = QueryServiceExePath(serviceName);
            if (fileName == null)
            {
                return false;
            }

            return File.Exists(fileName) == true && RunAs(fileName, "-remove") == 0;
        }

        public static bool ServiceIsInstalled(string serviceName)
        {
            bool ret = false;
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);
            if (scm == IntPtr.Zero)
            {
                return false;
            }

            IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);
            if (service != IntPtr.Zero)
            {
                ret = true;
                CloseServiceHandle(service);
            }

            CloseServiceHandle(scm);
            return ret;
        }

        public static bool IsStarted(string serviceName)
        {
            bool ret = false;
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);
            if (scm == IntPtr.Zero)
            {
                return false;
            }

            IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);
            if (service != IntPtr.Zero)
            {
                ServiceState status = GetServiceStatus(service);
                if (status == ServiceState.Running)
                {
                    ret = true;
                }
                CloseServiceHandle(service);
            }

            CloseServiceHandle(scm);
            return ret;
        }


        public static bool StartService(string serviceName)
        {
            bool ret = false;
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);
            if (scm == IntPtr.Zero)
            {
                return false;
            }

            IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);
            if (service != IntPtr.Zero)
            {
                //ret = StartService(service);
                if (RunAs("net.exe", "start \"" + serviceName + "\"") == 0)
                {
                    ret = WaitForServiceStatus(service, ServiceState.StartPending, ServiceState.Running);
                }
                CloseServiceHandle(service);
            }

            CloseServiceHandle(scm);
            return ret;
        }

        public static bool StopService(string serviceName)
        {
            bool ret = false;
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);
            if (scm == IntPtr.Zero)
            {
                return false;
            }

            IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);
            if (service != IntPtr.Zero)
            {
                //ret = StopService(service);
                if (RunAs("net.exe", "stop \"" + serviceName + "\"") == 0)
                {
                    ret = WaitForServiceStatus(service, ServiceState.StartPending, ServiceState.Stopped);
                }
                CloseServiceHandle(service);
            }
            CloseServiceHandle(scm);
            return ret;
        }

#if false
        private static bool StartService(IntPtr service)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();
            if (StartService(service, 0, 0) == 0)
            {
                return false;
            }
            var changedStatus = WaitForServiceStatus(service, ServiceState.StartPending, ServiceState.Running);
            if (!changedStatus)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
#endif

#if false
        private static bool StopService(IntPtr service)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();
            if (ControlService(service, ServiceControl.Stop, status) == 0)
            {
                return false;
            }
            var changedStatus = WaitForServiceStatus(service, ServiceState.StopPending, ServiceState.Stopped);
            if (!changedStatus)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
#endif

#if false
        public static ServiceState GetServiceStatus(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);

            try
            {
                IntPtr service = OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);
                if (service == IntPtr.Zero)
                    return ServiceState.NotFound;

                try
                {
                    return GetServiceStatus(service);
                }
                finally
                {
                    CloseServiceHandle(service);
                }
            }
            finally
            {
                CloseServiceHandle(scm);
            }
        }
#endif

        private static ServiceState GetServiceStatus(IntPtr service)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();

            if (QueryServiceStatus(service, status) == 0)
            {
                return ServiceState.Unknown;
            }

            return status.dwCurrentState;
        }

        private static bool WaitForServiceStatus(IntPtr service, ServiceState waitStatus, ServiceState desiredStatus)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();

            QueryServiceStatus(service, status);
            if (status.dwCurrentState == desiredStatus)
            {
                return true;
            }

            int count = 0;
            while (count<60)
            {
                 Thread.Sleep(1000);
                 QueryServiceStatus(service, status);
                 if (status.dwCurrentState == desiredStatus)
                 {
                     return true;
                 }
                 else
                 {
                     count++;
                 }
            }
            return false;
        }

        private static IntPtr OpenSCManager(ScmAccessRights rights)
        {
            IntPtr scm = OpenSCManager(null, null, rights);
            /*if (scm == IntPtr.Zero)
                throw new ApplicationException("Could not connect to service control manager.");*/

            return scm;
        }

        private static int RunAs(string exe, string args)
        {
            int ret = -1;
            try
            {
                Process proc = new Process();
                proc.StartInfo.FileName = exe;
                proc.StartInfo.Arguments = args;
                proc.StartInfo.Verb = "RunAs";  // XPでも有効なのかは未確認
                proc.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
                proc.Start();
                proc.WaitForExit();
                ret = proc.ExitCode;
                proc.Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message + "\r\n" + ex.StackTrace);
            }
            return ret;
        }
    }


    public enum ServiceState
    {
        Unknown = -1, // The state cannot be (has not been) retrieved. 
        NotFound = 0, // The service is not known on the host server. 
        Stopped = 1,
        StartPending = 2,
        StopPending = 3,
        Running = 4,
        ContinuePending = 5,
        PausePending = 6,
        Paused = 7
    }

    [Flags]
    public enum ScmAccessRights
    {
        Connect = 0x0001,
        CreateService = 0x0002,
        EnumerateService = 0x0004,
        Lock = 0x0008,
        QueryLockStatus = 0x0010,
        ModifyBootConfig = 0x0020,
        StandardRightsRequired = 0xF0000,
        AllAccess = (StandardRightsRequired | Connect | CreateService |
                     EnumerateService | Lock | QueryLockStatus | ModifyBootConfig)
    }

    [Flags]
    public enum ServiceAccessRights
    {
        QueryConfig = 0x1,
        ChangeConfig = 0x2,
        QueryStatus = 0x4,
        EnumerateDependants = 0x8,
        Start = 0x10,
        Stop = 0x20,
        PauseContinue = 0x40,
        Interrogate = 0x80,
        UserDefinedControl = 0x100,
        Delete = 0x00010000,
        StandardRightsRequired = 0xF0000,
        AllAccess = (StandardRightsRequired | QueryConfig | ChangeConfig |
                     QueryStatus | EnumerateDependants | Start | Stop | PauseContinue |
                     Interrogate | UserDefinedControl)
    }

    public enum ServiceBootFlag
    {
        Start = 0x00000000,
        SystemStart = 0x00000001,
        AutoStart = 0x00000002,
        DemandStart = 0x00000003,
        Disabled = 0x00000004
    }

    public enum ServiceControl
    {
        Stop = 0x00000001,
        Pause = 0x00000002,
        Continue = 0x00000003,
        Interrogate = 0x00000004,
        Shutdown = 0x00000005,
        ParamChange = 0x00000006,
        NetBindAdd = 0x00000007,
        NetBindRemove = 0x00000008,
        NetBindEnable = 0x00000009,
        NetBindDisable = 0x0000000A
    }

    public enum ServiceError
    {
        Ignore = 0x00000000,
        Normal = 0x00000001,
        Severe = 0x00000002,
        Critical = 0x00000003
    } 
}
