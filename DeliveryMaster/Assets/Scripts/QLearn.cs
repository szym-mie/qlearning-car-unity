using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public struct State
{
    // pozycje x, y sa w odniesieniu od startu manewru
    public int x; // x > 0.0 -> samochod po prawej stronie, x < 0.0 -> samochod po lewej stronie
    public int y; // y > 0.0 -> samochod pojechal do przodu
    public int vel; // predkosc samochodu do przodu
    public int angle; // angle > 0.0 -> obrocony w prawo, angle < 0.0 -> obrocony w lewo
    // czujniki patrz¹ce do przodu
    public int distCent; // dystans do przeszkody - czujnik poœrodku pojazdu
    public int distFwdL; // dystans do przeszkody - czujnik z lewej strony pojazdu
    public int distFwdR; // dystans do przeszkody - czujnik z prawej strony pojazdu
    // czujniki patrz¹ce na bok
    public int distSideL; // dystans do przeszkody - czujnik z lewej strony pojazdu
    public int distSideR; // dystans do przeszkody - czujnik z prawej strony pojazdu
}

public struct Observation
{
    // opis pol identyczny jak dla State, tylko nie-dyskretny
    public float x;
    public float y;
    public float vel;
    public float angle;
    public float distCent;
    public float distFwdL;
    public float distFwdR;
    public float distSideL;
    public float distSideR;
}

public enum AngleAction
{
    DRIVE_F = 0,
    DRIVE_L = 1, 
    DRIVE_R = 2,
}

public enum CommandAction
{
    DRIVE = 0,
    STEER_L = 1,
    STEER_R = 2,
}

public class QLearner
{
    public float alpha;
    public float epsilon;
    public float actionChangeThreshold;

    private readonly int[] bins;
    private readonly int actionCount;
    private readonly float gamma;

    public readonly float distFwdMax;
    public readonly float distSideMax;

    public NativeArray<float> q;

    public QLearner(
        int[] bins,
        int actionCount,
        float alpha,
        float gamma,
        float epsilon,
        float actionChangeThreshold,
        float distFwdMax,
        float distSideMax)
    {
        this.bins = bins;
        this.actionCount = actionCount;

        this.alpha = alpha;
        this.gamma = gamma;
        this.epsilon = epsilon;
        this.actionChangeThreshold = actionChangeThreshold;
        //this.actionChangeTemporalDelay = actionChangeTemporalDelay;

        int qSize = actionCount;
        foreach (int bin in bins) qSize *= bin;
        q = new NativeArray<float>(qSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        this.distFwdMax = distFwdMax;
        this.distSideMax = distSideMax;
    }

    public void Dispose()
    {
        if (q.IsCreated) q.Dispose();
    }

    private int QIndex(State s, int actionIdx)
    {
        int idxAcc = s.x;
        idxAcc *= bins[1];
        idxAcc += s.y;
        idxAcc *= bins[2];
        idxAcc += s.vel;
        idxAcc *= bins[3];
        idxAcc += s.angle;
        idxAcc *= bins[4];
        idxAcc += s.distCent;
        idxAcc *= bins[5];
        idxAcc += s.distFwdL;
        idxAcc *= bins[6];
        idxAcc += s.distFwdR;
        idxAcc *= bins[7];
        idxAcc += s.distSideL;
        idxAcc *= bins[8];
        idxAcc += s.distSideR;
        idxAcc *= actionCount;
        return idxAcc + actionIdx;
    }

    public State Discretise(Observation observation)
    {
        return new State
        {
            x = Digitize(observation.x, -2.5f, +2.5f, bins[0]),
            y = Digitize(observation.y, -1.0f, +15.0f, bins[1]),
            vel = Digitize(observation.vel, -1.0f, +3.0f, bins[3]),
            angle = Digitize(observation.angle, -30.0f, +30.0f, bins[2]),
            distCent = Digitize(observation.distCent, +0.0f, distFwdMax, bins[4]),
            distFwdL = Digitize(observation.distFwdL, +0.0f, distFwdMax, bins[5]),
            distFwdR = Digitize(observation.distFwdR, +0.0f, distFwdMax, bins[6]),
            distSideL = Digitize(observation.distSideL, +0.0f, distSideMax, bins[7]),
            distSideR = Digitize(observation.distSideR, +0.0f, distSideMax, bins[8])
        };
    }

    private static int Digitize(
        float value,
        float minValue,
        float maxValue,
        int binCount)
    {
        float t = math.unlerp(minValue, maxValue, value);
        int idx = (int)math.floor(t * (binCount - 1));
        return math.clamp(idx, 0, binCount - 1);
    }

    public int PickAction(int currentAction, State state)
    {
        if (UnityEngine.Random.value < epsilon)
            return UnityEngine.Random.Range(0, actionCount);

        float qNew = float.NegativeInfinity;
        int newActionIdx = 0;
        int currentActionIdx = (int)currentAction;
        float qCurrent = q[QIndex(state, currentActionIdx)];
        for (int a = 0; a < actionCount; a++)
        {
            float qAction = q[QIndex(state, a)];
            if (qAction > qNew)
            {
                qNew = qAction;
                newActionIdx = a;
            }
        }

        if (qNew > qCurrent + this.actionChangeThreshold) {
            return newActionIdx;
        }
        return currentActionIdx;
    }

    public void UpdateKnowledge(
        State state,
        int actionIdx,
        State nextState,
        float reward)
    {
        float maxFutureQ = float.NegativeInfinity;
        for (int a = 0; a < actionCount; a++)
        {
            maxFutureQ = math.max(maxFutureQ, q[QIndex(nextState, a)]);
        }

        int idx = QIndex(state, actionIdx);
        float currentQ = q[idx];
        float target = reward + gamma * maxFutureQ;
        q[idx] = (1f - alpha) * currentQ + alpha * target;
    }
}