
using GraphProcessor;

namespace HSE.CodeCraft
{
    public abstract class BaseHSENode: BaseNode
    {
        public virtual string EditorNodeTitleIconName => "";
        public override void OnViewInitialized() => ClearMessages();
        protected void AddStateMessage(string message, bool state, NodeMessageType messageType = NodeMessageType.Error, NodeMessageImportance importance = NodeMessageImportance.High)
        {
            if (state)
                AddMessage(message, messageType, importance);
            else RemoveMessageContains(message); 
        }
    }
}
