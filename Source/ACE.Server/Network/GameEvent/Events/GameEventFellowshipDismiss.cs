using ACE.Server.WorldObjects;
using ACE.Server.Network.GameMessages;

namespace ACE.Server.Network.GameEvent.Events
{
    public class GameEventFellowshipDismiss : GameMessage
    {
        public GameEventFellowshipDismiss(Session session, Player dismissedPlayer)
            : base(GameMessageOpcode.GameEvent, GameMessageGroup.UIQueue)
        {
            Writer.Write(session.Player.Guid.Full);
            Writer.Write(session.GameEventSequence++);
            Writer.Write((uint)GameEvent.GameEventType.FellowshipDismiss);
            Writer.Write(dismissedPlayer.Guid.Full);
        }
    }
}
