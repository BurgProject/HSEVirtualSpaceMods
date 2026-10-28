namespace GraphProcessor
{
	// MessageType is only available in UnityEditor so we have our own for runtime
	public enum NodeMessageType
	{
		None,
		Info,
		Warning,
		Error
	}

	public enum NodeMessageImportance
	{
		Low = 0,
		Medium = 1,
		High = 2,
	}
}