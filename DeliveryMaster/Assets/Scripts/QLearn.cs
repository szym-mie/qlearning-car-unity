using Unity.Collections;
using Unity.Mathematics;

public struct State
{
    // pozycje x, y sa w odniesieniu od startu manewru
    public int x; // x > 0.0 -> samochod po prawej stronie, x < 0.0 -> samochod po lewej stronie
    public int y; // y > 0.0 -> samochod pojechal do przodu
    public int dir; // dir > 0.0 -> obrocony w prawo, dir < 0.0 -> obrocony w lewo
    public int vel; // predkosc samochodu do przodu
    public int distL; // dystans do przeszkody - czujnik z lewej strony pojazdu
    public int distR; // dystans do przeszkody - czujnik z prawej strony pojazdu
}

public enum Action
{
    IDLE = 0, 
    DRIVE_LEFT = 1, 
    DRIVE_RIGHT = 2, 
    DRIVE_FWD = 3,  
    REVERSE = 4
}

/* How To Learn

Attempt {
    State state = learner.Discretise(observation);
    int action = learner.PickAction(state);
    // OBSERVE THE ENVIRONMENT
    // basically get nextState, reward
    learner.UpdateKnowledge(state, action, nextState, reward);
}

 */

public class QLearner
{
    private readonly int[] bins;
    private readonly int actionCount;

    private float alpha;
    private readonly float gamma;
    private float epsilon;

    private NativeArray<float> q;

    public QLearner(
        int[] bins,
        int actionCount,
        float alpha,
        float gamma,
        float epsilon)
    {
        this.bins = bins;
        this.actionCount = actionCount;

        this.alpha = alpha;
        this.gamma = gamma;
        this.epsilon = epsilon;

        int qSize = actionCount;
        foreach (int bin in bins)
        {
            qSize *= bin;
        }

        q = new NativeArray<float>(qSize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
    }

    public void Dispose()
    {
        if (q.IsCreated)
            q.Dispose();
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
        idxAcc += s.distL;
        idxAcc *= bins[5];
        idxAcc += s.distR;
        idxAcc *= actionCount;
        return idxAcc + action;
    }

    public State Discretise(float[] observation)
    {
        return new State
        {
            x = Digitize(observation[0], -2.5f, 2.5f, bins[0]),
            y = Digitize(observation[1], -1.0f, 11.0f, bins[1]),
            dir = Digitize(observation[2], -1.0f, 1.0f, bins[2]),
            vel = Digitize(observation[3], -1.0f, 3.0f, bins[3]),
            distL = Digitize(observation[4], 0.0f, 10.0f, bins[4]),
            distR = Digitize(observation[4], 0.0f, 10.0f, bins[5])
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

    public Action PickAction(State state)
    {
        if (UnityEngine.Random.value < epsilon)
            return (Action)UnityEngine.Random.Range(0, actionCount);

        float bestQ = float.NegativeInfinity;
        int bestAction = 0;
        for (int a = 0; a < actionCount; a++)
        {
            float qv = q[QIndex(state, a)];
            if (qv > bestQ)
            {
                bestQ = qv;
                bestAction = a;
            }
        }

        return (Action)bestAction;
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

    public void SetAlpha(float alpha)
    {
        this.alpha = alpha;
    }

    public void SetEpsilon(float epsilon)
    {
        this.epsilon = epsilon;
    }
}