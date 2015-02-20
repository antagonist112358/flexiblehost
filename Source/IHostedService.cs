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

namespace ServiceHosting
{
    /// <summary>
    /// The principal interface which is used by the service host.
    /// </summary>
    public interface IHostedService
    {
        /// <summary>
        /// This method is called by the service host on service hosting start. All program initialization and the main windows service
        /// loop should be instantiated during this step.
        /// </summary>
        /// <param name="args">Command line arguments provided to the executable.</param>
        /// <remarks>NOTE: Your start method must return on program start! All Windows Service based logic must run on a separate thread
        /// (e.g. A <see cref="System.Threading.Timer">Timer</see> object used to schedule work to be done on a schedule.</remarks>
        void Start(string[] args);
        
        /// <summary>
        /// This method will be called to perform the actual shutdown of the hosted service.
        /// </summary>
        void Terminate();

        /// <summary>
        /// This method will be called before the stop method, and can be used to prepare your windows service to shutdown.
        /// </summary>
        /// <remarks>
        /// Note: This method will block inside the service host, so be careful when using logic which may cause the host to
        /// wait for long periods of time.
        /// </remarks>
        void BeginShutdown();
    }
}
