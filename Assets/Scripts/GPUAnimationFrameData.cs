using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class GPUAnimationFrameData
{
}

[System.Serializable]
public class Skeleton : ScriptableObject
{
    public Matrix4x4[] jontFrameMatrixs;
}
