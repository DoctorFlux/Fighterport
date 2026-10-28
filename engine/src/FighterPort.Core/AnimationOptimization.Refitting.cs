namespace FighterPort.Core;

public static partial class AnimationOptimization
{
    // Proposals use sampled least squares only to choose coefficients. Acceptance
    // always uses the continuous rational baseline certificate in TestSpan.
    // The native caller still encodes/reopens and checks cumulative model error.
    static void RefitSpans(IEnumerable<ChannelState> states, Budget budget)
    {
        foreach (var state in states.Where(s => s.Reason is null && !s.Exact))
        {
            budget.Check();
            if (state.Alive.Count(v => v) < 3) continue;
            var context = new RefitContext(state);
            int left = Array.FindIndex(state.Alive, v => v);
            while (left >= 0)
            {
                budget.Check();
                while (state.Next[left] is var same && same >= 0 && state.Keys[same].Frame == state.Keys[left].Frame) left = same;
                var rights = new List<int>();
                int at = state.Next[left];
                while (at >= 0 && rights.Count < 64)
                {
                    budget.Charge(1);
                    int incoming = at;
                    while (state.Next[at] is var duplicate && duplicate >= 0 && state.Keys[duplicate].Frame == state.Keys[incoming].Frame)
                        at = duplicate;
                    rights.Add(incoming);
                    // Exact authored points split the search; they may be an
                    // endpoint, but must not disappear inside a candidate span.
                    if (context.Locked(incoming) || state.Mandatory[incoming]) break;
                    at = state.Next[at];
                }
                bool accepted = false;
                var ends = new SortedSet<int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
                if (rights.Count > 0) ends.Add(rights.Count - 1);
                for (int size = 32; size >= 2; size /= 2) if (size <= rights.Count) ends.Add(size - 1);
                foreach (int end in ends)
                {
                    int right = rights[end];
                    if (!state.AllowsSpan(left, right) || !context.CanRemoveInterior(left, right)) continue;
                    if (TryRefitSpan(context, left, right, budget)) { left = right; accepted = true; break; }
                }
                if (!accepted) left = state.Next[left];
            }
            // A continuous generated corner can sometimes keep its authored
            // value/time but share one newly fitted incoming/outgoing slope.
            // Authored/unknown pairs never enter this operation.
            RefitGeneratedCorners(context, budget);
        }
    }

    sealed class RefitContext(ChannelState state)
    {
        public readonly ChannelState State = state;
        readonly Dictionary<double, int[]> groups = state.Original.Keys.Select((key, index) => (key, index))
            .GroupBy(item => item.key.Frame).ToDictionary(g => g.Key, g => g.Select(item => item.index).ToArray());

        public bool Locked(int index)
        {
            var key = State.Original.Keys[index];
            if (HasSignedZero(key) || Bits(key.InTangent) != Bits(key.OutTangent)
                || State.Frames.Contains(key.Frame) || State.Ranges.Any(r => key.Frame >= r.Start && key.Frame <= r.End)) return true;
            var group = groups[key.Frame];
            return group.Length > 1 && (group.Length != 2 || !State.GeneratedPairs.Contains(key.Frame)
                || group.Any(i => HasSignedZero(State.Original.Keys[i])
                    || Bits(State.Original.Keys[i].Value) != Bits(key.Value)
                    || Bits(State.Original.Keys[i].InTangent) != Bits(State.Original.Keys[i].OutTangent)));
        }

        public bool CanRemoveInterior(int left, int right)
        {
            int middle = State.Next[left];
            if (middle < 0 || middle == right) return false;
            while (middle >= 0 && middle != right)
            {
                if (State.Mandatory[middle] || Locked(middle)) return false;
                middle = State.Next[middle];
            }
            return middle == right;
        }

        public bool IsGeneratedPair(int incoming, int outgoing) => groups[State.Keys[incoming].Frame].Length == 2
            && State.Keys[incoming].Frame == State.Keys[outgoing].Frame
            && State.GeneratedPairs.Contains(State.Keys[incoming].Frame) && !Locked(incoming) && !Locked(outgoing);

        public Key Left(int index, float slope)
        {
            var key = State.Keys[index];
            int previous = State.Previous[index];
            bool unusedIncoming = previous >= 0 && State.Keys[previous].Frame == key.Frame
                || previous < 0 && State.Original.LoopFrameLimit is null;
            return unusedIncoming ? key with { InTangent = slope, OutTangent = slope } : key with { OutTangent = slope };
        }

        public Key Right(int index, float slope)
        {
            var key = State.Keys[index];
            int next = State.Next[index];
            bool unusedOutgoing = next >= 0 && State.Keys[next].Frame == key.Frame
                || next < 0 && State.Original.LoopFrameLimit is null;
            return unusedOutgoing ? key with { InTangent = slope, OutTangent = slope } : key with { InTangent = slope };
        }
    }

    static int NativeRows(Key key, bool newlyFitted) => newlyFitted && Bits(key.InTangent) != Bits(key.OutTangent) ? 2 : 1;

    static bool TryRefitSpan(RefitContext context, int left, int right, Budget budget)
    {
        var state = context.State;
        bool freeLeft = !context.Locked(left), freeRight = !context.Locked(right);
        if (!freeLeft && !freeRight) return false;
        var a = state.Keys[left]; var b = state.Keys[right];
        double dt = b.Frame - a.Frame;
        if (!(dt > 0) || !double.IsFinite(dt)) return false;
        int rows = 0;
        for (int i = left; i >= 0; i = state.Next[i])
        {
            budget.Charge(1); rows += NativeRows(state.Keys[i], state.RefitChanged[i]);
            if (i == right) break;
        }
        if (rows < 3) return false;
        var slopes = new List<(double A, double B)>();
        double chord = ((double)b.Value - a.Value) / dt;
        slopes.Add((freeLeft ? chord : a.OutTangent, freeRight ? chord : b.InTangent));
        if (FitSlopes(state, left, right, freeLeft, freeRight, budget, out var fitted))
        {
            slopes.Add(fitted);
            slopes.Add((freeLeft ? (fitted.A + a.OutTangent) * .5 : a.OutTangent,
                freeRight ? (fitted.B + b.InTangent) * .5 : b.InTangent));
        }
        slopes.Add((a.OutTangent, b.InTangent));
        var seen = new HashSet<(int, int)>();
        (Key A, Key B, double Value, double Speed, int Rows, double Score)? best = null;
        foreach (var proposal in slopes)
        {
            if (!RoundSlope(proposal.A, a.OutTangent, state.SpeedBound, freeLeft, out float am)
                || !RoundSlope(proposal.B, b.InTangent, state.SpeedBound, freeRight, out float bm)
                || !seen.Add((Bits(am), Bits(bm)))) continue;
            var candidateA = freeLeft ? context.Left(left, am) : a;
            var candidateB = freeRight ? context.Right(right, bm) : b;
            int nextRows = NativeRows(candidateA, state.RefitChanged[left] || candidateA != a)
                + NativeRows(candidateB, state.RefitChanged[right] || candidateB != b);
            if (nextRows >= rows) continue;
            budget.BeginCandidate(); state.Candidates++; state.RefitCandidates++;
            if (!TestSpan(state, left, right, candidateA, candidateB, budget, out double value, out double speed)) continue;
            double score = Math.Max(value / state.ValueBound, speed / state.SpeedBound);
            if (best is null || nextRows < best.Value.Rows || nextRows == best.Value.Rows && score < best.Value.Score)
                best = (candidateA, candidateB, value, speed, nextRows, score);
            if (score == 0 && nextRows == 2) break;
        }
        if (best is not { } accepted) return false;
        // No proposal mutates the working graph until all interval proofs pass.
        state.RefitChanged[left] |= accepted.A != a; state.RefitChanged[right] |= accepted.B != b;
        state.Keys[left] = accepted.A; state.Keys[right] = accepted.B;
        int index = state.Next[left];
        while (index >= 0 && index != right)
        {
            int next = state.Next[index]; state.Remove(index, accepted.Value, accepted.Speed); budget.Merges++; index = next;
        }
        state.ValueError = Math.Max(state.ValueError, accepted.Value); state.SpeedError = Math.Max(state.SpeedError, accepted.Speed);
        state.RefitSpans++;
        return true;
    }

    static bool RoundSlope(double proposed, float original, double tolerance, bool free, out float value)
    {
        if (!free) { value = original; return true; }
        double first = original - tolerance, last = original + tolerance;
        value = (float)Math.Clamp(proposed, first, last);
        if (!float.IsFinite(value) || Bits(value) == int.MinValue) return false;
        if (value < first) value = MathF.BitIncrement(value);
        if (value > last) value = MathF.BitDecrement(value);
        return value >= first && value <= last;
    }

    static bool FitSlopes(ChannelState state, int left, int right, bool freeLeft, bool freeRight, Budget budget,
        out (double A, double B) slopes)
    {
        var first = state.Keys[left]; var last = state.Keys[right];
        double dt = last.Frame - first.Frame;
        double n00 = 0, n01 = 0, n11 = 0, y0 = 0, y1 = 0;
        void Equation(double h0, double h1, double target, double weight)
        {
            if (!freeLeft) { target -= h0 * first.OutTangent; h0 = 0; }
            if (!freeRight) { target -= h1 * last.InTangent; h1 = 0; }
            h0 *= weight; h1 *= weight; target *= weight;
            n00 += h0 * h0; n01 += h0 * h1; n11 += h1 * h1; y0 += h0 * target; y1 += h1 * target;
        }
        for (int i = left; i < right; i++)
        {
            var p = state.Original.Keys[i]; var q = state.Original.Keys[i + 1];
            if (q.Frame <= p.Frame) continue;
            double span = q.Frame - p.Frame;
            foreach (double fraction in new[] { 0d, .25, .5, .75, 1 })
            {
                budget.Charge(1);
                double frame = p.Frame + span * fraction, u = (frame - first.Frame) / dt;
                double u2 = u * u, u3 = u2 * u;
                double h0 = dt * (u3 - 2 * u2 + u), h1 = dt * (u3 - u2);
                double baseValue = (2 * u3 - 3 * u2 + 1) * first.Value + (-2 * u3 + 3 * u2) * last.Value;
                double f2 = fraction * fraction, f3 = f2 * fraction;
                double source = (2 * f3 - 3 * f2 + 1) * p.Value + (f3 - 2 * f2 + fraction) * span * p.OutTangent
                    + (-2 * f3 + 3 * f2) * q.Value + (f3 - f2) * span * q.InTangent;
                Equation(h0, h1, source - baseValue, 1 / state.ValueBound);
                double g0 = 3 * u2 - 4 * u + 1, g1 = 3 * u2 - 2 * u;
                double baseSpeed = ((6 * u2 - 6 * u) * first.Value + (-6 * u2 + 6 * u) * last.Value) / dt;
                double sourceSpeed = ((6 * f2 - 6 * fraction) * p.Value + (-6 * f2 + 6 * fraction) * q.Value) / span
                    + (3 * f2 - 4 * fraction + 1) * p.OutTangent + (3 * f2 - 2 * fraction) * q.InTangent;
                Equation(g0, g1, sourceSpeed - baseSpeed, 1 / state.SpeedBound);
            }
        }
        double am = first.OutTangent, bm = last.InTangent;
        if (freeLeft && freeRight)
        {
            double determinant = n00 * n11 - n01 * n01;
            if (!(determinant > 0) || !double.IsFinite(determinant)) { slopes = default; return false; }
            am = (y0 * n11 - y1 * n01) / determinant; bm = (y1 * n00 - y0 * n01) / determinant;
        }
        else if (freeLeft && n00 > 0) am = y0 / n00;
        else if (freeRight && n11 > 0) bm = y1 / n11;
        slopes = (am, bm);
        return double.IsFinite(am) && double.IsFinite(bm);
    }

    static void RefitGeneratedCorners(RefitContext context, Budget budget)
    {
        var state = context.State;
        for (int incoming = 0; incoming < state.Keys.Length; incoming++)
        {
            budget.Check();
            if (!state.Alive[incoming]) continue;
            int outgoing = state.Next[incoming];
            if (outgoing < 0 || !context.IsGeneratedPair(incoming, outgoing)) continue;
            int left = state.Previous[incoming], right = state.Next[outgoing];
            if (left < 0 || right < 0 || state.Keys[left].Frame >= state.Keys[incoming].Frame || state.Keys[right].Frame <= state.Keys[outgoing].Frame
                || !state.AllowsSpan(left, incoming) || !state.AllowsSpan(outgoing, right)) continue;
            var a = state.Keys[incoming]; var b = state.Keys[outgoing];
            double first = Math.Max(state.Original.Keys[incoming].InTangent - state.SpeedBound, state.Original.Keys[outgoing].OutTangent - state.SpeedBound);
            double last = Math.Min(state.Original.Keys[incoming].InTangent + state.SpeedBound, state.Original.Keys[outgoing].OutTangent + state.SpeedBound);
            if (first > last) continue;
            float tangent = (float)Math.Clamp(((double)a.InTangent + b.OutTangent) * .5, first, last);
            if (!float.IsFinite(tangent) || Bits(tangent) == int.MinValue || tangent < first || tangent > last) continue;
            var candidate = a with { InTangent = tangent, OutTangent = tangent };
            budget.BeginCandidate(); state.Candidates++; state.RefitCandidates++;
            if (!TestSpan(state, left, incoming, state.Keys[left], candidate, budget, out double lv, out double ls)
                || !TestSpan(state, outgoing, right, candidate, state.Keys[right], budget, out double rv, out double rs)) continue;
            state.Keys[incoming] = candidate; state.RefitChanged[incoming] |= candidate != a;
            state.Remove(outgoing, Math.Max(lv, rv), Math.Max(ls, rs)); budget.Merges++; state.RefitSpans++;
        }
    }
}
