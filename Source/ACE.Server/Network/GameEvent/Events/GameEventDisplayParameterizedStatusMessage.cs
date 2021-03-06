﻿using ACE.Entity.Enum;

namespace ACE.Server.Network.GameEvent.Events
{
    public class GameEventDisplayParameterizedStatusMessage : GameEventMessage
    {
        public GameEventDisplayParameterizedStatusMessage(Session session, StatusMessageType2 statusMessageType2, string message)
            : base(GameEventType.DisplayParameterizedStatusMessage, GameMessageGroup.UIQueue, session)
        {
            Writer.Write((uint)statusMessageType2);
            Writer.WriteString16L(message);
        }
    }
}
