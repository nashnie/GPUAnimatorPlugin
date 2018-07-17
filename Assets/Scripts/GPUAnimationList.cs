using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Nash
/// </summary>

[System.Serializable]
public class GPUAnimationList : ScriptableObject
{
    public int skinningTexSize;
    public int totalJoints;
    public GPUAnimation[] meshAnimations;
}
