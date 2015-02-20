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
using Culture = System.Globalization.CultureInfo;

using NLog;

namespace ServiceHosting
{
    /// <summary>
    /// Implementation of the base Windows Service class, which includes all the internal FlexibleServiceHost functionality and integration.
    /// </summary>
    public class FlexibleServiceHost : ServiceBase
    {
        // Logger
        private static Logger log = LogManager.GetCurrentClassLogger();
        
        // Unhandled exception handler
        private static UnhandledExceptionEventHandler threadExceptionHandler = ThreadExceptionHandler;


        private static FlexibleServiceHost mServiceHost;
        private static object mSyncRoot = new object();
        private static volatile bool mHandlerAssigned = false;
        private static volatile bool mHostedServiceRunning = false;

        #region Protected Static Methods

        private static void ThreadExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            log.FatalException("Unexpected error occurred. System is shutting down.", (Exception)e.ExceptionObject);
            TerminateHosting((Exception)e.ExceptionObject);
        }

        /// <summary>
        /// Starts a FlexibleService host, which begins hosting an <see cref="IHostedService">IHostedService</see> object instance.
        /// </summary>
        /// <param name="host">The FlexibleService host to begin hosting.</param>
        /// <param name="args">Any command line arguments to pass to the host.</param>
        protected static void StartServiceHost(FlexibleServiceHost host, string[] args)
        {
            // Check for no hosted service
            if (!HostInitialized)
                return;

            // Try to start the service
            try
            {
                host.HostedService.Start(args);
                mHostedServiceRunning = true;

                log.Info(Culture.CurrentCulture, "Now hosting: {0}.", (host.HostedService).GetType().Name);
            }
            catch (Exception ex)
            {
                log.FatalException("Could not start hosted service '" + host.HostedService.GetType().Name + "'.", ex);

                // Check if we need to dispose of the hosted service
                if (host.mIsHostedServiceDisposable)
                {
                    try
                    {
                        IDisposable disposer = (IDisposable)host.HostedService;
                        disposer.Dispose();
                    }
                    catch (Exception disEx)
                    {
                        log.ErrorException("Could not dispose of the hosted service, even though it implements IDisposable.", disEx);
                    }
                }

                // If we are hosting from within a windows service, exit the environment. If it is a console host,
                // rethrow the exception.
                if (host.HostingType == ServiceHostType.WindowsService)
                    Environment.Exit(1);
                else
                    throw;
            }
        }

        /// <summary>
        /// Instructs a FlexibleService host instance to stop hosting.
        /// </summary>
        /// <param name="host">The host object to stop.</param>
        protected static void StopServiceHost(FlexibleServiceHost host)
        {
            // Check for no host
            if (!HostInitialized)
                return;

            // Use this to track what to rethrow later
            Exception reThrow = null;

            // Log that we are stopping the hosted service
            log.Info(Culture.CurrentCulture, "Stop requested for {0}...", mServiceHost.ServiceName);

            // Check to see if we need to shutdown and stop the service
            if (mHostedServiceRunning)
            {
                // Make sure we handle 
                try
                {
                    host.HostedService.BeginShutdown();
                    host.HostedService.Terminate();
                    log.Info(Culture.CurrentCulture, "Gracefully stopped hosted service: {0}.", host.HostedService.GetType().Name);
                }
                catch (Exception ex)
                {
                    reThrow = ex;
                    log.FatalException("Could not properly stop and shutdown hosted service.", ex);
                }
                finally
                {
                    mHostedServiceRunning = false;
                }
            }

            // Next, attempt to dispose of the hosted service if necessary
            if (host.mIsHostedServiceDisposable)
            {
                // Make sure we handle
                try
                {
                    IDisposable disposable = (IDisposable)host.HostedService;
                    disposable.Dispose();
                    log.Info(Culture.CurrentCulture, "Properly disposed of hosted service: {0}.", host.HostedService.GetType().Name);
                }
                catch (Exception ex)
                {
                    if (reThrow == null) { reThrow = ex; }
                    log.ErrorException("Could not properly dispose of hosted service's resources.", ex);
                }
            }

            // Finally, mark all that junk as "null"
            lock (mSyncRoot)
            {
                mServiceHost = null;
            }


            // Finally, figure out what to do
            if (host.HostingType == ServiceHostType.WindowsService)
            {
                if (reThrow != null)
                    Environment.Exit(1);
                else
                    return;
            }
            else
            {
                if (reThrow != null)
                    throw reThrow;
                else
                    return;
            }
        }

        #endregion

        #region Public Static Properties

        /// <summary>
        /// Log for the FlexibleService host.
        /// </summary>
        public static Logger Log
        {
            get { return log; }
        }

        /// <summary>
        /// Gets whether the FlexibleService host is initialized.
        /// </summary>
        public static bool HostInitialized
        {
            get
            {
                lock (mSyncRoot)
                {
                    return (mServiceHost != null);
                }
            }
        }

        /// <summary>
        /// Gets whether the FlexibleService host is running.
        /// </summary>
        public static bool HostRunning
        {
            get { return mHostedServiceRunning; }
        }

        /// <summary>
        /// Returns the current instance of the FlexibleService host.
        /// </summary>
        public static FlexibleServiceHost ServiceHost
        {
            get
            {
                lock (mSyncRoot)
                {
                    return (mServiceHost);
                }
            }
        }

        #endregion

        #region Public Static Hosting Methods

        /// <summary>
        /// Starts hosting the currently registered service.
        /// </summary>
        public static void BeginHosting()
        {
            if (!HostInitialized)
            {
                throw new InvalidOperationException("Hosted service must be initialized first.");
            }
            
            // Log about the hosting type
            log.Info(Culture.CurrentCulture, "Starting {0} {1}...", mServiceHost.ServiceName, (mServiceHost.HostingType == ServiceHostType.Console) ?
                "in console mode." : "as a service.");

            if (ServiceHost.HostingType == ServiceHostType.WindowsService)
            {                
                ServiceBase.Run(FlexibleServiceHost.ServiceHost);
            }
            else
            {
                ServiceHost.OnStart(null);
            }
        }

        /// <summary>
        /// Stops hosting the currently registered service.
        /// </summary>
        public static void EndHosting()
        {
            if (HostInitialized)
            {
                ServiceHost.OnStop();
            }
        }

        /// <summary>
        /// Terminates the host under an error condition, which will subsequently stop the registered service.
        /// </summary>
        /// <param name="faultEx">The exception which caused the fault.</param>
        public static void TerminateHosting(Exception faultEx)
        {
            if (HostInitialized)
            {
                ServiceHost.OnStop();
            }

            // Finally, figure out how to exit
            if (ServiceHost.HostingType == ServiceHostType.Console)
            {
                throw faultEx;
            }
            else
            {
                Environment.Exit(1);
            }

        }

        /// <summary>
        /// Initializes the flexible service host for a specific <see cref="IHostedService">IHostedService</see> object.
        /// </summary>
        /// <typeparam name="T">The compile time type of the object which will be hosted.</typeparam>
        /// <param name="serviceName">The name of the hosted service.</param>
        /// <param name="hostType">An enumeration of either Console or Windows Service.</param>
        public static void InitializeHostedService<T>(string serviceName, ServiceHostType hostType) where T : IHostedService
        {
            InitializeHostedService<T>(serviceName, hostType, null);
        }
        /// <summary>
        /// Initializes the flexible service host for a specific <see cref="IHostedService">IHostedService</see> object.
        /// </summary>
        /// <typeparam name="T">The compile time type of the object which will be hosted.</typeparam>
        /// <param name="serviceName">The name of the hosted service.</param>
        /// <param name="hostType">An enumeration of either Console or Windows Service.</param>
        /// <param name="initializerArguments">Constructor parameters for creating an instance of the hosted service object.</param>
        public static void InitializeHostedService<T>(string serviceName, ServiceHostType hostType, params object[] initializerArguments) where T : IHostedService
        {
            // Instance of the hosted type
            IHostedService hostedService = null;

            // Try to instantiate a new instance of the provided type
            try
            {
                hostedService = (IHostedService)Activator.CreateInstance(typeof(T), initializerArguments);
            }
            catch (Exception ex)
            {
                log.FatalException("Could not create instance of " + typeof(T).Name + "!", ex);

                if (hostType == ServiceHostType.Console)
                    throw;
                else
                    Environment.Exit(1);
            }

            // Now setup the hosted instance
            lock (mSyncRoot)
            {
                mServiceHost = new FlexibleServiceHost(serviceName, hostType, hostedService);
            }
        }

        /// <summary>
        /// Installs the current FlexibleService host into the Windows Services registry.
        /// </summary>
        /// <param name="serviceName">The service name of the Windows Service.</param>
        /// <param name="displayName">The display name (name displayed in the Windows Services dialog) of the service.</param>
        /// <param name="serviceDescription">The description provided for the Windows Service.</param>
        /// <returns>True if the service was installed successfully, false otherwise.</returns>
        public static bool InstallWindowsServiceHost(string serviceName, string displayName, string serviceDescription)
        {
            ServiceInstaller svcInstaller = new ServiceInstaller(serviceName, displayName, serviceDescription);

            return svcInstaller.InstallService();
        }

        /// <summary>
        /// Installs the current FlexibleService host into the Windows Services registry.
        /// </summary>
        /// <param name="serviceName">The service name of the Windows Service.</param>
        /// <param name="displayName">The display name (name displayed in the Windows Services dialog) of the service.</param>
        /// <param name="serviceDescription">The description provided for the Windows Service.</param>
        /// <param name="userName">Username the Windows Service will be installed under.</param>
        /// <param name="password">Password for the user which the service will be installed with.</param>
        /// <returns>True if the service was installed successfully, false otherwise.</returns>
        public static bool InstallWindowsServiceHost(string serviceName, string displayName, string serviceDescription,
            string userName, string password)
        {
            var svcInstaller = new ServiceInstaller(serviceName, displayName, serviceDescription);

            return svcInstaller.InstallService(userName, password);
        }

        /// <summary>
        /// Uninstalls (removes) the FlexibleService host service from the Windows Services registry.
        /// </summary>
        /// <param name="serviceName">The name of the service (serviceName) to remove.</param>
        public static void UninstallWindowsServiceHost(string serviceName)
        {
            ServiceInstaller svcInstaller = new ServiceInstaller(serviceName, null, null);

            svcInstaller.UninstallService();
        }


        #endregion

        #region Instance Specific functionality

        private readonly ServiceHostType mHostType;
        private readonly IHostedService mHostedService;
        private readonly bool mIsHostedServiceDisposable;

        /// <summary>
        /// Used only by the installer.
        /// </summary>
        /// <param name="serviceName">The name of the service to install.</param>
        private FlexibleServiceHost(string serviceName)
        {
            base.ServiceName = serviceName;
        }

        private FlexibleServiceHost(string serviceName, ServiceHostType hostType, IHostedService hostedService)
        {
            base.ServiceName = serviceName;
            mHostedService = hostedService;
            mHostType = hostType;

            // Check for disposability
            IDisposable disposable = hostedService as IDisposable;
            mIsHostedServiceDisposable = (disposable != null);
        }

        /// <summary>
        /// Gets the current host type (an enumeration of either Console or Windows Service).
        /// </summary>
        public ServiceHostType HostingType
        {
            get { return mHostType; }
        }

        /// <summary>
        /// Gets the currently hosted object implementing <see cref="IHostedService">IHostedService</see>.
        /// </summary>
        public IHostedService HostedService
        {
            get { return mHostedService; }
        }

        /// <summary>
        /// Method is called by Windows when the service receives the 'Start' command.
        /// </summary>
        /// <param name="args">Command line arguments provided to the service from Windows service configuration.</param>
        protected override void OnStart(string[] args)
        {
            // Hook up the global exception handler
            AppDomain.CurrentDomain.UnhandledException += threadExceptionHandler;
            mHandlerAssigned = true;
 
            // Start the service host
            FlexibleServiceHost.StartServiceHost(this, args);
        }

        /// <summary>
        /// Method is called by Windows when the service receives the 'Stop' command.
        /// </summary>
        protected override void OnStop()
        {
            // Stop the service host
            FlexibleServiceHost.StopServiceHost(this);

            // Check to see if the global error handler needs to be removed.
            if (mHandlerAssigned)
            {
                AppDomain.CurrentDomain.UnhandledException -= threadExceptionHandler;
                mHandlerAssigned = false;
            }
        }

        #endregion

    }
}
