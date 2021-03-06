﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using Torch;
using Sandbox;
using Sandbox.Engine.Analytics;
using Sandbox.Engine.Multiplayer;
using Sandbox.Engine.Utils;
using Sandbox.Game;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using SpaceEngineers.Game;
using Torch.API;
using VRage.Dedicated;
using VRage.Game;
using VRage.Game.SessionComponents;
using VRage.Profiler;

namespace Torch.Server
{
    public class TorchServer : TorchBase, ITorchServer
    {
        public Thread GameThread { get; private set; }
        public bool IsRunning { get; private set; }
        public string InstancePath { get; }
        public string InstanceName { get; }

        private readonly AutoResetEvent _stopHandle = new AutoResetEvent(false);

        internal TorchServer(ServerConfig options)
        {
            InstanceName = options.InstanceName;
            InstancePath = options.InstancePath;
        }

        public override void Init()
        {
            base.Init();

            Log.Info($"Server instance {InstanceName} at path {InstancePath}");

            MyFakes.ENABLE_INFINARIO = false;
            MyPerGameSettings.SendLogToKeen = false;
            MyPerServerSettings.GameName = MyPerGameSettings.GameName;
            MyPerServerSettings.GameNameSafe = MyPerGameSettings.GameNameSafe;
            MyPerServerSettings.GameDSName = MyPerServerSettings.GameNameSafe + "Dedicated";
            MyPerServerSettings.GameDSDescription = "Your place for space engineering, destruction and exploring.";
            MySessionComponentExtDebug.ForceDisable = true;
            MyPerServerSettings.AppId = 244850u;
            ConfigForm<MyObjectBuilder_SessionSettings>.GameAttributes = Game.SpaceEngineers;
            ConfigForm<MyObjectBuilder_SessionSettings>.OnReset = delegate
            {
                SpaceEngineersGame.SetupBasicGameInfo();
                SpaceEngineersGame.SetupPerGameSettings();
            };
            var gameVersion = MyPerGameSettings.BasicGameInfo.GameVersion;
            MyFinalBuildConstants.APP_VERSION = gameVersion ?? 0;

            //InstancePath = InstanceName != null ? GetInstancePath(true, InstanceName) : GetInstancePath();
        }

        /// <summary>
        /// Start server on the current thread.
        /// </summary>
        public override void Start()
        {
            if (IsRunning)
                throw new InvalidOperationException("Server is already running.");

            IsRunning = true;
            Log.Info("Starting server.");

            MySandboxGame.IsDedicated = true;
            Environment.SetEnvironmentVariable("SteamAppId", MyPerServerSettings.AppId.ToString());

            Log.Trace("Invoking RunMain");
            try { Reflection.InvokeStaticMethod(typeof(DedicatedServer), "RunMain", InstanceName, InstancePath, false, true); }
            catch (Exception e)
            {
                Log.Error("Error running server.");
                Log.Error(e);
                throw;
            }
            Log.Trace("RunMain completed");
            IsRunning = false;
        }

        /// <summary>
        /// Stop the server.
        /// </summary>
        public override void Stop()
        {
            if (!IsRunning)
                Log.Error("Server is already stopped");

            if (Thread.CurrentThread.ManagedThreadId != GameThread?.ManagedThreadId && MySandboxGame.Static.IsRunning)
            {
                Log.Info("Requesting server stop.");
                MySandboxGame.Static.Invoke(Stop);
                _stopHandle.WaitOne(10000);
                Log.Error("Server stop timed out.");
                return;
            }

            Log.Info("Stopping server.");
            MySession.Static.Save();
            MySession.Static.Unload();
            MySandboxGame.Static.Exit();

            //Unload all the static junk.
            //TODO: Finish unloading all server data so it's in a completely clean state.
            VRage.FileSystem.MyFileSystem.Reset();
            VRage.Input.MyGuiGameControlsHelpers.Reset();
            VRage.Input.MyInput.UnloadData();
            //CleanupProfilers();

            Log.Info("Server stopped.");
            _stopHandle.Set();
            IsRunning = false;
        }

        /*
        private string GetInstancePath(bool isService = false, string instanceName = "Torch")
        {
            if (isService)
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), MyPerServerSettings.GameDSName, instanceName);

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), MyPerServerSettings.GameDSName);
        }*/

        /*
        private void CleanupProfilers()
        {
            typeof(MyRenderProfiler).GetField("m_threadProfiler", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            typeof(MyRenderProfiler).GetField("m_gpuProfiler", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            (typeof(MyRenderProfiler).GetField("m_threadProfilers", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as List<MyProfiler>).Clear();
        }*/
    }
}
