using System;

namespace FirstFighter.Sim
{
    /// <summary>
    /// Deterministic Q16.16 fixed-point number where 1 integer unit = 1 milli-unit (1000 mu = 1.00 meter).
    /// Zero non-deterministic types are used anywhere in the simulation layer.
    /// </summary>
    public readonly struct Fix : IEquatable<Fix>, IComparable<Fix>
    {
        public const int Shift = 16;
        public const int OneRaw = 1 << Shift;
        public const int MeterMilli = 1000;

        public readonly int Raw;

        public static readonly Fix Zero = new Fix(0);
        public static readonly Fix One = FromMilli(1);
        public static readonly Fix Meter = FromMilli(MeterMilli);

        public Fix(int raw)
        {
            Raw = raw;
        }

        public static Fix FromRaw(int raw) => new Fix(raw);

        public static Fix FromMilli(int milliUnits) => new Fix(milliUnits << Shift);

        public static Fix FromInt(int milliUnits) => new Fix(milliUnits << Shift);

        public static Fix FromRatio(int numeratorMilli, int denominator)
        {
            if (denominator == 0)
            {
                return Zero;
            }
            long scaled = ((long)numeratorMilli << Shift) / denominator;
            return new Fix((int)scaled);
        }

        public int ToMilli() => Raw >> Shift;

        public int ToInt() => Raw >> Shift;

        public static Fix ScalePercent(Fix value, int percent)
        {
            long scaled = ((long)value.Raw * percent) / 100L;
            return new Fix((int)scaled);
        }

        public static Fix Abs(Fix v) => v.Raw < 0 ? new Fix(-v.Raw) : v;

        public static Fix Min(Fix a, Fix b) => a.Raw <= b.Raw ? a : b;

        public static Fix Max(Fix a, Fix b) => a.Raw >= b.Raw ? a : b;

        public static Fix Clamp(Fix v, Fix min, Fix max)
        {
            if (v.Raw < min.Raw) return min;
            if (v.Raw > max.Raw) return max;
            return v;
        }

        public static Fix operator +(Fix a, Fix b) => new Fix(a.Raw + b.Raw);
        public static Fix operator -(Fix a, Fix b) => new Fix(a.Raw - b.Raw);
        public static Fix operator -(Fix a) => new Fix(-a.Raw);

        public static Fix operator *(Fix a, Fix b)
        {
            long prod = ((long)a.Raw * b.Raw) >> Shift;
            return new Fix((int)prod);
        }

        public static Fix operator *(Fix a, int scalar) => new Fix(a.Raw * scalar);
        public static Fix operator *(int scalar, Fix a) => new Fix(a.Raw * scalar);

        public static Fix operator /(Fix a, int divisor)
        {
            if (divisor == 0) return Zero;
            return new Fix(a.Raw / divisor);
        }

        public static Fix operator /(Fix a, Fix b)
        {
            if (b.Raw == 0) return Zero;
            long quot = ((long)a.Raw << Shift) / b.Raw;
            return new Fix((int)quot);
        }

        public static bool operator ==(Fix a, Fix b) => a.Raw == b.Raw;
        public static bool operator !=(Fix a, Fix b) => a.Raw != b.Raw;
        public static bool operator <(Fix a, Fix b) => a.Raw < b.Raw;
        public static bool operator >(Fix a, Fix b) => a.Raw > b.Raw;
        public static bool operator <=(Fix a, Fix b) => a.Raw <= b.Raw;
        public static bool operator >=(Fix a, Fix b) => a.Raw >= b.Raw;

        public bool Equals(Fix other) => Raw == other.Raw;
        public override bool Equals(object? obj) => obj is Fix other && Equals(other);
        public override int GetHashCode() => Raw;
        public int CompareTo(Fix other) => Raw.CompareTo(other.Raw);
        public override string ToString() => $"{ToMilli()} mu (raw:{Raw})";
    }
}
