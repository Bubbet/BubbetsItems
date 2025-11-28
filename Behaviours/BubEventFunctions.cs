using System;
using System.Linq;
using RoR2;
using RoR2.Networking;
using UnityEngine;
using UnityEngine.Networking;

namespace BubbetsItems.Behaviours
{
	public class BubEventFunctions : MonoBehaviour
	{
		public void InteractorOutOfBounds(Interactor interactor)
		{
			var body = interactor.GetComponent<CharacterBody>();
			if (!body) return;
			if (!Util.HasEffectiveAuthority(interactor.gameObject) && NetworkServer.active)
			{
				body.master.playerCharacterMasterController.networkUser.connectionToClient.Send(MsgType, new EventMessage(GetComponent<NetworkIdentity>().netId, EventMessage.MessageType.Oob, body.netId));
				return; 
			}
			var zone = InstanceTracker.GetInstancesList<MapZone>().First();
			zone.TeleportBody(body);
		}

		public static void Init()
		{
			NetworkManagerSystem.onStartClientGlobal += RegisterMessages;
		}

		private const short MsgType = 391; // TODO move to new system and fix this shit
		public static void RegisterMessages(NetworkClient client)
		{
			try
			{
				client.RegisterHandler(MsgType, EventMessage.Handle);
			}
			catch (Exception f)
			{
				BubbetsItemsPlugin.Log.LogError(f);
			}
		}
	}

	public class EventMessage : MessageBase
	{
		private MessageType _type;
		private NetworkInstanceId _objectId;
		private NetworkInstanceId _bodyId;

		public EventMessage(NetworkInstanceId networkInstanceId, MessageType type, NetworkInstanceId bodyId)
		{
			_objectId = networkInstanceId;
			_type = type;
			_bodyId = bodyId;
		}

		public EventMessage() {}

		public enum MessageType
		{
			Oob
		}
		public static void Handle(NetworkMessage netmsg)
		{
			var message = netmsg.ReadMessage<EventMessage>();
			var obj = Util.FindNetworkObject(message._objectId);
			var functions = obj.GetComponent<BubEventFunctions>();
			switch (message._type)
			{
				case MessageType.Oob:
					functions.InteractorOutOfBounds(Util.FindNetworkObject(message._bodyId).GetComponent<Interactor>());
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public override void Deserialize(NetworkReader reader)
		{
			base.Deserialize(reader);
			_objectId = reader.ReadNetworkId();
			_type = (MessageType)reader.ReadInt32();
			_bodyId = reader.ReadNetworkId();
		}

		public override void Serialize(NetworkWriter writer)
		{
			base.Serialize(writer);
			writer.Write(_objectId);
			writer.Write((int)_type);
			writer.Write(_bodyId);
		}
	}
}