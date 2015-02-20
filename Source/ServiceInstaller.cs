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
using System.ServiceProcess;
using System.Reflection;

namespace ServiceHosting
{
    internal class ServiceInstaller : IDisposable
    {
        private readonly ServiceController _scController;
        private readonly string _exePath;
        private readonly string _serviceName;
        private readonly string _displayName;
        private readonly string _description;

        public ServiceInstaller(string serviceName, string displayName, string description)
            : this(Assembly.GetEntryAssembly().Location, serviceName,
                   displayName, description)
        { }

        public ServiceInstaller(string exePath, string serviceName, string displayName, string description)
        {
            _serviceName = serviceName;
            _exePath = exePath;
            _displayName = displayName ?? serviceName;
            _description = description;
            _scController = new ServiceController(serviceName);
        }

        public bool InstallService()
        {
            WindowsServiceManager.InstallService(_serviceName, _displayName, _exePath);
            SetServiceDescription();

            // Wait a second
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1));

            // Refresh the service controller
            _scController.Refresh();
            _scController.ServiceName = _serviceName;

            return (_scController.Status == ServiceControllerStatus.Stopped);
        }

        public bool InstallService(string username, string password)
        {
            WindowsServiceManager.InstallService(_serviceName, _displayName, _exePath, username, password);
            SetServiceDescription();

            // Wait a second
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1));

            // Refresh the service controller
            _scController.Refresh();
            _scController.ServiceName = _serviceName;

            return (_scController.Status == ServiceControllerStatus.Stopped);
        }

        public bool InstallServiceAndStart()
        {
            WindowsServiceManager.InstallAndStart(_serviceName, _displayName, _exePath);
            SetServiceDescription();

            // Wait a second
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1));

            // Refresh the service controller
            _scController.Refresh();
            _scController.ServiceName = _serviceName;

            _scController.Start();

            return (_scController.Status == ServiceControllerStatus.StopPending || 
                _scController.Status == ServiceControllerStatus.Running);
        }

        public void UninstallService()
        {
            RemoveServiceDescription();
            WindowsServiceManager.Uninstall(_serviceName);
        }

        public void Dispose()
        {
            if (_scController != null)
                _scController.Dispose();

            // Supress this object's finalizer
            GC.SuppressFinalize(this);
        }

        private void SetServiceDescription()
        {
            Microsoft.Win32.RegistryKey system,
                //HKEY_LOCAL_MACHINE\Services\CurrentControlSet
              currentControlSet,
                //...\Services
              services,
                //...\<Service Name>
              service,
                //...\Parameters - this is where you can put service-specific configuration
              config;

            try
            {
                //Open the HKEY_LOCAL_MACHINE\SYSTEM key
                system = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("System");
                //Open CurrentControlSet
                currentControlSet = system.OpenSubKey("CurrentControlSet");
                //Go to the services key
                services = currentControlSet.OpenSubKey("Services");
                //Open the key for your service, and allow writing
                service = services.OpenSubKey(_serviceName, true);
                //Add your service's description as a REG_SZ value named "Description"
                service.SetValue("Description", _description);
                //(Optional) Add some custom information your service will use...
                config = service.CreateSubKey("Parameters");
            }
            catch (Exception e)
            {
                FlexibleServiceHost.Log.ErrorException("Error occurred while setting the service description.", e);
                throw;
            }
        }

        private void RemoveServiceDescription()
        {
            Microsoft.Win32.RegistryKey system,
              currentControlSet,
              services,
              service;

            try
            {
                //Drill down to the service key and open it with write permission
                system = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("System");
                currentControlSet = system.OpenSubKey("CurrentControlSet");
                services = currentControlSet.OpenSubKey("Services");
                service = services.OpenSubKey(_serviceName, true);
                //Delete any keys you created during installation (or that your service created)
                if (service.OpenSubKey("Parameters") != null)
                    service.DeleteSubKeyTree("Parameters");
                //...
            }
            catch (Exception e)
            {
                FlexibleServiceHost.Log.ErrorException("Error occurred while removing the service description.", e);
                throw;
            }
        }

    }
}
