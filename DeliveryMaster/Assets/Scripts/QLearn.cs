using Unity.Collections;
using Unity.Mathematics;

/* TODO: model the state of the car
wariant 1:
x, y - pozycja samochodu
dir - kierunek w radianach (cos jak kompas)
vel - predkosc samochodu
dist_l, dist_r - odleglosc od przeszkod w metrach (raycasty)s
 */
public struct State
{
    public int x;
    public int vx;
    public int a;
    public int va;
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
    private readonly int4 bins;
    private readonly int actionCount;

    private float alpha;
    private readonly float gamma;
    private float epsilon;

    private NativeArray<float> q;

    public QLearner(
        int4 bins,
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

        int qSize =
            bins.x *
            bins.y *
            bins.z *
            bins.w *
            actionCount;

        q = new NativeArray<float>(
            qSize,
            Allocator.Persistent,
            NativeArrayOptions.ClearMemory);
    }

    public void Dispose()
    {
        if (q.IsCreated)
            q.Dispose();
    }

    private int QIndex(State s, int action)
    {
        int idxAcc = s.x;
        idxAcc *= bins.y;
        idxAcc += s.vx;
        idxAcc *= bins.z;
        idxAcc += s.a;
        idxAcc *= bins.w;
        idxAcc += s.va;
        idxAcc *= actionCount;
        return idxAcc + action;
        //return
        //    ((((s.x * bins.y)
        //     + s.vx) * bins.z
        //     + s.a) * bins.w
        //     + s.va) * actionCount
        //     + action;
    }

    public State Discretise(float4 observation)
    {
        return new State
        {
            x = Digitize(observation.x, -2.5f, 2.5f, bins.x),
            vx = Digitize(observation.y, -0.5f, 0.5f, bins.y),
            a = Digitize(observation.z, -0.5f, 0.5f, bins.z),
            va = Digitize(observation.w, -1.0f, 1.0f, bins.w)
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

    public int PickAction(State state)
    {
        if (UnityEngine.Random.value < epsilon)
            return UnityEngine.Random.Range(0, actionCount);

        float bestQ = float.NegativeInfinity;
        int bestAction = 0;

        for (int a = 0; a < actionCount; ++a)
        {
            float qv = q[QIndex(state, a)];

            if (qv > bestQ)
            {
                bestQ = qv;
                bestAction = a;
            }
        }

        return bestAction;
    }

    public void UpdateKnowledge(
        State state,
        int action,
        State nextState,
        float reward)
    {
        float maxFutureQ = float.NegativeInfinity;

        for (int a = 0; a < actionCount; ++a)
        {
            maxFutureQ =
                math.max(
                    maxFutureQ,
                    q[QIndex(nextState, a)]);
        }

        int idx = QIndex(state, action);

        float currentQ = q[idx];

        float target =
            reward +
            gamma * maxFutureQ;

        q[idx] =
            (1f - alpha) * currentQ +
            alpha * target;
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