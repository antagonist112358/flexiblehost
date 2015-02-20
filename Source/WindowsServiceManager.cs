/*
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ServiceHosting
{
    #region PInvoke Required Enumerations

    internal enum ServiceState
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
    internal enum ScmAccessRights
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
    internal enum ServiceAccessRights
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

    internal enum ServiceBootFlag
    {
        Start = 0x00000000,
        AutoStart = 0x00000002,
        DemandStart = 0x00000003,
        Disabled = 0x00000004
    }

    internal enum ServiceControl
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

    internal enum ServiceError
    {
        Ignore = 0x00000000,
        Normal = 0x00000001,
        Severe = 0x00000002,
        Critical = 0x00000003
    }

    #endregion

    internal static class WindowsServiceManager
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

        /// <summary>
        /// P/Invoke NativeMethods wrapper.
        /// </summary>
        private static class NativeMethods
        {
            #region OpenSCManager
            [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr OpenSCManager(string machineName, string databaseName, ScmAccessRights dwDesiredAccess);
            #endregion

            #region OpenService
            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, ServiceAccessRights dwDesiredAccess);
            #endregion

            #region CreateService
            [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateService(IntPtr hSCManager, string lpServiceName, string lpDisplayName, ServiceAccessRights dwDesiredAccess, int dwServiceType, ServiceBootFlag dwStartType, ServiceError dwErrorControl, string lpBinaryPathName, string lpLoadOrderGroup, IntPtr lpdwTagId, string lpDependencies, string lp, string lpPassword);
            #endregion

            #region CloseServiceHandle
            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseServiceHandle(IntPtr hSCObject);
            #endregion

            #region QueryServiceStatus
            [DllImport("advapi32.dll")]
            public static extern int QueryServiceStatus(IntPtr hService, SERVICE_STATUS lpServiceStatus);
            #endregion

            #region DeleteService
            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeleteService(IntPtr hService);
            #endregion

            #region ControlService
            [DllImport("advapi32.dll")]
            public static extern int ControlService(IntPtr hService, ServiceControl dwControl, SERVICE_STATUS lpServiceStatus);
            #endregion

            #region StartService
            [DllImport("advapi32.dll", SetLastError = true)]
            public static extern int StartService(IntPtr hService, int dwNumServiceArgs, [MarshalAs(UnmanagedType.LPWStr)] string lpServiceArgVectors);
            #endregion
        }
      
        internal static void Uninstall(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.AllAccess);

            try
            {
                IntPtr service = NativeMethods.OpenService(scm, serviceName, ServiceAccessRights.AllAccess);
                if (service == IntPtr.Zero)
                    throw new ServiceHostException("Service not installed.");

                try
                {
                    StopService(service);
                    if (!NativeMethods.DeleteService(service))
                        throw new ServiceHostException("Could not delete service " + Marshal.GetLastWin32Error());
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(service);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(scm);
            }
        }

        internal static bool ServiceIsInstalled(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);

            try
            {
                IntPtr service = NativeMethods.OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);

                if (service == IntPtr.Zero)
                    return false;

                NativeMethods.CloseServiceHandle(service);
                return true;
            }
            finally
            {
                NativeMethods.CloseServiceHandle(scm);
            }
        }

        internal static void InstallAndStart(string serviceName, string displayName, string fileName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.AllAccess);

            try
            {
                IntPtr service = NativeMethods.OpenService(scm, serviceName, ServiceAccessRights.AllAccess);

                if (service == IntPtr.Zero)
                    service = NativeMethods.CreateService(scm, serviceName, displayName, ServiceAccessRights.AllAccess, SERVICE_WIN32_OWN_PROCESS, ServiceBootFlag.AutoStart, ServiceError.Normal, fileName, null, IntPtr.Zero, null, null, null);

                if (service == IntPtr.Zero)
                    throw new ServiceHostException("Failed to install service.");

                try
                {
                    StartService(service);
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(service);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(scm);
            }
        }

        internal static void InstallService(string serviceName, string displayName, string fileName, 
            string username = null, string password = null)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.AllAccess);

            try
            {
                IntPtr service = NativeMethods.OpenService(scm, serviceName, ServiceAccessRights.AllAccess);

                if (service == IntPtr.Zero)
                    service = NativeMethods.CreateService(scm, serviceName, displayName, ServiceAccessRights.AllAccess, SERVICE_WIN32_OWN_PROCESS, ServiceBootFlag.AutoStart, ServiceError.Normal, fileName, null, IntPtr.Zero, null, username, password);

                if (service == IntPtr.Zero)
                    throw new ServiceHostException("Failed to install service.");

            }
            finally
            {
                NativeMethods.CloseServiceHandle(scm);
            }
        }

        internal static void StartService(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);

            try
            {
                IntPtr service = NativeMethods.OpenService(scm, serviceName, ServiceAccessRights.QueryStatus | ServiceAccessRights.Start);
                if (service == IntPtr.Zero)
                    throw new ServiceHostException("Could not open service.");

                try
                {
                    StartService(service);
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(service);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(scm);
            }
        }

        internal static void StopService(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);

            try
            {
                IntPtr service = NativeMethods.OpenService(scm, serviceName, ServiceAccessRights.QueryStatus | ServiceAccessRights.Stop);
                if (service == IntPtr.Zero)
                    throw new ServiceHostException("Could not open service.");

                try
                {
                    StopService(service);
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(service);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(scm);
            }
        }

        internal static ServiceState GetServiceStatus(string serviceName)
        {
            IntPtr scm = OpenSCManager(ScmAccessRights.Connect);

            try
            {
                IntPtr service = NativeMethods.OpenService(scm, serviceName, ServiceAccessRights.QueryStatus);
                if (service == IntPtr.Zero)
                    return ServiceState.NotFound;

                try
                {
                    return GetServiceStatus(service);
                }
                finally
                {
                    NativeMethods.CloseServiceHandle(service);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(scm);
            }
        }
        
        private static void StartService(IntPtr service)
        {            
            int HRESULT = NativeMethods.StartService(service, 0, null);
            if (HRESULT != 0) { throw new ServiceHostException("Unable to execute Windows Service control to Start state - Error code: {0}", HRESULT); }
            var changedStatus = WaitForServiceStatus(service, ServiceState.StartPending, ServiceState.Running);
            if (!changedStatus)
                throw new ServiceHostException("Unable to start service");
        }

        private static void StopService(IntPtr service)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();
            int HRESULT = NativeMethods.ControlService(service, ServiceControl.Stop, status);
            if (HRESULT != 0) { throw new ServiceHostException("Unable to execute Windows Service control to Stop state - Error code: {0}", HRESULT); }

            if (status.dwCurrentState != ServiceState.Stopped)
            {
                bool changedStatus = WaitForServiceStatus(service, ServiceState.StopPending, ServiceState.Stopped);
                if (!changedStatus)
                    throw new ServiceHostException("Unable to stop service");
            }
        }

        private static ServiceState GetServiceStatus(IntPtr service)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();

            if (NativeMethods.QueryServiceStatus(service, status) == 0)
                throw new ServiceHostException("Failed to query service status.");

            return status.dwCurrentState;
        }

        private static bool WaitForServiceStatus(IntPtr service, ServiceState waitStatus, ServiceState desiredStatus)
        {
            SERVICE_STATUS status = new SERVICE_STATUS();

            int HRESULT = NativeMethods.QueryServiceStatus(service, status);

            if (HRESULT != 0) { throw new ServiceHostException("Unable to query Windows Service status - Error code: {0}", HRESULT); }
            else if (status.dwCurrentState == desiredStatus) return true;

            int dwStartTickCount = Environment.TickCount;
            int dwOldCheckPoint = status.dwCheckPoint;

            while (status.dwCurrentState == waitStatus)
            {
                // Do not wait longer than the wait hint. A good interval is 
                // one tenth the wait hint, but no less than 1 second and no 
                // more than 10 seconds. 

                int dwWaitTime = status.dwWaitHint / 10;

                if (dwWaitTime < 1000) dwWaitTime = 1000;
                else if (dwWaitTime > 10000) dwWaitTime = 10000;

                Thread.Sleep(dwWaitTime);

                // Check the status again. 

                if (NativeMethods.QueryServiceStatus(service, status) == 0) break;

                if (status.dwCheckPoint > dwOldCheckPoint)
                {
                    // The service is making progress. 
                    dwStartTickCount = Environment.TickCount;
                    dwOldCheckPoint = status.dwCheckPoint;
                }
                else
                {
                    if (Environment.TickCount - dwStartTickCount > status.dwWaitHint)
                    {
                        // No progress made within the wait hint 
                        break;
                    }
                }
            }
            return (status.dwCurrentState == desiredStatus);
        }

        private static IntPtr OpenSCManager(ScmAccessRights rights)
        {
            IntPtr scm = NativeMethods.OpenSCManager(null, null, rights);
            if (scm == IntPtr.Zero)
                throw new ServiceHostException("Could not connect to service control manager.");

            return scm;
        }
    }

}
