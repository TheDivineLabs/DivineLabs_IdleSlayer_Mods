namespace DivineLabs.IdleSlayer;

// No Unity dependency: input ordering and pointer ownership can be checked without a device.
internal sealed class MobileTouchState
{
    internal int? PointerId { get; private set; }
    internal bool Dragged { get; private set; }
    internal bool GestureActive { get; private set; }
    private float startX, startY;
    private int blockedFrame = -1, clickBlockedFrame = -1, tapFrame = -1;

    internal bool BlocksInput(int frame) => GestureActive || PointerId.HasValue || blockedFrame == frame;

    internal bool ObserveTouches(int count, int frame)
    {
        if (count >= 3 && !GestureActive)
        {
            CancelPointer(frame);
            GestureActive = true;
            return true;
        }
        if (count == 0 && GestureActive)
        {
            GestureActive = false;
            blockedFrame = clickBlockedFrame = frame;
        }
        return false;
    }

    internal bool BeginPointer(int id, float x, float y, int frame)
    {
        if (PointerId.HasValue || GestureActive || frame <= clickBlockedFrame) return false;
        PointerId = id;
        startX = x;
        startY = y;
        Dragged = false;
        tapFrame = -1;
        return true;
    }

    internal bool MovePointer(int id, float x, float y)
    {
        if (PointerId != id) return false;
        float dx = x - startX, dy = y - startY;
        Dragged |= dx * dx + dy * dy > 64;
        return Dragged;
    }

    internal bool EndPointer(int id, int frame)
    {
        if (PointerId != id) return false;
        PointerId = null;
        blockedFrame = frame;
        if (Dragged) clickBlockedFrame = frame;
        else tapFrame = frame;
        return Dragged;
    }

    internal bool ConsumeTap(int frame)
    {
        if (GestureActive || frame <= clickBlockedFrame || tapFrame != frame) return false;
        tapFrame = -1;
        return true;
    }

    internal void CancelPointer(int frame)
    {
        PointerId = null;
        tapFrame = -1;
        blockedFrame = clickBlockedFrame = frame;
    }

    internal void Reset(int frame)
    {
        CancelPointer(frame);
        GestureActive = false;
        Dragged = false;
    }
}
