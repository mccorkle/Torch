﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox;
using Sandbox.Engine.Networking;
using Sandbox.Engine.Platform;
using SteamSDK;
using Torch.API;
using VRage.Game;

namespace Torch
{
    public static class SteamHelper
    {
        private static CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private static CancellationToken _cancelToken;
        private static Logger _log = LogManager.GetLogger(nameof(SteamHelper));

        public static void Init()
        {
            _cancelToken = _tokenSource.Token;

            Task.Run(() =>
            {
                while (!_cancelToken.IsCancellationRequested)
                {
                    SteamAPI.Instance.RunCallbacks();
                    Thread.Sleep(100);
                }
            });
        }

        public static void StopCallbackLoop()
        {
            _tokenSource.Cancel();
        }

        public static MySteamWorkshop.SubscribedItem GetItemInfo(ulong itemId)
        {
            MySteamWorkshop.SubscribedItem item = null;

            using (var mre = new ManualResetEvent(false))
            {
                SteamAPI.Instance.RemoteStorage.GetPublishedFileDetails(itemId, 0, (ioFail, result) =>
                {
                    if (!ioFail && result.Result == Result.OK)
                    {
                        item = new MySteamWorkshop.SubscribedItem
                        {
                            Title = result.Title,
                            Description = result.Description,
                            PublishedFileId = result.PublishedFileId,
                            SteamIDOwner = result.SteamIDOwner,
                            Tags = result.Tags.Split(' '),
                            TimeUpdated = result.TimeUpdated,
                            UGCHandle = result.FileHandle
                        };
                    }
                    else
                    {
                        _log.Error($"Failed to get item info for {itemId}");
                    }

                    mre.Set();
                });

                mre.WaitOne();
                mre.Reset();

                return item;
            }
        }

        public static SteamUGCDetails GetItemDetails(ulong itemId)
        {
            SteamUGCDetails details = default(SteamUGCDetails);
            using (var re = new AutoResetEvent(false))
            {
                SteamAPI.Instance.UGC.RequestUGCDetails(itemId, 0, (b, result) =>
                {
                    if (!b && result.Details.Result == Result.OK)
                        details = result.Details;
                    else
                        _log.Error($"Failed to get item details for {itemId}");

                    re.Set();
                });

                re.WaitOne();
            }

            return details;
        }

        public static MyObjectBuilder_Checkpoint.ModItem GetModItem(ulong modId)
        {
            var details = GetItemDetails(modId);
            return new MyObjectBuilder_Checkpoint.ModItem(null, modId, details.Title);
        }

        public static MyObjectBuilder_Checkpoint.ModItem GetModItem(SteamUGCDetails details)
        {
            return new MyObjectBuilder_Checkpoint.ModItem(null, details.PublishedFileId, details.Title);
        }
    }
}
