using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nash
/// GPUAnimatorManager
/// </summary>
public class GPUAnimatorManager : MonoBehaviour
{
    public static GPUAnimatorManager Instance
    {
        get
        {
            if (mInstance == null)
            {
                mInstance = FindObjectOfType<GPUAnimatorManager>();
                if (mInstance == null)
                {
                    mInstance = new GameObject("GPUAnimatorManager").AddComponent<GPUAnimatorManager>();
                }
            }
            return mInstance;
        }
    }

    private static GPUAnimatorManager mInstance = null;
    private static List<GPUAnimator> mAnimators = new List<GPUAnimator>(100);

    public static void RegisterAnimator(GPUAnimator animator)
    {
        if (Instance)
        {
            mAnimators.Add(animator);
        }
    }

    public static void UnregisterAnimator(GPUAnimator animator)
    {
        mAnimators.Remove(animator);
    }

    private void Update()
    {
        float t = Time.time;
        int c = mAnimators.Count;
        for (int i = 0; i < c; i++)
        {
            GPUAnimator animator = mAnimators[i];
            if (t >= animator.nextTick)
            {
                animator.UpdateTick(t);
            }
        }
    }
}