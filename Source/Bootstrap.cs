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
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using System.Reflection;

using NLog;
using NLog.Targets;
using NLog.Config;

namespace ServiceHosting
{
    /// <summary>
    /// The primary class used to host a service with the Flexible Service Host utility.
    /// </summary>
    public sealed class Bootstrap 
    {
        private const string ConsoleExecutionArgument = @"Console";
        private const string InstallExecutionArgument = @"Install";
        private const string UninstallExecutionArgument = @"Uninstall";
        private const string ShowConsoleLogArgument = @"ShowLog";
        private const string InstallUsernameArgument = @"Username";
        private const string InstallPasswordArgument = @"Password";

        private const string HostedServiceDisplayTemplate = "{0} Service";
        private const string HostedServiceDescriptionTemplate ="Flexible Service Hosted Service for {0}";

        private static readonly Type sFlexibleServiceHostType = typeof(FlexibleServiceHost);
        private static readonly System.Globalization.CultureInfo sCulture = System.Globalization.CultureInfo.CurrentCulture;

        private Type mServiceType;
        private string mServiceName;        
        private string mDisplayName;
        private string mDescription;
        
        private ServiceController mController = null;

        /// <summary>
        /// The main entry point for the service host.
        /// </summary>
        public Bootstrap(Type serviceType, string serviceName) : 
            this(serviceType, serviceName, String.Format(sCulture, HostedServiceDisplayTemplate, serviceName), 
                String.Format(sCulture, HostedServiceDescriptionTemplate, serviceName)) { }

        /// <summary>
        /// The main entry point for the service host.
        /// </summary>
        public Bootstrap(Type serviceType, string serviceName, string displayName, string description)
        {
            // Check the arguments
            if (String.IsNullOrEmpty(serviceName))
                throw new ArgumentException("Service name must be specified and cannot be null or empty.", "serviceName");

            if (serviceType.IsAbstract || serviceType.IsInterface)
                throw new ArgumentException("The hosted service class to host must: Implement the " +
                    "IHostableService interface, not be abstract, and not be an interface.", "serviceType");            

            // Check for display name and description
            if (!String.IsNullOrEmpty(displayName))
                mDisplayName = displayName;
            else
                mDisplayName = String.Format(sCulture, mDisplayName, serviceName);

            if (!String.IsNullOrEmpty(description))
                mDescription = description;
            else
                mDescription = String.Format(sCulture, mDescription, serviceName);

            // Set the service name            
            mServiceName = serviceName;

            // Set the service type
            mServiceType = serviceType;

            // Get a service controller for the service
            mController = GetServiceController(mServiceName);
        }

        /// <summary>
        /// Runs the service host once initialized
        /// </summary>
        /// <param name="args">The command line arguments, if any, to pass to the hosted service.</param>
        public void Execute(string[] args)
        {
            // See if the "install service" flag was used
            if (FindArgument(InstallExecutionArgument, args))
            {
                // Try to find the username and password from the commandline
                string username = ExtractArgument(InstallUsernameArgument, args);
                string password = ExtractArgument(InstallPasswordArgument, args);

                bool fastTrack = false;

                // If both username and password are set, we do not require any input from the user
                if (!String.IsNullOrEmpty(username) && !String.IsNullOrEmpty(password))
                {
                    fastTrack = true;
                }

                // If the controller is null, there is no service installed
                if (mController == null)
                {
                    Console.WriteLine("Running as user: {0}\\{1}", Environment.UserDomainName, Environment.UserName);                    

                    if (!fastTrack) 
                    {
                        Console.WriteLine("Press any key to install the {0} service... (or escape to exit)", mServiceName);
                        var keyPress = Console.ReadKey(true);

                        // Check for escape
                        if (keyPress.Key == ConsoleKey.Escape)
                        {
                            PressAnyKeyToExit(fastTrack);
                        }

                        // Get the username from the console
                        Console.Write("RunAs Username [Enter for Local System]: ");
                        username = Console.ReadLine();

                        // Get the password also
                        if (!String.IsNullOrEmpty(username))
                        {
                            Console.Write("Password: ");
                            password = ReadPassword();
                            Console.Write("\r\n");
                        }
                    }

                    if (String.IsNullOrEmpty(password) && !String.IsNullOrEmpty(username))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("You must enter a password when setting the \"RunAs\" Username!");
                        Console.ResetColor();
                        PressAnyKeyToExit(fastTrack);
                    }

                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("Installing {0} Service...", mServiceName);

                        if (!String.IsNullOrEmpty(username))
                            FlexibleServiceHost.InstallWindowsServiceHost(mServiceName, mDisplayName,
                                mDescription, username, password);
                        else
                            FlexibleServiceHost.InstallWindowsServiceHost(mServiceName, mDisplayName,
                                mDescription);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Service Installed.");
                        Console.ResetColor();
                        PressAnyKeyToExit(fastTrack);
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\r\nEncountered an error while trying to install service. Error: {0}.",
                            ex.Message);
                        Console.ResetColor();
                        PressAnyKeyToExit(fastTrack, true);
                    }
                }
                // Otherwise, the service is already installed
                else
                {
                    Console.WriteLine("The service {0} is already installed on this system!", mServiceName);
                    PressAnyKeyToExit(fastTrack);
                }
            }
            // See if the "uninstall service" flag was used
            else if (FindArgument(UninstallExecutionArgument, args))
            {
                // See if the service is installed
                if (mController != null)
                {
                    // Refresh the controller status
                    mController.Refresh();

                    // Make sure the service is stopped
                    if (mController.Status != ServiceControllerStatus.Stopped)
                    {
                        bool goodInput = false;

                        // Wait for the user to provide valid input
                        while (!goodInput)
                        {
                            Console.WriteLine("The service {0} is currently running. Do you want to stop it (Y/N)?");

                            var keyPress = Console.ReadKey(true);                            

                            // Check for escape
                            if (keyPress.Key == ConsoleKey.Escape)
                            {
                                Environment.Exit(0);
                            }
                            else if (keyPress.KeyChar.ToString() == "n")
                            {
                                Console.WriteLine("The service must be stopped to uninstall it. Please stop the service " +
                                    "before attempting uninstallation.");
                                PressAnyKeyToExit();
                            }
                            else if (keyPress.KeyChar.ToString() == "y")
                            {
                                goodInput = true;
                            }
                            else
                            {
                                Console.WriteLine("\tUnrecognized input. Please press either \"y\", \"n\", or Escape.");
                            }
                        }

                        // Stop the service
                        try
                        {
                            mController.Stop();
                            mController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(1));
                            Console.WriteLine("Service stopped successfully.");
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("\r\nEncountered an error while trying to stop the service. Error: {0}.",
                                ex.Message);
                            Console.ResetColor();

                            PressAnyKeyToExit();
                        }
                    }

                    Console.WriteLine("Press any key to uninstall the {0} service... (or escape to exit)", mServiceName);
                    var escKeyPress = Console.ReadKey(true);                    

                    // Check for escape
                    if (escKeyPress.Key == ConsoleKey.Escape)
                    {
                        PressAnyKeyToExit();
                    }

                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("Uninstalling {0} Service...", mServiceName);

                        FlexibleServiceHost.UninstallWindowsServiceHost(mServiceName);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Service Uninstalled.");
                        Console.ResetColor();
                        PressAnyKeyToExit();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\r\nEncountered an error while trying to uninstall service. Error: {0}.",
                            ex.Message);
                        Console.ResetColor();
                        PressAnyKeyToExit();
                    }
                }
                // The service is not installed
                else
                {
                    Console.WriteLine("The service {0} is not installed on this system.", mServiceName);
                    PressAnyKeyToExit();
                }
            }
            // Run the hosted service
            else
            {
                // Used to keep track of the hosting type
                ServiceHostType hostingType = ServiceHostType.Console;

                // Check for service mode
                if (mController != null)
                {
                    mController.Refresh();
                    mController.ServiceName = mServiceName;

                    if (mController.Status == ServiceControllerStatus.StartPending)
                    {
                        hostingType = ServiceHostType.WindowsService;
                    }
                    else if (mController.Status == ServiceControllerStatus.Running)
                    {
                        Console.WriteLine("The {0} Service must be stopped to run this application in console mode.",
                            mServiceName);

                        PressAnyKeyToExit();
                    }
                }

                // Create a new logging configuration
                LoggingConfiguration newConfig = new LoggingConfiguration();

                // Check for any Nlog.config configured loggers
                if (LogManager.Configuration != null)
                {
                    // Add any existing targets
                    foreach (var target in LogManager.Configuration.AllTargets)
                        newConfig.AddTarget(target.Name, target);

                    // ...and their associated rules.
                    foreach (var rule in LogManager.Configuration.LoggingRules)
                        newConfig.LoggingRules.Add(rule);
                }

                // Setup the file log
                ConfigureFlexibleServiceHostLog(newConfig);

                switch (hostingType)
                {
                    case ServiceHostType.Console:
                        {
                            // Don't break on CTRL-C!
                            Console.TreatControlCAsInput = true;

                            // Set the console title to the application display name
                            Console.Title = mDisplayName;

                            // Check for the ShowConsole argument
                            if (FindArgument(ShowConsoleLogArgument, args))
                            {
                                ConfigureConsoleLog(newConfig, mServiceName);
                            }

                            // Set the logging configuration
                            LogManager.Configuration = newConfig;
                            
                            // Start the console host
                            try
                            {
                                InitializeService(mServiceType, mServiceName, ServiceHostType.Console);

                                FlexibleServiceHost.BeginHosting();

                                Console.ReadKey(true);

                                FlexibleServiceHost.EndHosting();
                            }
                            catch (Exception ex)
                            {

                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("\r\nHosted service {0} threw an exception. Message: {1}.",
                                    mServiceName, ex.Message);
                                Console.ResetColor();

                                try
                                {
                                    FlexibleServiceHost.EndHosting();
                                }
                                catch
                                {
                                }
                            }
                            Console.WriteLine("\r\nSYSTEM HALTED - {0}", DateTime.Now.ToString());
                            PressAnyKeyToExit();
                        };
                        break;

                    case ServiceHostType.WindowsService:
                        {
                            // Set the logging configuration
                            LogManager.Configuration = newConfig;

                            // Initialize the hosted service
                            InitializeService(mServiceType, mServiceName, ServiceHostType.WindowsService);

                            // Begin hosting it
                            FlexibleServiceHost.BeginHosting();
                        }
                        break;
                }
            }            
        }

        /// <summary>
        /// Quick and simple service host.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="displayName">The display name of the service (more descriptive).</param>
        /// <param name="description">A full description of the service.</param>
        /// <param name="args">Any command like arguments to pass to the hosted class.</param>
        /// <typeparam name="T">
        /// The class type, which must inherit from the <see cref="ServiceHosting.IHostedService">IHostableService</see> interface, which will
        /// be hosted by the service host.
        /// </typeparam>
        [MTAThread]
        public static void Jumpstart<T>(string serviceName, string displayName, string description,
            params string[] args) where T : IHostedService
        {
            Bootstrap strapper = new Bootstrap(typeof(T), serviceName, displayName, description);
            strapper.Execute(args);
        }

        /// <summary>
        /// Prompts the user to press any key, then closes the application.
        /// </summary>
        private static void PressAnyKeyToExit(bool skipKeypress = false, bool exitWithError = false)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("\r\nPress any key to close console.");
            Console.ResetColor();

            if (!skipKeypress)
                Console.ReadKey(true);
            else
                System.Threading.Thread.Sleep(1000);

            if (!exitWithError)
                Environment.Exit(0);
            else
                Environment.Exit(-1);
        }

        /// <summary>
        /// Finds an argument in the command line.
        /// </summary>
        /// <param name="findArg">The string to look for (case insensitive).</param>
        /// <param name="args">The command line parameter list.</param>
        /// <param name="requireDash">Specifies whether command line arguments to the service host require a leading dash ("-") before the operation name.</param>
        /// <returns>True if the argument is found, false otherwise.</returns>
        private static bool FindArgument(string findArg, string[] args, bool requireDash = false)
        {
            if (requireDash)
            {
                findArg = @"-" + findArg;
            }

            return (null != Array.Find<string>(args, 
                        delegate(string arg)
                        {
                            if (!requireDash)
                            {
                                if (arg.StartsWith(@"-", StringComparison.OrdinalIgnoreCase))
                                    arg = arg.Remove(0, 1);
                            }
                            return (String.Compare(arg.Trim(), findArg, StringComparison.CurrentCultureIgnoreCase) == 0);
                        })
                    );
        }

        /// <summary>
        /// Extracts an argument's value from the command line.
        /// </summary>
        /// <param name="findArg">The suffix (precursor) name of the argument.</param>
        /// <param name="args">The arguments list to search.</param>
        /// <param name="delim">After the suffix string, the value will be split by this character (defaults to ':').</param>
        /// <param name="requiresDash">If true, each argument requires a leading dash.</param>
        /// <returns>The value in the command line argument following the delimiter.</returns>
        private static string ExtractArgument(string findArg, string[] args, char delim = ':', bool requiresDash = false)
        {
            // Append the dash if required
            if (requiresDash) { findArg = "-" + findArg; }

            // Go through each argument and look for the precursor
            foreach (string arg in args)
            {
                string temp = arg;

                // Remove dash if not required
                if (!requiresDash && temp.StartsWith("-", StringComparison.OrdinalIgnoreCase))
                    temp = temp.Remove(0, 1);

                // Check match
                if (temp.StartsWith(findArg, StringComparison.CurrentCultureIgnoreCase))
                {
                    // Split by the delimiter
                    string[] argValue = temp.Split(delim);

                    // If we have a value
                    if (argValue.Length == 2)
                        return argValue[1];
                    else if (argValue.Length > 2)
                    {
                        string[] results = new string[argValue.Length - 1];
                        Array.Copy(argValue, 1, results, 0, argValue.Length - 1);
                        return String.Join(delim.ToString(), results);
                    }
                    else
                        return String.Empty;
                }
            }

            // Argument was not found
            return null;
        }

        /// <summary>
        /// Gets a service controller for the named service.
        /// </summary>
        /// <param name="serviceName">The name of the service to look for.</param>
        /// <returns>A service controller if the service is found, null otherwise.</returns>
        private static ServiceController GetServiceController(string serviceName)
        {
            ServiceController foundController = null;
            ServiceController[] services = ServiceController.GetServices();
            foundController = Array.Find<ServiceController>(services, 
                delegate(ServiceController sc)
                {
                    return (sc.ServiceName.ToUpperInvariant() == serviceName.ToUpperInvariant());
                });

            return (foundController);
        }

        /// <summary>
        /// Creates the header string for the console.
        /// </summary>
        private static string WriteLogStartupHeader(string serviceName, string mode)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("================================================================================");
            sb.AppendLine(String.Format(sCulture, "Starting {0} on {1}", serviceName.ToUpper(sCulture), DateTime.Now.ToString()));
            sb.AppendLine(String.Format(sCulture, "Running as user: {0}\\{1}", Environment.UserDomainName, Environment.UserName));
            sb.AppendLine(String.Format(sCulture, "Running {0}.", mode));
            sb.AppendLine(String.Format(sCulture, "Press any key to begin the shutdown process..."));
            sb.AppendLine("================================================================================");

            return sb.ToString();
        }

        /// <summary>
        /// Allows for reading a password from the console writing out "*" characters instead of the password text.
        /// </summary>
        /// <returns>The password text typed into the console after the enter key is pressed.</returns>
        private static string ReadPassword()
        {
            Stack<string> pass = new Stack<string>();

            for (ConsoleKeyInfo consKeyInfo = Console.ReadKey(true);
              consKeyInfo.Key != ConsoleKey.Enter; consKeyInfo = Console.ReadKey(true))
            {
                if (consKeyInfo.Key == ConsoleKey.Backspace)
                {
                    try
                    {
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                        Console.Write(" ");
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                        pass.Pop();
                    }
                    catch (InvalidOperationException)
                    {
                        /* Nothing to delete, go back to previous position */
                        Console.SetCursorPosition(Console.CursorLeft + 1, Console.CursorTop);
                    }
                }
                else
                {
                    Console.Write("*");
                    pass.Push(consKeyInfo.KeyChar.ToString());
                }
            }
            String[] password = pass.ToArray();
            Array.Reverse(password);
            return string.Join(string.Empty, password);
        }

        private static void InitializeService(Type serviceType, string serviceName, ServiceHostType hostType)
        {
            MethodInfo initializer = sFlexibleServiceHostType.GetMethod("InitializeHostedService", new Type[] { typeof(String), typeof(ServiceHostType) })
                .MakeGenericMethod(serviceType);

            initializer.Invoke(null, new object[] { serviceName, hostType });
        }

        private static void ConfigureFlexibleServiceHostLog(LoggingConfiguration config)
        {
            // Create a new file logger
            FileTarget fileTarget = new FileTarget();                     

            // Setup the log format output
            fileTarget.Layout = @"${longdate} | ${message} ${onexception:${newline}   Exception\: ${exception:format=type,method,message,stacktrace" +
              ":maxInnerExceptionLevel=3:innerFormat=shortType,method,message,stacktrace}}";
            
            // Setup the header and footer
            fileTarget.Header = "------------------ Begin Flexible Service Host Log - ${longdate} ----------------";
            fileTarget.Footer = "------------------ End Flexible Service Host Log - ${longdate} ------------------${newline}";                       

            // Setup the file path
            string runningPath = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Set the filename
            fileTarget.FileName = runningPath + @"\FlexibleServiceHost.log";

            // Create a logging rule for the classes we care about
            LoggingRule consoleRule = new LoggingRule("ServiceHosting.*", LogLevel.Info, fileTarget);

            config.AddTarget("FlexibleServiceHostFileTarget", fileTarget);
            config.LoggingRules.Add(consoleRule);
        }

        private static void ConfigureConsoleLog(LoggingConfiguration config, string serviceName)
        {
            ColoredConsoleTarget consoleTarget = new ColoredConsoleTarget();
            consoleTarget.Layout = @"${date:format=HH\:mm\:ss} | ${pad:padding=15:fixedLenght=true:inner=${logger:shortName=true}} | ${message}";
            consoleTarget.Header = WriteLogStartupHeader(serviceName, "in console mode.");
            LoggingRule consoleRule = new LoggingRule("*", LogLevel.Info, consoleTarget);

            config.AddTarget("console", consoleTarget);
            config.LoggingRules.Add(consoleRule);
        }
    }
}

