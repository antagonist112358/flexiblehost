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
using System.Runtime.Serialization;

namespace ServiceHosting
{
    /// <summary>
    /// Exception thrown by the FlexibleServiceHost framework.
    /// </summary>
    [Serializable]
    public sealed class ServiceHostException : Exception
    {
        /// <summary>
        /// Default empty constructor
        /// </summary>
        public ServiceHostException() : base() { }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message">The exception explanation message.</param>
        public ServiceHostException(string message) : base(message) { }

        /// <summary>
        /// Constructor - Provided for exception standardization.
        /// </summary>
        /// <param name="message">The exception message text.</param>
        /// <param name="ex">Inner exception this exception wraps</param>
        public ServiceHostException(string message, Exception ex) : base(message, ex) { }

        /// <summary>
        /// Constructor, with a formattable exception message.
        /// </summary>
        /// <param name="message">The formattable exception message.</param>
        /// <param name="formatArguments">A parameterized list of objects to format the <paramref name="message">message</paramref> with.</param>
        public ServiceHostException(string message, params object[] formatArguments) : base(String.Format(System.Globalization.CultureInfo.CurrentCulture, message, formatArguments)) { }

        /// <summary>
        /// Constructor - Provided for serialization support.
        /// </summary>
        /// <param name="info">The serialization info for the exception.</param>
        /// <param name="context">The streaming context for the exception.</param>
        private ServiceHostException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
