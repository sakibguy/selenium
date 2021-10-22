// <copyright file="ChromiumDriver.cs" company="WebDriver Committers">
// Licensed to the Software Freedom Conservancy (SFC) under one
// or more contributor license agreements. See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership. The SFC licenses this file
// to you under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.DevTools;
using OpenQA.Selenium.Remote;

namespace OpenQA.Selenium.Chromium
{
    /// <summary>
    /// Provides an abstract way to access Chromium-based browsers to run tests.
    /// </summary>
    public abstract class ChromiumDriver : WebDriver, ISupportsLogs, IDevTools
    {
        /// <summary>
        /// Accept untrusted SSL Certificates
        /// </summary>
        public static readonly bool AcceptUntrustedCertificates = true;

        protected const string ExecuteCdp = "executeCdpCommand";
        protected const string GetCastSinksCommand = "getCastSinks";
        protected const string SelectCastSinkCommand = "selectCastSink";
        protected const string StartCastTabMirroringCommand = "startCastTabMirroring";
        protected const string GetCastIssueMessageCommand = "getCastIssueMessage";
        protected const string StopCastingCommand = "stopCasting";
        private const string GetNetworkConditionsCommand = "getNetworkConditions";
        private const string SetNetworkConditionsCommand = "setNetworkConditions";
        private const string DeleteNetworkConditionsCommand = "deleteNetworkConditions";
        private const string SendChromeCommand = "sendChromeCommand";
        private const string SendChromeCommandWithResult = "sendChromeCommandWithResult";
        private const string LaunchAppCommand = "launchAppCommand";
        private const string SetPermissionCommand = "setPermission";

        private readonly string optionsCapabilityName;
        private DevToolsSession devToolsSession;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromiumDriver"/> class using the specified
        /// <see cref="ChromiumDriverService"/> and options.
        /// </summary>
        /// <param name="service">The <see cref="ChromiumDriverService"/> to use.</param>
        /// <param name="options">The <see cref="ChromiumOptions"/> used to initialize the driver.</param>
        public ChromiumDriver(ChromiumDriverService service, ChromiumOptions options)
            : this(service, options, RemoteWebDriver.DefaultCommandTimeout)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromiumDriver"/> class using the specified <see cref="ChromiumDriverService"/>.
        /// </summary>
        /// <param name="service">The <see cref="ChromiumDriverService"/> to use.</param>
        /// <param name="options">The <see cref="ChromiumOptions"/> to be used with the ChromiumDriver.</param>
        /// <param name="commandTimeout">The maximum amount of time to wait for each command.</param>
        public ChromiumDriver(ChromiumDriverService service, ChromiumOptions options, TimeSpan commandTimeout)
            : base(new DriverServiceCommandExecutor(service, commandTimeout), ConvertOptionsToCapabilities(options))
        {
            this.optionsCapabilityName = options.CapabilityName;

            // Add the custom commands unique to Chrome
            this.AddCustomChromeCommand(GetNetworkConditionsCommand, HttpCommandInfo.GetCommand, "/session/{sessionId}/chromium/network_conditions");
            this.AddCustomChromeCommand(SetNetworkConditionsCommand, HttpCommandInfo.PostCommand, "/session/{sessionId}/chromium/network_conditions");
            this.AddCustomChromeCommand(DeleteNetworkConditionsCommand, HttpCommandInfo.DeleteCommand, "/session/{sessionId}/chromium/network_conditions");
            this.AddCustomChromeCommand(SendChromeCommand, HttpCommandInfo.PostCommand, "/session/{sessionId}/chromium/send_command");
            this.AddCustomChromeCommand(SendChromeCommandWithResult, HttpCommandInfo.PostCommand, "/session/{sessionId}/chromium/send_command_and_get_result");
            this.AddCustomChromeCommand(LaunchAppCommand, HttpCommandInfo.PostCommand, "/session/{sessionId}/chromium/launch_app");
            this.AddCustomChromeCommand(SetPermissionCommand, HttpCommandInfo.PostCommand, "/session/{sessionId}/permissions");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChromiumDriver"/> class
        /// </summary>
        /// <param name="commandExecutor">An <see cref="ICommandExecutor"/> object which executes commands for the driver.</param>
        /// <param name="desiredCapabilities">An <see cref="ICapabilities"/> object containing the desired capabilities of the browser.</param>
        protected ChromiumDriver(ICommandExecutor commandExecutor, ICapabilities desiredCapabilities)
            : base(commandExecutor, desiredCapabilities)
        {
        }

        /// <summary>
        /// Gets or sets the <see cref="IFileDetector"/> responsible for detecting
        /// sequences of keystrokes representing file paths and names.
        /// </summary>
        /// <remarks>The Chromium driver does not allow a file detector to be set,
        /// as the server component of the Chromium driver only
        /// allows uploads from the local computer environment. Attempting to set
        /// this property has no effect, but does not throw an exception. If you
        /// are attempting to run the Chromium driver remotely, use <see cref="RemoteWebDriver"/>
        /// in conjunction with a standalone WebDriver server.</remarks>
        public override IFileDetector FileDetector
        {
            get { return base.FileDetector; }
            set { }
        }

        /// <summary>
        /// Gets a value indicating whether a DevTools session is active.
        /// </summary>
        public bool HasActiveDevToolsSession
        {
            get { return this.devToolsSession != null; }
        }

        /// <summary>
        /// Gets or sets the network condition emulation for Chromium.
        /// </summary>
        public ChromiumNetworkConditions NetworkConditions
        {
            get
            {
                Response response = this.Execute(GetNetworkConditionsCommand, null);
                return ChromiumNetworkConditions.FromDictionary(response.Value as Dictionary<string, object>);
            }

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException("value", "value must not be null");
                }

                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters["network_conditions"] = value.ToDictionary();
                this.Execute(SetNetworkConditionsCommand, parameters);
            }
        }

        /// <summary>
        /// Launches a Chromium based application.
        /// </summary>
        /// <param name="id">ID of the chromium app to launch.</param>
        public void LaunchApp(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id", "id must not be null");
            }

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["id"] = id;
            this.Execute(LaunchAppCommand, parameters);
        }

        /// <summary>
        /// Set supported permission on browser.
        /// </summary>
        /// <param name="permissionName">Name of item to set the permission on.</param>
        /// <param name="permissionValue">Value to set the permission to.</param>
        public void SetPermission(string permissionName, string permissionValue)
        {
            if (permissionName == null)
            {
                throw new ArgumentNullException("permissionName", "name must not be null");
            }

            if (permissionValue == null)
            {
                throw new ArgumentNullException("permissionValue", "value must not be null");
            }

            Dictionary<string, object> nameParameter = new Dictionary<string, object>();
            nameParameter["name"] = permissionName;
            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["descriptor"] = nameParameter;
            parameters["state"] = permissionValue;
            this.Execute(SetPermissionCommand, parameters);
        }

        /// <summary>
        /// Executes a custom Chrome Dev Tools Protocol Command.
        /// </summary>
        /// <param name="commandName">Name of the command to execute.</param>
        /// <param name="commandParameters">Parameters of the command to execute.</param>
        /// <returns>An object representing the result of the command, if applicable.</returns>
        public object ExecuteCdpCommand(string commandName, Dictionary<string, object> commandParameters)
        {
            if (commandName == null)
            {
                throw new ArgumentNullException("commandName", "commandName must not be null");
            }

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["cmd"] = commandName;
            parameters["params"] = commandParameters;
            Response response = this.Execute(ExecuteCdp, parameters);
            return response.Value;
        }

        /// <summary>
        /// Executes a custom Chrome command.
        /// </summary>
        /// <param name="commandName">Name of the command to execute.</param>
        /// <param name="commandParameters">Parameters of the command to execute.</param>
        [Obsolete("ExecuteChromeCommand is deprecated in favor of ExecuteCdpCommand.")]
        public void ExecuteChromeCommand(string commandName, Dictionary<string, object> commandParameters)
        {
            ExecuteCdpCommand(commandName, commandParameters);
        }

        /// <summary>
        /// Executes a custom Chrome command.
        /// </summary>
        /// <param name="commandName">Name of the command to execute.</param>
        /// <param name="commandParameters">Parameters of the command to execute.</param>
        /// <returns>An object representing the result of the command.</returns>
        [Obsolete("ExecuteChromeCommandWithResult is deprecated in favor of ExecuteCdpCommand.")]
        public object ExecuteChromeCommandWithResult(string commandName, Dictionary<string, object> commandParameters)
        {
            return ExecuteCdpCommand(commandName, commandParameters);
        }

        /// <summary>
        /// Creates a session to communicate with a browser using the Chromium Developer Tools debugging protocol.
        /// </summary>
        /// <param name="devToolsProtocolVersion">The version of the Chromium Developer Tools protocol to use. Defaults to autodetect the protocol version.</param>
        /// <returns>The active session to use to communicate with the Chromium Developer Tools debugging protocol.</returns>
        public DevToolsSession GetDevToolsSession()
        {
            return GetDevToolsSession(DevToolsSession.AutoDetectDevToolsProtocolVersion);
        }

        /// <summary>
        /// Creates a session to communicate with a browser using the Chromium Developer Tools debugging protocol.
        /// </summary>
        /// <param name="devToolsProtocolVersion">The version of the Chromium Developer Tools protocol to use. Defaults to autodetect the protocol version.</param>
        /// <returns>The active session to use to communicate with the Chromium Developer Tools debugging protocol.</returns>
        public DevToolsSession GetDevToolsSession(int devToolsProtocolVersion)
        {
            if (this.devToolsSession == null)
            {
                if (!this.Capabilities.HasCapability(this.optionsCapabilityName))
                {
                    throw new WebDriverException("Cannot find " + this.optionsCapabilityName + " capability for driver");
                }

                Dictionary<string, object> options = this.Capabilities.GetCapability(this.optionsCapabilityName) as Dictionary<string, object>;
                if (options == null)
                {
                    throw new WebDriverException("Found " + this.optionsCapabilityName + " capability, but is not an object");
                }

                if (!options.ContainsKey("debuggerAddress"))
                {
                    throw new WebDriverException("Did not find debuggerAddress capability in " + this.optionsCapabilityName);
                }

                string debuggerAddress = options["debuggerAddress"].ToString();
                try
                {
                    DevToolsSession session = new DevToolsSession(debuggerAddress);
                    session.StartSession(devToolsProtocolVersion).ConfigureAwait(false).GetAwaiter().GetResult();
                    this.devToolsSession = session;
                }
                catch (Exception e)
                {
                    throw new WebDriverException("Unexpected error creating WebSocket DevTools session.", e);
                }
            }

            return this.devToolsSession;
        }

        /// <summary>
        /// Closes a DevTools session.
        /// </summary>
        public void CloseDevToolsSession()
        {
            if (this.devToolsSession != null)
            {
                this.devToolsSession.StopSession(true).ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Returns the list of cast sinks (Cast devices) available to the Chrome media router.
        /// </summary>
        /// <returns>The list of available sinks.</returns>
        public List<Dictionary<string, string>> GetCastSinks()
        {
            List<Dictionary<string, string>> returnValue = new List<Dictionary<string, string>>();
            Response response = this.Execute(GetCastSinksCommand, null);
            object[] responseValue = response.Value as object[];
            if (responseValue != null)
            {
                foreach (object entry in responseValue)
                {
                    Dictionary<string, object> entryValue = entry as Dictionary<string, object>;
                    if (entryValue != null)
                    {
                        Dictionary<string, string> sink = new Dictionary<string, string>();
                        foreach (KeyValuePair<string, object> pair in entryValue)
                        {
                            sink[pair.Key] = pair.Value.ToString();
                        }

                        returnValue.Add(sink);
                    }
                }
            }
            return returnValue;
        }

        /// <summary>
        /// Selects a cast sink (Cast device) as the recipient of media router intents (connect or play).
        /// </summary>
        /// <param name="deviceName">Name of the target sink (device).</param>
        public void SelectCastSink(string deviceName)
        {
            if (deviceName == null)
            {
                throw new ArgumentNullException("deviceName", "deviceName must not be null");
            }

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["sinkName"] = deviceName;
            this.Execute(SelectCastSinkCommand, parameters);
        }

        /// <summary>
        /// Initiates tab mirroring for the current browser tab on the specified device.
        /// </summary>
        /// <param name="deviceName">Name of the target sink (device).</param>
        public void StartTabMirroring(string deviceName)
        {
            if (deviceName == null)
            {
                throw new ArgumentNullException("deviceName", "deviceName must not be null");
            }

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["sinkName"] = deviceName;
            this.Execute(StartCastTabMirroringCommand, parameters);
        }

        /// <summary>
        /// Returns the error message if there is any issue in a Cast session.
        /// </summary>
        /// <returns>An error message.</returns>
        public String GetCastIssueMessage()
        {
            Response response = this.Execute(GetCastIssueMessageCommand, null);
            return (string)response.Value;
        }

        /// <summary>
        /// Stops casting from media router to the specified device, if connected.
        /// </summary>
        /// <param name="deviceName">Name of the target sink (device).</param>
        public void StopCasting(string deviceName)
        {
            if (deviceName == null)
            {
                throw new ArgumentNullException("deviceName", "deviceName must not be null");
            }

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            parameters["sinkName"] = deviceName;
            this.Execute(StopCastingCommand, parameters);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.devToolsSession != null)
                {
                    this.devToolsSession.Dispose();
                    this.devToolsSession = null;
                }
            }

            base.Dispose(disposing);
        }

        private static ICapabilities ConvertOptionsToCapabilities(ChromiumOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options", "options must not be null");
            }

            return options.ToCapabilities();
        }

        protected void AddCustomChromeCommand(string commandName, string method, string resourcePath)
        {
            HttpCommandInfo commandInfoToAdd = new HttpCommandInfo(method, resourcePath);
            this.CommandExecutor.TryAddCommand(commandName, commandInfoToAdd);
        }
    }
}
