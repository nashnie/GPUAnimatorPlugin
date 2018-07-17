using UnityEngine;

[System.Serializable]
public class GPUAnimationEvent
{
    public enum Mode : byte { Data, String, Float };
    public string methodName;
    public int frame;
    public Mode eventType;
    public string stringValue;
    public float floatValue;
    public System.Object data;

    public void FireEvent()
    {
        if (eventType == Mode.Data)
        {
            SendMessage(methodName, data);
        }
        else if (eventType == Mode.Float)
        {
            SendMessage(methodName, floatValue);
        }
        else if (eventType == Mode.String)
        {
            SendMessage(methodName, stringValue);
        }
    }

    private void SendMessage<T>(string methodName, T value)
    {
    }
}