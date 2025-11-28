using System;
using System.Collections.Generic;
using System.Reflection;
using HG.Reflection;
using JetBrains.Annotations;
using RoR2.Networking;
using UnityEngine.Networking;

namespace BubbetsItems.Helpers
{
    [MeansImplicitUse]
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ExtraNetworkMessageHandlerAttribute : SearchableAttribute
    {
        public int priority;
        public bool server;
        public bool client;
        private short msgType;
        public static short msgTypeStart = 389;

        public static short? GetMsgType(string methodName)
        {
            if (ClientMessageHandlers.TryGetValue(methodName, out var client))
                return client.msgType;
            if (ServerMessageHandlers.TryGetValue(methodName, out var server))
                return server.msgType;
            return null;
        }

        public static short? GetMsgType(MethodInfo methodInfo) =>
            GetMsgType((methodInfo.DeclaringType?.FullName ?? "") + "." + methodInfo.Name);

        public static short? GetMsgType<T>(string methodName) => GetMsgType(typeof(T).FullName + "." + methodName);

        public static void Initialize()
        {
            ClientMessageHandlers.Clear();
            ServerMessageHandlers.Clear();

            var instances = GetInstances<ExtraNetworkMessageHandlerAttribute>();
            instances.Sort((x, y) =>
            {
                var a = (ExtraNetworkMessageHandlerAttribute)x;
                var b = (ExtraNetworkMessageHandlerAttribute)y;
                return b.priority.CompareTo(a.priority);
            });
            foreach (var searchableAttribute in instances)
            {
                var networkMessageHandlerAttribute = (ExtraNetworkMessageHandlerAttribute)searchableAttribute;
                var methodInfo = networkMessageHandlerAttribute.target as MethodInfo;
                if (!(methodInfo == null) && methodInfo.IsStatic)
                {
                    var key = (methodInfo.DeclaringType?.FullName ?? "") + "." + methodInfo.Name;
                    networkMessageHandlerAttribute.messageHandler =
                        Delegate.CreateDelegate(typeof(NetworkMessageDelegate), methodInfo) as NetworkMessageDelegate;
                    if (networkMessageHandlerAttribute.messageHandler != null)
                    {
                        networkMessageHandlerAttribute.msgType = msgTypeStart++;

                        if (networkMessageHandlerAttribute.client)
                        {
                            ClientMessageHandlers[key] = networkMessageHandlerAttribute;
                        }

                        if (networkMessageHandlerAttribute.server)
                        {
                            ServerMessageHandlers[key] = networkMessageHandlerAttribute;
                        }
                    }

                    if (networkMessageHandlerAttribute.messageHandler == null)
                    {
                        BubbetsItemsPlugin.Log.LogWarning(
                            $"Could not register message handler for {methodInfo.Name}. The function signature is likely incorrect.");
                    }

                    if (networkMessageHandlerAttribute is { client: false, server: false })
                    {
                        BubbetsItemsPlugin.Log.LogWarning(
                            $"Could not register message handler for {methodInfo.Name}. It is marked as neither server nor client.");
                    }
                }
            }

            NetworkManagerSystem.onStartClientGlobal += RegisterClientMessages;
            NetworkManagerSystem.onStartServerGlobal += RegisterServerMessages;
        }

        public static void RegisterServerMessages()
        {
            foreach (var networkMessageHandlerAttribute in ServerMessageHandlers.Values)
            {
                NetworkServer.RegisterHandler(networkMessageHandlerAttribute.msgType,
                    networkMessageHandlerAttribute.messageHandler);
            }
        }

        public static void RegisterClientMessages(NetworkClient client)
        {
            foreach (var networkMessageHandlerAttribute in ClientMessageHandlers.Values)
            {
                client.RegisterHandler(networkMessageHandlerAttribute.msgType,
                    networkMessageHandlerAttribute.messageHandler);
            }
        }

        private NetworkMessageDelegate? messageHandler;
        private static readonly Dictionary<string, ExtraNetworkMessageHandlerAttribute> ClientMessageHandlers = [];
        private static readonly Dictionary<string, ExtraNetworkMessageHandlerAttribute> ServerMessageHandlers = [];
    }
}