﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Squared.Util {
    public delegate T InterpolatorSource<out T> (int index) where T : struct;
    public delegate T Interpolator<T> (InterpolatorSource<T> data, int dataOffset, float positionInWindow) where T : struct;
    public delegate T BoundInterpolatorSource<out T, U> (ref U obj, int index) where T : struct;
    public delegate T BoundInterpolator<T, U> (BoundInterpolatorSource<T, U> data, ref U obj, int dataOffset, float positionInWindow) where T : struct;

    public static class Interpolators<T>
        where T : struct {

        public delegate T LinearFn (T a, T b, float x);
        public delegate T CosineFn (T a, T b, float x);
        public delegate T CubicPFn (T a, T b, T c, T d);
        public delegate T CubicRFn (T a, T b, T c, T d, T p, float x, float x2, float x3);
        public delegate T HermiteFn (T a, T u, T d, T v, float t, float t2, float tSquared, float s, float s2, float sSquared);

        private static LinearFn _Linear = null;
        private static CosineFn _Cosine = null;
        private static CubicPFn _CubicP = null;
        private static CubicRFn _CubicR = null;
        private static HermiteFn _Hermite = null;

        static Interpolators () {
            var m_lerp = (
                typeof(T).GetMethod(
                    "Lerp", BindingFlags.Static | BindingFlags.Public, null, 
                    new[] { typeof(T), typeof(T), typeof(float) }, null
                )?.CreateDelegate(typeof(LinearFn))) as LinearFn;
            CompileFallbackExpressions(m_lerp);
            CompileNativeExpressions(m_lerp);
        }

        private static void CompileFallbackExpressions (LinearFn m_lerp) {
            var m_sub = Arithmetic.GetOperator<T, T>(Arithmetic.Operators.Subtract, optional: m_lerp != null);
            var m_add = Arithmetic.GetOperator<T, T>(Arithmetic.Operators.Add, optional: m_lerp != null);
            var m_mul_float = Arithmetic.GetOperator<T, float>(Arithmetic.Operators.Multiply, optional: m_lerp != null);

            if (m_lerp != null)
                _Linear = m_lerp;
            else
                _Linear = (a, b, x) => {
                    return m_add(a, m_mul_float(m_sub(b, a), x));
                };

            if (m_lerp != null)
                _Cosine = (a, b, x) => {
                    var temp = (1.0f - (float)Math.Cos(x * Math.PI)) * 0.5f;
                    return m_lerp(a, b, temp);
                };
            else
                _Cosine = (a, b, x) => {
                    var temp = (1.0f - (float)Math.Cos(x * Math.PI)) * 0.5f;
                    return m_add(a, m_mul_float(m_sub(b, a), temp));
                };

            _CubicP = (a, b, c, d) => {
                return m_sub(m_sub(d, c), m_sub(a, b));
            };

            _CubicR = (a, b, c, d, p, x, x2, x3) => {
                return m_add(
                    m_add(
                        m_mul_float(p, x3),
                        m_mul_float(
                            m_sub(
                                m_sub(a, b),
                                p
                            ),
                            x2
                        )
                    ),
                    m_add(
                        m_mul_float(
                            m_sub(c, a),
                            x
                        ),
                        b
                    )
                );
            };

            _Hermite = (a, u, d, v, t, t2, tSquared, s, s2, sSquared) => {
                return m_sub(
                    m_add(
                        m_add(
                            m_mul_float(a, sSquared * (1 + t2)),
                            m_mul_float(d, tSquared * (1 + s2))
                        ),
                        m_mul_float(u, sSquared * t)
                    ),
                    m_mul_float(v, s * tSquared)
                );
            };
        }

        private static void CompileNativeExpressions (LinearFn m_lerp) {
#if !DYNAMICMETHOD
            if (m_lerp == null)
                Arithmetic.CompileExpression(
                    (a, b, x) =>
                        a + ((b - a) * x),
                    out _Linear
                );

            // FIXME: This is the best we can do
            if (m_lerp == null)
                Arithmetic.CompileExpression(
                    (a, b, x) =>
                        a + ((b - a) * ((1.0f - (float)Math.Cos(x * Math.PI)) * 0.5f)),
                    out _Cosine
                );

            Arithmetic.CompileExpression(
                (a, b, c, d) =>
                    (d - c) - (a - b),
                out _CubicP
            );

            Arithmetic.CompileExpression(
                (a, b, c, d, p, x, x2, x3) =>
                    (p * x3) + ((a - b - p) * x2) + ((c - a) * x) + b,
                out _CubicR
            );
#endif
        }

        public static T Null (InterpolatorSource<T> data, int dataOffset, float positionInWindow) {
            return data(dataOffset);
        }

        public static T Linear (InterpolatorSource<T> data, int dataOffset, float positionInWindow) {
            return _Linear(
                data(dataOffset),
                data(dataOffset + 1),
                positionInWindow
            );
        }

        public static T Cosine (InterpolatorSource<T> data, int dataOffset, float positionInWindow) {
            return _Cosine(
                data(dataOffset),
                data(dataOffset + 1),
                positionInWindow
            );
        }

        public static T Cubic (InterpolatorSource<T> data, int dataOffset, float positionInWindow) {
            if (positionInWindow < 0) {
                var n = Math.Ceiling(Math.Abs(positionInWindow));
                positionInWindow += (float)n;
                dataOffset -= (int)n;
            }

            T a = data(dataOffset - 1);
            T b = data(dataOffset);
            T c = data(dataOffset + 1);
            T d = data(dataOffset + 2);
            T p = _CubicP(a, b, c, d);
            float x2 = positionInWindow * positionInWindow;
            float x3 = positionInWindow * x2;
            return _CubicR(a, b, c, d, p, positionInWindow, x2, x3);
        }

        public static T Hermite (InterpolatorSource<T> data, int dataOffset, float positionInWindow) {
            if (positionInWindow < 0) {
                var n = Math.Ceiling(Math.Abs(positionInWindow));
                positionInWindow += (float)n;
                dataOffset -= (int)n;
            }

            T a = data(dataOffset);
            T u = data(dataOffset + 1);
            T d = data(dataOffset + 2);
            T v = data(dataOffset + 3);

            var tSquared = positionInWindow * positionInWindow;
            var t2 = positionInWindow * 2;
            var s = 1 - positionInWindow;
            var s2 = s * 2;
            var sSquared = s * s;

            return _Hermite(a, u, d, v, positionInWindow, t2, tSquared, s, s2, sSquared);
        }

        public static T Null<U> (BoundInterpolatorSource<T, U> data, ref U obj, int dataOffset, float positionInWindow) {
            return data(ref obj, dataOffset);
        }

        public static T Linear<U> (BoundInterpolatorSource<T, U> data, ref U obj, int dataOffset, float positionInWindow) {
            return _Linear(
                data(ref obj, dataOffset),
                data(ref obj, dataOffset + 1),
                positionInWindow
            );
        }

        public static T Cosine<U> (BoundInterpolatorSource<T, U> data, ref U obj, int dataOffset, float positionInWindow) {
            return _Cosine(
                data(ref obj, dataOffset),
                data(ref obj, dataOffset + 1),
                positionInWindow
            );
        }

        private class ExponentialInterpolator {
            public float ExponentIn, ExponentMidpoint, ExponentOut;

            public T Interpolate<U> (BoundInterpolatorSource<T, U> data, ref U obj, int dataOffset, float positionInWindow) {
                var centered = positionInWindow - 0.5f;
                var exponent1 = Arithmetic.Lerp(ExponentIn, ExponentMidpoint, positionInWindow * 2f);
                var exponent2 = Arithmetic.Lerp(ExponentMidpoint, ExponentOut, centered);
                var left =
                    (centered < 0)
                        ? Math.Pow(positionInWindow * 2f, exponent1)
                        : 1;
                var right =
                    (centered >= 0)
                        ? 1 - Math.Pow(1 - (centered * 2f), exponent2)
                        : 0;
                var finalPosition = (left + right) / 2f;
                return _Linear(
                    data(ref obj, dataOffset),
                    data(ref obj, dataOffset + 1),
                    (float)finalPosition
                );
            }
        }

        public static BoundInterpolator<T, U> Exponential<U> (float exponent = 2) {
            var obj = new ExponentialInterpolator {
                ExponentIn = exponent,
                ExponentMidpoint = exponent,
                ExponentOut = exponent
            };
            return obj.Interpolate;
        } 

        public static BoundInterpolator<T, U> Exponential<U> (float start, float? mid, float? end) {
            var obj = new ExponentialInterpolator {
                ExponentIn = start,
                ExponentMidpoint = mid ?? start,
                ExponentOut = end ?? start
            };
            return obj.Interpolate;
        } 

        public abstract class Easing {
            public abstract float GetT (float t);

            public T Get (InterpolatorSource<T> data, int dataOffset, float positionInWindow) {
                return _Linear(
                    data(dataOffset),
                    data(dataOffset + 1),
                    GetT(positionInWindow)
                );
            }
        }

        public static class Ease {
            internal class EaseInSine : Easing {
                public override float GetT (float t) => 
                    (float)(1 - Math.Cos((t * Math.PI) / 2));
            }

            internal class EaseOutSine : Easing {
                public override float GetT (float t) =>
                    (float)(Math.Sin((t * Math.PI) / 2));
            }

            internal class EaseSine : Easing {
                public override float GetT (float t) => 
                    (float)-(Math.Cos(Math.PI * t) - 1) / 2;
            }

            public static readonly Easing InSine = new EaseInSine(),
                Sine = new EaseSine(),
                OutSine = new EaseOutSine();

            public class InExponential : Easing {
                public float Exponent = 2;

                public override float GetT (float t) => 
                    (float)Math.Pow(t, Exponent);
            }

            public class OutExponential : Easing {
                public float Exponent = 2;

                public override float GetT (float t) =>
                    1f - (float)Math.Pow(1 - t, Exponent);
            }

            public class Exponential : Easing {
                public float Exponent = 2;

                public override float GetT (float t) {
                    var tE = Math.Pow(t, Exponent);
                    return (float)(tE / (tE + Math.Pow(1 - t, Exponent)));
                }
            }
        }

        private class EasedInterpolator {
            public Easing Ease;

            public T Interpolate<U> (BoundInterpolatorSource<T, U> data, ref U obj, int dataOffset, float positionInWindow) {
                var t = Ease.GetT(positionInWindow);
                return _Linear(
                    data(ref obj, dataOffset),
                    data(ref obj, dataOffset + 1),
                    t
                );
            }
        }

        public static BoundInterpolator<T, U> Eased<U> (Easing easing) {
            var obj = new EasedInterpolator { Ease = easing };
            return obj.Interpolate<U>;
        }

        public static T Cubic<U> (BoundInterpolatorSource<T, U> data, ref U obj, int dataOffset, float positionInWindow) {
            if (positionInWindow < 0) {
                var n = Math.Ceiling(Math.Abs(positionInWindow));
                positionInWindow += (float)n;
                dataOffset -= (int)n;
            }

            T a = data(ref obj, dataOffset - 1);
            T b = data(ref obj, dataOffset);
            T c = data(ref obj, dataOffset + 1);
            T d = data(ref obj, dataOffset + 2);
            T p = _CubicP(a, b, c, d);
            float x2 = positionInWindow * positionInWindow;
            float x3 = positionInWindow * x2;
            return _CubicR(a, b, c, d, p, positionInWindow, x2, x3);
        }

        public static T Hermite<U> (BoundInterpolatorSource<T, U> data, ref U obj, int dataOffset, float positionInWindow) {
            if (positionInWindow < 0) {
                var n = Math.Ceiling(Math.Abs(positionInWindow));
                positionInWindow += (float)n;
                dataOffset -= (int)n;
            }

            T a = data(ref obj, dataOffset);
            T u = data(ref obj, dataOffset + 1);
            T d = data(ref obj, dataOffset + 2);
            T v = data(ref obj, dataOffset + 3);

            var tSquared = positionInWindow * positionInWindow;
            var t2 = positionInWindow * 2;
            var s = 1 - positionInWindow;
            var s2 = s * 2;
            var sSquared = s * s;

            return _Hermite(a, u, d, v, positionInWindow, t2, tSquared, s, s2, sSquared);
        }

        private static T[] _TemporaryValues = new T[4];
        private static int _NumTemporaryValues = 0;
        private static InterpolatorSource<T> _TemporarySource;

        private static T TemporarySource (int index) {
            return _TemporaryValues[Arithmetic.Wrap(index, 0, _NumTemporaryValues - 1)];
        }

        public static T Interpolate (Interpolator<T> interpolator, T a, T b, float progress) {
            if ((_TemporaryValues == null) || (_TemporaryValues.Length < 2))
                _TemporaryValues = new T[2];
            if (_TemporarySource == null)
                _TemporarySource = TemporarySource;

            _TemporaryValues[0] = a;
            _TemporaryValues[1] = b;
            _NumTemporaryValues = 2;

            return interpolator(_TemporarySource, 0, progress);
        }

        public static T Interpolate (Interpolator<T> interpolator, T[] values, float progress) {
            var previousValues = _TemporaryValues;
            if (_TemporarySource == null)
                _TemporarySource = TemporarySource;

            try {
                _TemporaryValues = values;
                _NumTemporaryValues = values.Length;
                return interpolator(_TemporarySource, 0, progress);
            } finally {
                _TemporaryValues = previousValues;
            }
        }

        private static readonly Dictionary<string, Interpolator<T>> Cache = 
            new Dictionary<string, Interpolator<T>>(StringComparer.OrdinalIgnoreCase);

        public static Interpolator<T> GetByName (string name) {
            Interpolator<T> result;

            lock (Cache)
            if (Cache.TryGetValue(name, out result))
                return result;

            var types = new Type[] {
                typeof(InterpolatorSource<T>), typeof(int), typeof(float)
            };
            Type myType = typeof(Interpolators<T>);
            Type resultType = typeof(Interpolator<T>);

            var mi = myType.GetMethod(name, types);
            if (mi == null)
                mi = myType.GetMethod("Null", types);
            result = Delegate.CreateDelegate(resultType, null, mi) as Interpolator<T>;

            lock (Cache)
                Cache[name] = result;

            return result;
        }

        public static BoundInterpolator<T, U> GetBoundDefault<U>() {
            return Linear<U>;
        }

        private static class CacheContainer<U> {
            public static readonly Dictionary<string, BoundInterpolator<T, U>> Cache = 
                new Dictionary<string, BoundInterpolator<T, U>>(StringComparer.OrdinalIgnoreCase);
        }

        public static BoundInterpolator<T, U> GetBoundByName<U> (string name) {
            BoundInterpolator<T, U> result;

            lock (CacheContainer<U>.Cache)
            if (CacheContainer<U>.Cache.TryGetValue(name, out result))
                return result;

            var myType = typeof(Interpolators<T>);
            var resultType = typeof(BoundInterpolator<T, U>);
            var methods = myType.GetMethods();

            MethodInfo mi = null;

            foreach (var m in methods) {
                if (m.Name != name)
                    continue;
                if (!m.ContainsGenericParameters)
                    continue;
                var p = m.GetParameters();
                if (p?.Length != 4)
                    continue;

                mi = m;
                break;
            }

            if (mi == null)
                return GetBoundByName<U>("Null");

            var mii = mi.MakeGenericMethod(typeof(U));
            result = Delegate.CreateDelegate(resultType, null, mii) as BoundInterpolator<T, U>;

            lock (CacheContainer<U>.Cache)
                CacheContainer<U>.Cache[name] = result;

            return result;
        }

        public static Interpolator<T> Default {
            get {
                return Linear;
            }
        }
    }

    public static class InterpolatorExtensions {
        public static T Interpolate<T> (this Interpolator<T> interpolator, T a, T b, float progress) 
            where T : struct
        {
            return Interpolators<T>.Interpolate(interpolator, a, b, progress);
        }
    }
}
