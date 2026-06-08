using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

public struct State
{
    // pozycje x, y sa w odniesieniu od startu manewru
    public int x; // x > 0.0 -> samochod po prawej stronie, x < 0.0 -> samochod po lewej stronie
    public int y; // y > 0.0 -> samochod pojechal do przodu
    public int dir; // dir > 0.0 -> obrocony w prawo, dir < 0.0 -> obrocony w lewo
    public int vel; // predkosc samochodu do przodu
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
    public float dir;
    public float vel;
    public float distCent;
    public float distFwdL;
    public float distFwdR;
    public float distSideL;
    public float distSideR;
}

public enum Action
{
    DRIVE_FWD = 0,
    DRIVE_LEFT = 1, 
    DRIVE_RIGHT = 2,
    //IDLE = 3,
    //REVERSE = 4
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

    private int QIndex(State s, int action)
    {
        int idxAcc = s.x;
        idxAcc *= bins[1];
        idxAcc += s.y;
        idxAcc *= bins[2];
        idxAcc += s.vel;
        idxAcc *= bins[3];
        idxAcc += s.dir;
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
        return idxAcc + action;
    }

    public State Discretise(Observation observation)
    {
        return new State
        {
            x = Digitize(observation.x, -2.5f, +2.5f, bins[0]),
            y = Digitize(observation.y, -1.0f, +15.0f, bins[1]),
            dir = Digitize(observation.dir, -40.0f, +40.0f, bins[2]),
            vel = Digitize(observation.vel, -1.0f, +3.0f, bins[3]),
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

    public Action PickAction(Action currentAction, State state)
    {
        if (UnityEngine.Random.value < epsilon)
            return (Action)UnityEngine.Random.Range(0, actionCount);

        float qNew = float.NegativeInfinity;
        int newActionNdx = 0;
        int currentActionNdx = (int)currentAction;
        float qCurrent = q[QIndex(state, currentActionNdx)];
        for (int a = 0; a < actionCount; a++)
        {
            float qAction = q[QIndex(state, a)];
            if (qAction > qNew)
            {
                qNew = qAction;
                newActionNdx = a;
            }
        }

        if (qNew > qCurrent + this.actionChangeThreshold) {
            return (Action)newActionNdx;
        }
        return (Action)currentActionNdx;
    }

    public void UpdateKnowledge(
        State state,
        Action action,
        State nextState,
        float reward)
    {
        float maxFutureQ = float.NegativeInfinity;
        for (int a = 0; a < actionCount; a++)
        {
            maxFutureQ = math.max(maxFutureQ, q[QIndex(nextState, a)]);
        }

        int idx = QIndex(state, (int)action);
        float currentQ = q[idx];
        float target = reward + gamma * maxFutureQ;
        q[idx] = (1f - alpha) * currentQ + alpha * target;
    }
}