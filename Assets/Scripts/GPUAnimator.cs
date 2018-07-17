using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Nash
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class GPUAnimator : MonoBehaviour, IAnimator
{
    public const int FPS = 30;
    public float nextTick = 0;
    private float lastFrameTime;
    private bool pingPong = false;
    private float currentAnimTime;
    public float speed = 1;
    public float playbackSpeed = 1f;
    private int currentFrame;
    private int totalJoints = 0;
    private int totalVerts = 0;

    public GPUAnimation defaultAnimation;
    public GPUAnimationList meshAnimationList;
    private GPUAnimation[] animations;
    private int currentAnimIndex;

    private Queue<string> queuedAnims;
    private bool isPaused;
    private MeshRenderer meshRenderer;
    private int pixelsStartIndex;

    public Color[] meshTexturePixels;
    public Dictionary<string, int> jointsMap;
    public Transform[] joints;
    private int frame = 0;
    private int skinningTexSize;

    public int currentPixelIndex;

    private const int perFramePixelsCount = 3;

    private void Initialization()
    {
        defaultAnimation = meshAnimationList.meshAnimations[0];
        totalJoints = meshAnimationList.totalJoints;
        animations = meshAnimationList.meshAnimations;
        if (defaultAnimation.isVertsAnimation)
        {
            skinningTexSize = defaultAnimation.textureSize;
            pixelsStartIndex = 0;
        }
        else
        {
            skinningTexSize = meshAnimationList.skinningTexSize;
            pixelsStartIndex = defaultAnimation.startIndex;
        }

        meshRenderer = gameObject.GetComponent<MeshRenderer>();
        meshRenderer.material.SetInt("_StartPixelIndex", pixelsStartIndex);
        meshRenderer.material.SetInt("_SkinningTexSize", skinningTexSize);
        currentAnimIndex = 0;
        totalVerts = defaultAnimation.totalVerts;
    }

    void Start()
    {
        Initialization();
    }

    public GPUAnimation currentAnimation
    {
        get
        {
            return animations[currentAnimIndex];
        }
    }

    public void Play(int index)
    {
        if (animations.Length <= index || index < 0 || currentAnimIndex == index)
        {
            return;
        }

        if (queuedAnims != null)
        {
            queuedAnims.Clear();
        }

        currentAnimIndex = index;
        currentFrame = 0;
        currentAnimTime = 0;
        pingPong = false;
        isPaused = false;
        nextTick = Time.time;
    }

    public void UpdateTick(float time)
    {
        GPUAnimation cAnim = currentAnimation;

        float lodFPS = FPS;
        float totalSpeed = speed;
        float tickRate = Mathf.Max(0.0001f, 1f / lodFPS / totalSpeed);
        float actualDelta = time - lastFrameTime;
        bool finished = false;

        float pingPongMult = pingPong ? -1 : 1;
        if (speed * playbackSpeed < 0)
            currentAnimTime -= actualDelta * pingPongMult * totalSpeed;
        else
            currentAnimTime += actualDelta * pingPongMult * totalSpeed;

        if (currentAnimTime < 0)
        {
            currentAnimTime = cAnim.length;
            finished = true;
        }
        else if (currentAnimTime > cAnim.length)
        {
            if (cAnim.wrapMode == WrapMode.Loop)
            {
                currentAnimTime = 0;
            }
            finished = true;
        }

        nextTick = time + tickRate;
        lastFrameTime = time;

        float normalizedTime = currentAnimTime / cAnim.length;
        int previousFrame = currentFrame;
        currentFrame = Mathf.Min(Mathf.RoundToInt(normalizedTime * cAnim.totalFrames), cAnim.totalFrames - 1);

        if (cAnim.wrapMode == WrapMode.PingPong)
        {
            if (finished)
            {
                pingPong = !pingPong;
            }
        }

        if (finished)
        {
            bool stopUpdate = false;
            if (cAnim.wrapMode != WrapMode.Loop && cAnim.wrapMode != WrapMode.PingPong)
            {
                nextTick = float.MaxValue;
                stopUpdate = true;
            }
            currentAnimation.FireFinishedEvents(currentFrame);
            if (stopUpdate)
            {
                return;
            }
        }
        if (currentAnimation.isVertsAnimation)
        {
            currentPixelIndex = totalVerts * currentFrame;
        }
        else
        {
            currentPixelIndex = pixelsStartIndex + totalJoints * currentFrame * perFramePixelsCount;
        }
        meshRenderer.material.SetInt("_StartPixelIndex", currentPixelIndex);
    }
}